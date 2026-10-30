// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import Foundation
import Darwin
import IOKit

final class OwnedProcess {
    let process = Process()
    let input = Pipe()
    let output = Pipe()
    private var pending = Data()
    init(executable: URL, arguments: [String], inputEnabled: Bool = false) throws {
        process.executableURL = executable; process.arguments = arguments
        process.standardInput = inputEnabled ? input : FileHandle.nullDevice
        process.standardOutput = output; process.standardError = output
        try process.run()
        try? output.fileHandleForWriting.close()
        if inputEnabled { try? input.fileHandleForReading.close() }
        let fd = output.fileHandleForReading.fileDescriptor
        _ = fcntl(fd,F_SETFL,O_NONBLOCK); _ = fcntl(fd,F_SETFD,FD_CLOEXEC)
    }
    var running: Bool { process.isRunning }
    func write(_ string: String) throws { try input.fileHandleForWriting.write(contentsOf:Data(string.utf8)) }
    func readAvailable() throws -> Data {
        var result = Data(); var buffer = [UInt8](repeating:0,count:8192)
        while true {
            let count = read(output.fileHandleForReading.fileDescriptor,&buffer,buffer.count)
            if count < 0 && errno == EINTR { continue }
            if count < 0 && [EAGAIN,EWOULDBLOCK].contains(errno) { break }
            guard count >= 0 else { throw RuntimeFailure("process_output_read") }
            if count == 0 { break }
            result.append(contentsOf:buffer.prefix(count))
            guard result.count <= 1_048_576 else { throw RuntimeFailure("process_output_limit") }
        }
        return result
    }
    func waitFor(_ marker: String, seconds: Double, stop: RuntimeStopFlag?) throws -> String {
        let deadline = RuntimeSocket.now + seconds
        while RuntimeSocket.now < deadline {
            if stop?.requested == true { throw RuntimeFailure("cancelled") }
            pending.append(try readAvailable())
            guard pending.count <= 1_048_576 else { throw RuntimeFailure("process_output_limit") }
            if let range = pending.range(of:Data(marker.utf8)) {
                let value = String(decoding:pending[..<range.upperBound],as:UTF8.self)
                pending.removeSubrange(..<range.upperBound)
                return value
            }
            guard running else { throw RuntimeFailure("process_ended") }
            Thread.sleep(forTimeInterval:0.02)
        }
        throw RuntimeFailure("process_deadline")
    }
    func command(_ script: String, seconds: Double = 8, stop: RuntimeStopFlag? = nil) throws -> String {
        let token = UUID().uuidString.replacingOccurrences(of:"-",with:"")
        let begin = "PSKB_BEGIN_" + token; let end = "PSKB_END_" + token
        try write("printf '\\n\(begin)\\n'; (set -e; \(script)); pskb_result=$?; printf '\\n\(end):%s\\n' \"$pskb_result\"\n")
        _ = try waitFor(begin + "\r\n",seconds:seconds,stop:stop)
        let output = try waitFor(end + ":",seconds:seconds,stop:stop)
        let status = try waitFor("\n",seconds:seconds,stop:stop).trimmingCharacters(in:.whitespacesAndNewlines)
        guard status == "0" else { throw RuntimeFailure("guest_command_failed") }
        return String(output.dropLast((end + ":").count)).trimmingCharacters(in:.whitespacesAndNewlines)
    }
    func collect(seconds: Double, stop: RuntimeStopFlag? = nil) throws -> String {
        let deadline = RuntimeSocket.now + seconds
        var data = Data()
        repeat {
            if stop?.requested == true { terminate(); throw RuntimeFailure("cancelled") }
            data.append(try readAvailable())
            guard data.count <= 1_048_576 else { terminate(); throw RuntimeFailure("process_output_limit") }
            if !running {
                data.append(try readAvailable())
                guard process.terminationStatus == 0 else { throw RuntimeFailure("command_failed") }
                return String(decoding:data,as:UTF8.self)
            }
            Thread.sleep(forTimeInterval:0.02)
        } while RuntimeSocket.now < deadline
        terminate(); throw RuntimeFailure("command_deadline")
    }
    @discardableResult
    func terminate() -> Bool {
        try? input.fileHandleForWriting.close()
        if running { process.terminate() }
        let deadline = RuntimeSocket.now + 2
        while running && RuntimeSocket.now < deadline { _ = try? readAvailable(); Thread.sleep(forTimeInterval:0.02) }
        if running { _ = kill(process.processIdentifier,SIGKILL) }
        let killedDeadline = RuntimeSocket.now + 2
        while running && RuntimeSocket.now < killedDeadline { Thread.sleep(forTimeInterval:0.02) }
        return !running
    }
    deinit { if running { terminate() }; try? output.fileHandleForReading.close(); try? input.fileHandleForWriting.close() }
}

enum RuntimeHID {
    /// Metadata only: no IOHIDManager open, report reads, or serial properties.
    static func dualSenseEntries() throws -> Set<UInt64> {
        var iterator: io_iterator_t = 0
        guard IOServiceGetMatchingServices(kIOMainPortDefault,IOServiceMatching("IOHIDDevice"),&iterator) == KERN_SUCCESS else { throw RuntimeFailure("hid_enumeration_failed") }
        defer { IOObjectRelease(iterator) }
        var entries = Set<UInt64>()
        while case let entry = IOIteratorNext(iterator), entry != 0 {
            defer { IOObjectRelease(entry) }
            func property(_ name: String) -> Any? { IORegistryEntryCreateCFProperty(entry,name as CFString,kCFAllocatorDefault,0)?.takeRetainedValue() }
            guard property("VendorID") as? Int == 0x054c, property("ProductID") as? Int == 0x0ce6,
                  property("PrimaryUsagePage") as? Int == 1, property("PrimaryUsage") as? Int == 5,
                  property("Transport") as? String == "USB" else { continue }
            var identifier: UInt64 = 0
            if IORegistryEntryGetRegistryEntryID(entry,&identifier) == KERN_SUCCESS { entries.insert(identifier) }
        }
        return entries
    }
}
