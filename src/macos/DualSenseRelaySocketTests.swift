// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Ephemeral loopback fake peers only. No app, USB, guest, Sony or port 33743.
import Darwin
import Dispatch
import Foundation

private final class TestClock: @unchecked Sendable {
    private let lock = NSLock()
    private var value: UInt64
    init(_ value: UInt64 = 0) { self.value = value }
    func read() -> UInt64 { lock.lock(); defer { lock.unlock() }; return value }
    func set(_ value: UInt64) { lock.lock(); defer { lock.unlock() }; self.value = value }
}

/// The worker owns both descriptors. Results are read only after done.wait().
private final class FakeRelay: @unchecked Sendable {
    static let greeting = Array("PSKeyboard-DualSenseV5-Relay/1\n".utf8)
    let port: UInt16
    let done = DispatchSemaphore(value: 0)
    private(set) var bytes: [UInt8] = []
    private(set) var eof = false
    private(set) var readError: Int32 = 0

    init(greeting: [UInt8] = FakeRelay.greeting, fragmented: Bool = false,
         receiveBuffer: Int32? = nil,
         beforeRead: @escaping @Sendable (Int32) -> Void = { _ in }) {
        let listener = socket(AF_INET, SOCK_STREAM, 0)
        precondition(listener >= 0)
        if var size = receiveBuffer {
            precondition(setsockopt(listener, SOL_SOCKET, SO_RCVBUF, &size,
                                   socklen_t(MemoryLayout<Int32>.size)) == 0)
        }
        var address = sockaddr_in()
        address.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        address.sin_family = sa_family_t(AF_INET)
        address.sin_addr.s_addr = inet_addr("127.0.0.1")
        precondition(withUnsafePointer(to: &address) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.bind(listener, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        } == 0)
        var length = socklen_t(MemoryLayout<sockaddr_in>.size)
        precondition(withUnsafeMutablePointer(to: &address) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                getsockname(listener, $0, &length)
            }
        } == 0)
        port = UInt16(bigEndian: address.sin_port)
        precondition(port != 33743 && port > 0)
        precondition(listen(listener, 1) == 0)
        DispatchQueue.global(qos: .userInitiated).async { [self] in
            let peer = accept(listener, nil, nil)
            Darwin.close(listener)
            precondition(peer >= 0)
            defer { Darwin.close(peer); done.signal() }
            precondition(fcntl(peer, F_SETNOSIGPIPE, 1) == 0)
            var timeout = timeval(tv_sec: 3, tv_usec: 0)
            precondition(setsockopt(peer, SOL_SOCKET, SO_RCVTIMEO, &timeout,
                                   socklen_t(MemoryLayout<timeval>.size)) == 0)
            if fragmented {
                for byte in greeting {
                    if !Self.send([byte], on: peer) { break }
                    usleep(1_000)
                }
            } else { _ = Self.send(greeting, on: peer) }
            beforeRead(peer)
            var buffer = [UInt8](repeating: 0, count: 16_384)
            while true {
                let count = Darwin.read(peer, &buffer, buffer.count)
                if count == 0 { eof = true; break }
                if count < 0 {
                    if errno == EINTR { continue }
                    readError = errno; break
                }
                bytes.append(contentsOf: buffer.prefix(count))
                precondition(bytes.count < 16_000_000, "test receive bound")
            }
        }
    }

    func finish(allowReset: Bool = false) {
        precondition(done.wait(timeout: .now() + 5) == .success, "fake peer finishes")
        precondition(readError == 0 || (allowReset && readError == ECONNRESET),
                     "fake peer read failed: \(readError)")
    }
    private static func send(_ bytes: [UInt8], on fd: Int32) -> Bool {
        var offset = 0
        while offset < bytes.count {
            let count = bytes.withUnsafeBytes {
                Darwin.write(fd, $0.baseAddress!.advanced(by: offset), bytes.count - offset)
            }
            if count < 0 && errno == EINTR { continue }
            guard count > 0 else { return false }
            offset += count
        }
        return true
    }
}

