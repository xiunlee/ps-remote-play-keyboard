// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import Foundation
import CoreFoundation
import Darwin

final class RuntimeStopFlag: @unchecked Sendable {
    private let lock = NSLock()
    private var stopped = false
    var requested: Bool { lock.lock(); defer { lock.unlock() }; return stopped }
    func request() { lock.lock(); stopped = true; lock.unlock() }
}

enum RuntimeSocket {
    static var now: Double { Double(DispatchTime.now().uptimeNanoseconds) / 1_000_000_000 }
    static func configure(_ fd: Int32) throws {
        guard fcntl(fd, F_SETFL, O_NONBLOCK) == 0, fcntl(fd, F_SETFD, FD_CLOEXEC) == 0,
              fcntl(fd, F_SETNOSIGPIPE, 1) == 0 else { throw RuntimeFailure("socket_configuration") }
        var yes: Int32 = 1
        _ = setsockopt(fd, IPPROTO_TCP, TCP_NODELAY, &yes, socklen_t(MemoryLayout<Int32>.size))
    }
    static func address(_ port: UInt16) -> sockaddr_in {
        var value = sockaddr_in(); value.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        value.sin_family = sa_family_t(AF_INET); value.sin_port = port.bigEndian
        value.sin_addr.s_addr = inet_addr("127.0.0.1"); return value
    }
    static func connect(_ port: UInt16, timeout: Double = 1.5) throws -> Int32 {
        let fd = socket(AF_INET, SOCK_STREAM, 0)
        guard fd >= 0 else { throw RuntimeFailure("socket_create") }
        do {
            try configure(fd)
            var value = address(port)
            let code = withUnsafePointer(to: &value) { pointer in
                pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { Darwin.connect(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size)) }
            }
            if code != 0 {
                guard errno == EINPROGRESS else { throw RuntimeFailure("api_unavailable") }
                try wait(fd, events: Int16(POLLOUT), deadline: now + timeout)
                var failure: Int32 = 0; var size = socklen_t(MemoryLayout<Int32>.size)
                guard getsockopt(fd, SOL_SOCKET, SO_ERROR, &failure, &size) == 0, failure == 0 else { throw RuntimeFailure("api_unavailable") }
            }
            return fd
        } catch { close(fd); throw error }
    }
    static func listen(_ port: UInt16) throws -> Int32 {
        let fd = socket(AF_INET, SOCK_STREAM, 0)
        guard fd >= 0 else { throw RuntimeFailure("socket_create") }
        do {
            try configure(fd)
            var reuse: Int32 = 1
            guard setsockopt(fd,SOL_SOCKET,SO_REUSEADDR,&reuse,socklen_t(MemoryLayout<Int32>.size)) == 0 else { throw RuntimeFailure("socket_configuration") }
            var value = address(port)
            let code = withUnsafePointer(to: &value) { pointer in
                pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { Darwin.bind(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size)) }
            }
            guard code == 0, Darwin.listen(fd, 1) == 0 else { throw RuntimeFailure("port_in_use") }
            return fd
        } catch { close(fd); throw error }
    }
    static func wait(_ fd: Int32, events: Int16, deadline: Double) throws {
        while now < deadline {
            var value = pollfd(fd: fd, events: events, revents: 0)
            let ready = poll(&value, 1, Int32(max(1, min(50, (deadline - now) * 1000))))
            if ready < 0 && errno == EINTR { continue }
            guard ready >= 0 else { throw RuntimeFailure("socket_poll") }
            if ready > 0 { return }
        }
        throw RuntimeFailure("socket_deadline")
    }
    static func sendComplete(_ fd: Int32, _ bytes: [UInt8]) throws {
        let count = bytes.withUnsafeBytes { Darwin.write(fd, $0.baseAddress, $0.count) }
        guard count == bytes.count else { throw RuntimeFailure("partial_write") }
    }
    static func request(_ command: String, port: UInt16 = 33742) throws -> [String: Any] {
        let deadline = now + 1.5
        let fd = try connect(port); defer { close(fd) }
        try sendComplete(fd, Array(command.utf8) + [0])
        var response = Data()
        while true {
            try wait(fd, events: Int16(POLLIN), deadline: deadline)
            var block = [UInt8](repeating: 0, count: 4096)
            let count = read(fd, &block, block.count)
            if count == 0 { break }
            if count < 0 && [EINTR,EAGAIN,EWOULDBLOCK].contains(errno) { continue }
            guard count > 0, response.count + count <= 16384 else { throw RuntimeFailure("api_response") }
            response.append(contentsOf: block.prefix(count))
        }
        guard let object = try? JSONSerialization.jsonObject(with: response) as? [String:Any] else { throw RuntimeFailure("api_json") }
        if let status = integer(object["status"]) { throw RuntimeFailure("api_status_\(status)") }
        return object
    }
    static func integer(_ object: Any?) -> UInt32? {
        guard let number = object as? NSNumber, CFGetTypeID(number) != CFBooleanGetTypeID(),
              number.doubleValue > 0, number.doubleValue <= Double(UInt32.max),
              number.doubleValue == Double(number.uint32Value) else { return nil }
        return number.uint32Value
    }
    static func validatePing(_ object: [String:Any]) throws {
        let text: [String:String] = ["server":"PSKeyboard-DualSenseV5-GuestCandidate", "version":"research.guest.1",
            "labMarker":"pskeyboard-dualsense-v5-guest", "sourceCommit":RuntimeWire.commit,
            "apiAddress":"127.0.0.1:33642", "usbAddress":"127.0.0.1:33641"]
        for (key,value) in text { guard object[key] as? String == value else { throw RuntimeFailure("guest_identity_mismatch") } }
        for (key,value) in ["autoAttach":false,"hostAutoAttach":false,"loopbackOnly":true,"stdinEofStops":true,"sessionLifetimeLimit":false] {
            guard let number = object[key] as? NSNumber, CFGetTypeID(number) == CFBooleanGetTypeID(), number.boolValue == value else { throw RuntimeFailure("guest_identity_mismatch") }
        }
    }
}

