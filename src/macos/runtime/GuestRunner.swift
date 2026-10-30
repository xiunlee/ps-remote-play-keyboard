// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Owned diskless runtime guest. No physical USB, HID input, Sony or external networking.
import Foundation
import Virtualization
import Darwin

private func note(_ message: String) {
    let bytes = Data(("[psusb-guest] " + message + "\n").utf8)
    _ = bytes.withUnsafeBytes { Darwin.write(STDERR_FILENO,$0.baseAddress,$0.count) }
}

private final class ForwardedConnection {
    let tcp: Int32
    let peer: Int32
    let connection: VZVirtioSocketConnection
    let lock = NSLock()
    var stopping = false

    init?(tcp: Int32, connection: VZVirtioSocketConnection) {
        self.tcp = tcp; self.connection = connection
        peer = dup(connection.fileDescriptor)
        guard peer >= 0 else { return nil }
        for fd in [tcp, peer] {
            // Darwin accept inherits O_NONBLOCK from the listener. These
            // bounded pump threads use blocking I/O and kernel backpressure.
            let flags = fcntl(fd, F_GETFL)
            guard flags >= 0,
                  fcntl(fd, F_SETFL, flags & ~O_NONBLOCK) == 0,
                  fcntl(fd, F_SETNOSIGPIPE, 1) == 0,
                  fcntl(fd, F_SETFD, FD_CLOEXEC) == 0 else {
                close(peer); return nil
            }
        }
    }
    func stop() {
        lock.lock(); defer { lock.unlock() }
        if stopping { return }
        stopping = true
        // Interrupt both pumps, but do not close/reuse either fd while a pump owns it.
        _ = shutdown(tcp, SHUT_RDWR); _ = shutdown(peer, SHUT_RDWR)
    }
    func start(completed: @escaping () -> Void) {
        let group = DispatchGroup()
        for (source, destination) in [(tcp, peer), (peer, tcp)] {
            group.enter()
            DispatchQueue.global(qos: .userInitiated).async {
                defer { group.leave() }
                var bytes = [UInt8](repeating: 0, count: 16384)
                while true {
                    let count = read(source, &bytes, bytes.count)
                    if count < 0 && errno == EINTR { continue }
                    if count == 0 {
                        // A peer may finish sending its request and still await a response.
                        // Preserve the opposite pump until it also reaches EOF.
                        if shutdown(destination, SHUT_WR) != 0 { self.stop() }
                        return
                    }
                    if count < 0 { self.stop(); return }
                    var offset = 0
                    while offset < count {
                        let written = bytes.withUnsafeBytes {
                            write(destination, $0.baseAddress!.advanced(by: offset), count - offset)
                        }
                        if written < 0 && errno == EINTR { continue }
                        if written <= 0 { self.stop(); return }
                        offset += written
                    }
                }
            }
        }
        group.notify(queue: .main) {
            close(self.tcp); close(self.peer); self.connection.close()
            completed()
        }
    }
}

private final class Guest: NSObject, VZVirtualMachineDelegate {
    var machine: VZVirtualMachine!
    var listeners: [DispatchSourceRead] = []
    var signals: [DispatchSourceSignal] = []
    var bridges: [UUID: ForwardedConnection] = [:]
    var pendingConnections = 0
    var stopping = false
    var requestedExitCode: Int32 = 0
    var originalTTY: termios?
    var activity: NSObjectProtocol?
    let serialInput = Pipe()

