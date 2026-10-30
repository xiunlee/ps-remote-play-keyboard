// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import Foundation
import Darwin

/// Owns one bounded loopback relay session. No automatic reconnect or queued input.
final class DualSenseRelaySocket: @unchecked Sendable {
    private let lock = NSLock()
    private var fd: Int32 = -1
    private var lastPulse = DispatchTime.now().uptimeNanoseconds
    private let clock: @Sendable () -> UInt64
    private var watchdog: DispatchSourceTimer?
    private let queue = DispatchQueue(label: "pskeyboard.dualsense-watchdog", qos: .userInitiated)

    init(port: UInt16 = 33743,
         clock: @escaping @Sendable () -> UInt64 = { DispatchTime.now().uptimeNanoseconds }) throws {
        self.clock = clock
        let candidate = socket(AF_INET, SOCK_STREAM, 0)
        guard candidate >= 0 else { throw POSIXError(.EIO) }
        var owned = false
        defer { if !owned { Darwin.close(candidate) } }
        guard fcntl(candidate, F_SETFL, O_NONBLOCK) == 0,
              fcntl(candidate, F_SETFD, FD_CLOEXEC) == 0,
              fcntl(candidate, F_SETNOSIGPIPE, 1) == 0 else { throw POSIXError(.EIO) }
        var noDelay: Int32 = 1
        guard setsockopt(candidate, IPPROTO_TCP, TCP_NODELAY, &noDelay,
                         socklen_t(MemoryLayout<Int32>.size)) == 0 else { throw POSIXError(.EIO) }
        var address = sockaddr_in()
        address.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        address.sin_family = sa_family_t(AF_INET)
        address.sin_port = port.bigEndian
        address.sin_addr.s_addr = inet_addr("127.0.0.1")
        let result = withUnsafePointer(to: &address) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.connect(candidate, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        if result != 0 {
            guard errno == EINPROGRESS else { throw POSIXError(.ECONNREFUSED) }
            var pollFD = pollfd(fd: candidate, events: Int16(POLLOUT), revents: 0)
            guard poll(&pollFD, 1, 1000) == 1 else { throw POSIXError(.ETIMEDOUT) }
            var error: Int32 = 0
            var length = socklen_t(MemoryLayout<Int32>.size)
            guard getsockopt(candidate, SOL_SOCKET, SO_ERROR, &error, &length) == 0, error == 0 else {
                throw POSIXError(.ECONNREFUSED)
            }
        }
        let expected = Array("PSKeyboard-DualSenseV5-Relay/1\n".utf8)
        var greeting: [UInt8] = []
        let deadline = DispatchTime.now().uptimeNanoseconds + 1_000_000_000
        while greeting.count < expected.count {
            guard DispatchTime.now().uptimeNanoseconds < deadline else { throw POSIXError(.ETIMEDOUT) }
            var pollFD = pollfd(fd: candidate, events: Int16(POLLIN), revents: 0)
            let ready = poll(&pollFD, 1, 20)
            if ready < 0 && errno == EINTR { continue }
            guard ready >= 0 else { throw POSIXError(.EIO) }
            if ready == 0 { continue }
            var byte: UInt8 = 0
            guard recv(candidate, &byte, 1, 0) == 1, byte == expected[greeting.count] else {
                throw POSIXError(.EPROTO)
            }
            greeting.append(byte)
        }
        fd = candidate; owned = true
        lastPulse = clock()
    }
    deinit { close() }

    var isOpen: Bool {
        lock.lock(); defer { lock.unlock() }
        return fd >= 0
    }
    func startWatchdog() {
        lock.lock(); defer { lock.unlock() }
        guard fd >= 0, watchdog == nil else { return }
        let timer = DispatchSource.makeTimerSource(queue: queue)
        timer.schedule(deadline: .now() + .milliseconds(50), repeating: .milliseconds(50))
        timer.setEventHandler { [weak self] in self?.check() }
        watchdog = timer; timer.resume()
    }
    func check() {
        lock.lock(); defer { lock.unlock() }
        if stale(at: clock()) { closeLocked() }
    }
    /// Frames are never retried: any partial write ends this session.
    func write(_ frame: [UInt8]) -> Bool {
        lock.lock(); defer { lock.unlock() }
        guard fd >= 0, frame.count == 49 else { closeLocked(); return false }
        let sample = clock()
        // A delayed timer must not let a late main-thread write revive stale input.
        guard !stale(at: sample) else { closeLocked(); return false }
        var byte: UInt8 = 0
        let peek = recv(fd, &byte, 1, MSG_PEEK | MSG_DONTWAIT)
        guard peek < 0 && (errno == EAGAIN || errno == EWOULDBLOCK || errno == EINTR) else {
            closeLocked(); return false
        }
        let count = frame.withUnsafeBytes { Darwin.write(fd, $0.baseAddress, $0.count) }
        guard count == frame.count else { closeLocked(); return false }
        lastPulse = sample
        return true
    }
    func close() {
        lock.lock(); defer { lock.unlock() }
        closeLocked()
    }
    private func stale(at sample: UInt64) -> Bool {
        sample < lastPulse || sample - lastPulse >= 250_000_000
    }
    private func closeLocked() {
        watchdog?.cancel(); watchdog = nil
        if fd >= 0 { _ = shutdown(fd, SHUT_RDWR); Darwin.close(fd); fd = -1 }
    }
}