@main
struct DualSenseRelaySocketTests {
    static func main() throws {
        var checks = 0
        func check(_ value: Bool, _ label: String) {
            checks += 1; precondition(value, label)
        }
        let frame = [UInt8](repeating: 0x5a, count: 49)

        do {
            let server = FakeRelay(fragmented: true)
            let clock = TestClock()
            let socket = try DualSenseRelaySocket(port: server.port, clock: { clock.read() })
            check(socket.isOpen && socket.write(frame), "fragmented greeting then frame")
            socket.close(); socket.close(); socket.startWatchdog()
            check(!socket.isOpen && !socket.write(frame), "close is final and idempotent")
            server.finish()
            check(server.eof && server.bytes == frame, "exact frame followed by EOF")
        }

        for greeting in [Array("wrong\n".utf8), Array(FakeRelay.greeting.prefix(4))] {
            let server = FakeRelay(greeting: greeting, beforeRead: { peer in
                _ = shutdown(peer, SHUT_WR)
            })
            var rejected = false
            do { let socket = try DualSenseRelaySocket(port: server.port); socket.close() }
            catch let error as POSIXError { rejected = error.code == .EPROTO }
            check(rejected, "wrong or truncated greeting rejected")
            server.finish(allowReset: true)
            check((server.eof || server.readError == ECONNRESET) && server.bytes.isEmpty,
                  "rejected peer receives no input")
        }

        do {
            let server = FakeRelay(greeting: [])
            let started = DispatchTime.now().uptimeNanoseconds
            var timedOut = false
            do { let socket = try DualSenseRelaySocket(port: server.port); socket.close() }
            catch let error as POSIXError { timedOut = error.code == .ETIMEDOUT }
            check(timedOut, "silent greeting times out")
            check(DispatchTime.now().uptimeNanoseconds - started < 2_000_000_000,
                  "greeting wait is bounded")
            server.finish()
            check(server.eof && server.bytes.isEmpty, "timeout closes candidate descriptor")
        }

        for size in [0, 48, 50] {
            let server = FakeRelay()
            let socket = try DualSenseRelaySocket(port: server.port)
            check(!socket.write([UInt8](repeating: 1, count: size)) && !socket.isOpen,
                  "invalid frame size closes session")
            server.finish()
            check(server.eof && server.bytes.isEmpty, "invalid frame sends no bytes")
        }

        for extraByte in [false, true] {
            let ready = DispatchSemaphore(value: 0)
            let greeting = FakeRelay.greeting + (extraByte ? [UInt8(0xff)] : [])
            let server = FakeRelay(greeting: greeting, beforeRead: { peer in
                if !extraByte { _ = shutdown(peer, SHUT_WR) }
                ready.signal()
            })
            let socket = try DualSenseRelaySocket(port: server.port)
            precondition(ready.wait(timeout: .now() + 2) == .success)
            check(!socket.write(frame) && !socket.isOpen, "EOF or unexpected reply fails closed")
            server.finish(allowReset: true)
            check((server.eof || server.readError == ECONNRESET) && server.bytes.isEmpty,
                  "unhealthy peer gets no input frame")
        }

        do {
            let clock = TestClock()
            let server = FakeRelay()
            let socket = try DualSenseRelaySocket(port: server.port, clock: { clock.read() })
            clock.set(249_999_999); socket.check()
            check(socket.isOpen && socket.write(frame), "write before deadline renews heartbeat")
            clock.set(499_999_998); socket.check()
            check(socket.isOpen, "renewed heartbeat before deadline")
            clock.set(499_999_999); socket.check()
            check(!socket.isOpen, "250 ms exact deadline closes")
            server.finish()
            check(server.eof && server.bytes == frame, "watchdog close preserves prior frame then EOF")
        }

        for timestamp: UInt64 in [250_000_000, 1_000_000_000] {
            let clock = TestClock()
            let server = FakeRelay()
            let socket = try DualSenseRelaySocket(port: server.port, clock: { clock.read() })
            clock.set(timestamp)
            check(!socket.write(frame) && !socket.isOpen, "late write cannot revive stale session")
            server.finish()
            check(server.eof && server.bytes.isEmpty, "late frame never reaches peer")
        }

        do {
            let clock = TestClock(UInt64.max - 100)
            let server = FakeRelay()
            let socket = try DualSenseRelaySocket(port: server.port, clock: { clock.read() })
            clock.set(.max); socket.check()
            check(socket.isOpen, "near maximum clock does not overflow")
            clock.set(0); socket.check()
            check(!socket.isOpen, "regressed clock fails closed without underflow")
            server.finish()
            check(server.eof, "regressed clock ends peer session")
        }

        do {
            let server = FakeRelay()
            let socket = try DualSenseRelaySocket(port: server.port)
            socket.startWatchdog(); socket.startWatchdog()
            let started = DispatchTime.now().uptimeNanoseconds
            // Deliberately block main; watchdog has its own dispatch queue.
            server.finish()
            check(!socket.isOpen && server.eof, "watchdog closes while main thread blocked")
            let elapsed = DispatchTime.now().uptimeNanoseconds - started
            check(elapsed >= 200_000_000 && elapsed < 1_000_000_000, "watchdog deadline is bounded")
        }

        do {
            let server = FakeRelay()
            var socket: DualSenseRelaySocket? = try DualSenseRelaySocket(port: server.port)
            weak let reference = socket
            socket?.startWatchdog(); socket = nil
            server.finish()
            check(reference == nil && server.eof, "watchdog does not retain owner or leak descriptor")
        }

        do {
            let server = FakeRelay()
            let clock = TestClock()
            let socket = try DualSenseRelaySocket(port: server.port, clock: { clock.read() })
            DispatchQueue.concurrentPerform(iterations: 16) { index in
                precondition(socket.write([UInt8](repeating: UInt8(index), count: 49)))
            }
            socket.close(); server.finish()
            check(server.bytes.count == 16 * 49 && server.eof, "concurrent frames arrive before EOF")
            var values = Set<UInt8>()
            for offset in stride(from: 0, to: server.bytes.count, by: 49) {
                let chunk = server.bytes[offset..<(offset + 49)]
                check(Set(chunk).count == 1, "concurrent frame not interleaved")
                values.insert(chunk.first!)
            }
            check(values == Set(UInt8(0)..<UInt8(16)), "each concurrent frame delivered once")
        }

        do {
            let drain = DispatchSemaphore(value: 0)
            let server = FakeRelay(receiveBuffer: 1024, beforeRead: { _ in
                precondition(drain.wait(timeout: .now() + 5) == .success)
            })
            let clock = TestClock()
            let socket = try DualSenseRelaySocket(port: server.port, clock: { clock.read() })
            let started = DispatchTime.now().uptimeNanoseconds
            var accepted = 0
            while accepted < 100_000 && socket.write(frame) { accepted += 1 }
            let elapsed = DispatchTime.now().uptimeNanoseconds - started
            check(accepted > 0 && accepted < 100_000 && !socket.isOpen,
                  "backpressure terminates session without retry or blocking")
            check(elapsed < 2_000_000_000, "backpressure write duration is bounded")
            check(!socket.write(frame), "backpressure session cannot resume")
            drain.signal(); server.finish()
            check(server.eof && server.bytes.allSatisfy { $0 == 0x5a }, "backpressure ends in EOF")
            check(server.bytes.count >= accepted * 49 && server.bytes.count < (accepted + 1) * 49,
                  "no retry appends another complete frame after failure")
            print("Backpressure: accepted=\(accepted), received=\(server.bytes.count), partial=\(server.bytes.count % 49), elapsedNs=\(elapsed)")
        }
        print("PASS: \(checks) DualSenseRelaySocket checks (ephemeral loopback only; port 33743 unused)")
    }
}