    func run(kernel: URL, initrd: URL, share: URL) throws {
        let loader = VZLinuxBootLoader(kernelURL: kernel)
        loader.initialRamdiskURL = initrd
        loader.commandLine = "console=hvc0 rdinit=/init panic=-1 loglevel=5"
        let config = VZVirtualMachineConfiguration()
        config.bootLoader = loader; config.cpuCount = 2; config.memorySize = 1024 * 1024 * 1024
        config.entropyDevices = [VZVirtioEntropyDeviceConfiguration()]
        config.socketDevices = [VZVirtioSocketDeviceConfiguration()]
        let serial = VZVirtioConsoleDeviceSerialPortConfiguration()
        serial.attachment = VZFileHandleSerialPortAttachment(fileHandleForReading: serialInput.fileHandleForReading,
                                                           fileHandleForWriting: .standardOutput)
        config.serialPorts = [serial]
        let shared = VZVirtioFileSystemDeviceConfiguration(tag: "psusb-spike")
        shared.share = VZSingleDirectoryShare(directory: VZSharedDirectory(url: share, readOnly: true))
        config.directorySharingDevices = [shared]
        // No NIC, storage disk or physical USB controller is assigned.
        try config.validate()
        machine = VZVirtualMachine(configuration: config)
        machine.delegate = self
        if isatty(STDIN_FILENO) != 0 {
            var state = termios()
            if tcgetattr(STDIN_FILENO, &state) == 0 {
                originalTTY = state; cfmakeraw(&state); _ = tcsetattr(STDIN_FILENO, TCSANOW, &state)
            }
        }
        activity = ProcessInfo.processInfo.beginActivity(options: .userInitiatedAllowingIdleSystemSleep,
                                                          reason: "PSKeyboard controller services")
        for code in [SIGINT, SIGTERM] {
            signal(code, SIG_IGN)
            let source = DispatchSource.makeSignalSource(signal: code, queue: .main)
            source.setEventHandler { self.stop() }; source.resume(); signals.append(source)
        }
        // EOF from the supervisor is a lease: even a crashed supervisor cannot
        // leave this VM running after its owned stdin pipe closes.
        Thread {
            var bytes = [UInt8](repeating: 0, count: 8192)
            while true {
                let count = read(STDIN_FILENO, &bytes, bytes.count)
                if count < 0 && errno == EINTR { continue }
                if count <= 0 { break }
                do { try self.serialInput.fileHandleForWriting.write(contentsOf: Data(bytes.prefix(count))) }
                catch { break }
            }
            try? self.serialInput.fileHandleForWriting.close()
            DispatchQueue.main.async { self.stop() }
        }.start()
        machine.start { result in
            switch result {
            case .success:
                note("running; 2 CPU, 1 GiB RAM, no NIC or disk; read-only shared directory")
                do { try self.listen(hostPort: 33742, guestPort: 33642)
                     try self.listen(hostPort: 7575, guestPort: 7575) }
                catch { note("listener failed: \(error)"); self.stop(exitCode: 1) }
            case .failure(let error): note("start failed: \(error)"); self.finish(1)
            }
        }
    }
    func listen(hostPort: UInt16, guestPort: UInt32) throws {
        let fd = socket(AF_INET, SOCK_STREAM, 0)
        guard fd >= 0 else { throw POSIXError(.EIO) }
        var yes: Int32 = 1
        _ = setsockopt(fd, SOL_SOCKET, SO_REUSEADDR, &yes, socklen_t(MemoryLayout.size(ofValue: yes)))
        _ = fcntl(fd, F_SETFL, O_NONBLOCK); _ = fcntl(fd, F_SETFD, FD_CLOEXEC)
        var address = sockaddr_in()
        address.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        address.sin_family = sa_family_t(AF_INET); address.sin_port = hostPort.bigEndian
        address.sin_addr.s_addr = inet_addr("127.0.0.1")
        let addressLength = socklen_t(MemoryLayout<sockaddr_in>.size)
        let bound = withUnsafePointer(to: &address) { p in
            p.withMemoryRebound(to: sockaddr.self, capacity: 1) { Darwin.bind(fd, $0, addressLength) }
        }
        guard bound == 0, Darwin.listen(fd, 8) == 0 else {
            let code = errno; close(fd); throw POSIXError(POSIXErrorCode(rawValue: code) ?? .EIO)
        }
        let source = DispatchSource.makeReadSource(fileDescriptor: fd, queue: .main)
        source.setCancelHandler { close(fd) }
        source.setEventHandler {
            while true {
                let client = accept(fd, nil, nil)
                if client < 0 { return }
                guard !self.stopping, self.bridges.count + self.pendingConnections < 8,
                      let socketDevice = self.machine.socketDevices.first as? VZVirtioSocketDevice else {
                    close(client); continue
                }
                self.pendingConnections += 1
                socketDevice.connect(toPort: guestPort) { result in
                    self.pendingConnections -= 1
                    switch result {
                    case .success(let connection):
                        guard !self.stopping, let bridge = ForwardedConnection(tcp: client, connection: connection) else {
                            close(client); connection.close(); return
                        }
                        let id = UUID(); self.bridges[id] = bridge
                        bridge.start { self.bridges.removeValue(forKey: id) }
                    case .failure(let error): close(client); note("vsock \(guestPort) connection failed: \(error)")
                    }
                }
            }
        }
        source.resume(); listeners.append(source)
        note("loopback TCP \(hostPort) -> guest vsock \(guestPort)")
    }
    func stop(exitCode: Int32 = 0) {
        if exitCode != 0 { requestedExitCode = exitCode }
        guard !stopping else { return }; stopping = true
        listeners.forEach { $0.cancel() }; bridges.values.forEach { $0.stop() }
        if machine.canRequestStop { try? machine.requestStop() }
        DispatchQueue.main.asyncAfter(deadline: .now() + 5) {
            if self.machine.canStop {
                self.machine.stop { error in
                    if let error { note("stop: \(error)") }
                    self.finish(error == nil ? 0 : 1)
                }
            } else { self.finish(0) }
        }
    }
    func finish(_ code: Int32) -> Never {
        listeners.forEach { $0.cancel() }; bridges.values.forEach { $0.stop() }
        if let activity { ProcessInfo.processInfo.endActivity(activity) }
        if var state = originalTTY { _ = tcsetattr(STDIN_FILENO, TCSANOW, &state) }
        note("guest ended"); exit(code == 0 ? requestedExitCode : code)
    }
    func guestDidStop(_ virtualMachine: VZVirtualMachine) { finish(0) }
    func virtualMachine(_ virtualMachine: VZVirtualMachine, didStopWithError error: Error) {
        note("guest error: \(error)"); finish(1)
    }
}

@main
private struct Main {
    static func main() {
        signal(SIGPIPE,SIG_IGN)
        guard CommandLine.arguments.count == 4 else {
            note("usage: VZUSBGuest KERNEL INITRD READ_ONLY_SHARE_DIRECTORY"); exit(64)
        }
        let guest = Guest()
        do { try guest.run(kernel: URL(fileURLWithPath: CommandLine.arguments[1]),
                           initrd: URL(fileURLWithPath: CommandLine.arguments[2]),
                           share: URL(fileURLWithPath: CommandLine.arguments[3])) }
        catch { note("configuration failed: \(error)"); exit(1) }
        RunLoop.main.run()
        withExtendedLifetime(guest) {}
    }
}