/// One owned bus and stream, with a watchdog independent of the App's UI thread.
final class NativeRelay: @unchecked Sendable {
    private let apiPort: UInt16
    private let frontendPort: UInt16
    private let stop = RuntimeStopFlag()
    private let lock = NSLock()
    private var result: String?
    private var cleaned = false
    private var listener: Int32 = -1
    private var owner: Int32 = -1
    private var frontend: Int32 = -1
    private var creationUncertain = false
    private(set) var busID: UInt32?
    private(set) var deviceID: UInt32?
    var outcome: String? { lock.lock(); defer { lock.unlock() }; return result }
    var cleanupConfirmed: Bool { lock.lock(); defer { lock.unlock() }; return cleaned }
    init(apiPort: UInt16 = 33742, frontendPort: UInt16 = 33743) { self.apiPort = apiPort; self.frontendPort = frontendPort }
    func start() throws {
        do {
            listener = try RuntimeSocket.listen(frontendPort)
            try RuntimeSocket.validatePing(RuntimeSocket.request("ping", port: apiPort))
            creationUncertain = true
            guard let bus = RuntimeSocket.integer(try RuntimeSocket.request("bus/create", port: apiPort)["busId"]) else { throw RuntimeFailure("invalid_bus") }
            busID = bus
            creationUncertain = false
            let added = try RuntimeSocket.request("bus/\(bus)/add {\"type\":\"dualsensegamepadv5\"}", port: apiPort)
            guard RuntimeSocket.integer(added["busId"]) == bus,
                  added["type"] as? String == "dualsensegamepadv5", let string = added["devId"] as? String,
                  string.range(of: "^[1-9][0-9]*$", options: .regularExpression) != nil,
                  let device = UInt32(string) else { throw RuntimeFailure("invalid_device") }
            deviceID = device
            owner = try RuntimeSocket.connect(apiPort)
            try RuntimeSocket.sendComplete(owner, Array("bus/\(bus)/\(device)".utf8) + [0])
            Thread { self.run() }.start()
        } catch { finish(String(describing:error)); throw error }
    }
    func requestStop() { stop.request() }
    func waitForStop(seconds: Double = 3) -> Bool {
        let deadline = RuntimeSocket.now + seconds
        while outcome == nil && RuntimeSocket.now < deadline { Thread.sleep(forTimeInterval:0.01) }
        return outcome != nil && cleanupConfirmed
    }
    private func run() {
        var clock = RuntimeFeedClock(now: RuntimeSocket.now)
        var inputParser = RuntimeFrameParser(); var feedbackParser = RuntimeFrameParser(feedback:true)
        do {
            while !stop.requested {
                let now = RuntimeSocket.now
                try inputParser.checkDeadline(now); try feedbackParser.checkDeadline(now)
                if let packet = try clock.tick(now) { try RuntimeSocket.sendComplete(owner,packet) }
                var polls = [pollfd(fd:owner,events:Int16(POLLIN),revents:0),
                             pollfd(fd:listener,events:Int16(POLLIN),revents:0),
                             pollfd(fd:frontend,events:Int16(POLLIN),revents:0)]
                let ready = poll(&polls,nfds_t(polls.count),4)
                if ready < 0 && errno == EINTR { continue }
                guard ready >= 0 else { throw RuntimeFailure("relay_poll") }
                if polls[1].revents != 0 && listener >= 0 {
                    var peer = sockaddr_in(); var size = socklen_t(MemoryLayout<sockaddr_in>.size)
                    let accepted = withUnsafeMutablePointer(to:&peer) { p in p.withMemoryRebound(to:sockaddr.self,capacity:1) { accept(listener,$0,&size) } }
                    guard accepted >= 0 else { throw RuntimeFailure("frontend_accept") }
                    frontend = accepted
                    guard peer.sin_addr.s_addr == inet_addr("127.0.0.1") else { throw RuntimeFailure("non_loopback_frontend") }
                    try RuntimeSocket.configure(frontend); close(listener); listener = -1
                    try RuntimeSocket.sendComplete(frontend,RuntimeWire.greeting)
                    try clock.connect(at:RuntimeSocket.now)
                }
                for index in [0,2] where polls[index].revents != 0 && polls[index].fd >= 0 {
                    var bytes = [UInt8](repeating:0,count:index == 0 ? 8192 : 4096)
                    let count = read(polls[index].fd,&bytes,bytes.count)
                    if count < 0 && [EINTR,EAGAIN,EWOULDBLOCK].contains(errno) { continue }
                    guard count >= 0 else { throw RuntimeFailure("relay_read") }
                    let block = Array(bytes.prefix(count))
                    if index == 0 { _ = try feedbackParser.feed(block,at:RuntimeSocket.now) }
                    else { for payload in try inputParser.feed(block,at:RuntimeSocket.now) { try clock.input(payload,at:RuntimeSocket.now) } }
                }
            }
            finish("stopped")
        } catch { finish(String(describing:error)) }
    }
    private func finish(_ reason: String) {
        for fd in [frontend,owner,listener] where fd >= 0 { _ = shutdown(fd,SHUT_RDWR); close(fd) }
        frontend = -1; owner = -1; listener = -1
        var ok = busID == nil && !creationUncertain
        if let busID {
            do { ok = RuntimeSocket.integer(try RuntimeSocket.request("bus/remove \(busID)",port:apiPort)["busId"]) == busID }
            catch let error as RuntimeFailure { ok = error.code == "api_status_404" }
            catch { ok = false }
        }
        lock.lock(); cleaned = ok; result = reason; lock.unlock()
    }
}
