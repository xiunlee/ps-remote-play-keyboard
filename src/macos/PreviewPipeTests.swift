// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Local POSIX pipes and synthetic clocks only. No subprocesses, HID, keys or Sony.
import Darwin
import Dispatch
import Foundation

@main
struct PreviewPipeTests {
    static func main() throws {
        var checks = 0
        func check(_ value: Bool, _ label: String) {
            checks += 1
            precondition(value, label)
        }
        func localPipe(now: UInt64 = 0) throws -> (PreviewPipe, FileHandle, Int32) {
            var descriptors = [Int32](repeating: -1, count: 2)
            precondition(Darwin.pipe(&descriptors) == 0)
            let reader = FileHandle(fileDescriptor: descriptors[0], closeOnDealloc: true)
            let handle = FileHandle(fileDescriptor: descriptors[1], closeOnDealloc: true)
            precondition(fcntl(descriptors[0], F_SETFL, O_NONBLOCK) == 0)
            return (try PreviewPipe(writer: handle, now: now), reader, descriptors[1])
        }
        func read(_ reader: FileHandle) -> (Int, Data) {
            var bytes = [UInt8](repeating: 0, count: 4096)
            let count = Darwin.read(reader.fileDescriptor, &bytes, bytes.count)
            return (count, count > 0 ? Data(bytes.prefix(count)) : Data())
        }

        do {
            let (output, reader, descriptor) = try localPipe()
            defer { output.shutdown(); try? reader.close() }
            check(fcntl(descriptor, F_GETFL) & O_NONBLOCK != 0, "writer is nonblocking")
            check(fcntl(descriptor, F_GETNOSIGPIPE) == 1, "SIGPIPE suppressed on our fd only")
            check(fcntl(descriptor, F_GETFD) & FD_CLOEXEC != 0, "child cannot inherit spare writer")
            let frame = Data(repeating: 42, count: 512)
            check(output.write(frame: frame), "PIPE_BUF sized frame accepted")
            check(read(reader).1 == frame, "whole frame delivered exactly")
            output.close(); output.close(); output.shutdown()
            output.pulse(at: .max); output.startWatchdog()
            check(output.isClosed && !output.write(frame: frame), "close is final and idempotent")
            check(read(reader).0 == 0, "reader sees EOF")
        }

        do {
            let (output, reader, _) = try localPipe()
            defer { output.shutdown(); try? reader.close() }
            output.check(now: 249_999_999)
            check(!output.isClosed, "before deadline stays open")
            output.pulse(at: 200_000_000)
            output.pulse(at: 100_000_000)
            output.check(now: 199_999_999)
            output.check(now: 449_999_999)
            check(!output.isClosed, "pulse extends deadline; old timestamps cannot underflow or regress")
            check(output.write(frame: Data([10])), "write before deadline works")
            output.check(now: 450_000_000)
            check(output.isClosed, "deadline closes even after a successful write")
            check(read(reader).1 == Data([10]) && read(reader).0 == 0, "queued byte then EOF")
        }

        do {
            let (output, reader, _) = try localPipe(now: UInt64.max - 100)
            defer { output.shutdown(); try? reader.close() }
            output.check(now: 0); output.check(now: .max)
            check(!output.isClosed, "near-maximum clock does not overflow")
        }

        for size in [0, 513] {
            let (output, reader, _) = try localPipe()
            defer { output.shutdown(); try? reader.close() }
            check(!output.write(frame: Data(repeating: 1, count: size)), "invalid frame refused")
            check(output.isClosed && read(reader).0 == 0, "invalid frame closes without data")
        }

        do {
            let (output, reader, _) = try localPipe()
            defer { output.shutdown(); try? reader.close() }
            let frame = Data(repeating: 7, count: 512)
            var accepted = 0
            while accepted < 4096, output.write(frame: frame) { accepted += 1 }
            check(accepted > 0 && accepted < 4096 && output.isClosed, "backpressure fails closed without blocking")
            var received = 0
            while true {
                let (count, data) = read(reader)
                if count == 0 { break }
                precondition(count > 0 && data.allSatisfy { $0 == 7 })
                received += count
            }
            check(received == accepted * 512, "failed atomic write appended no partial frame")
        }

        do {
            let (output, reader, _) = try localPipe()
            defer { output.shutdown() }
            try reader.close()
            check(!output.write(frame: Data([10])) && output.isClosed, "EPIPE closes without process SIGPIPE")
        }

        do {
            let (output, reader, _) = try localPipe()
            defer { output.shutdown(); try? reader.close() }
            DispatchQueue.concurrentPerform(iterations: 8) { value in
                precondition(output.write(frame: Data(repeating: UInt8(value), count: 64)))
            }
            output.shutdown()
            let data = read(reader).1
            check(data.count == 512, "concurrent writes all delivered")
            for start in stride(from: 0, to: data.count, by: 64) {
                check(Set(data[start..<(start + 64)]).count == 1, "concurrent frame remains intact")
            }
            check(read(reader).0 == 0, "concurrent writes followed by EOF")
        }

        do {
            let (output, reader, _) = try localPipe(now: 0)
            defer { output.shutdown(); try? reader.close() }
            output.startWatchdog(); output.startWatchdog()
            // Block this test's main thread: the watchdog must run on its own queue.
            var descriptor = pollfd(fd: reader.fileDescriptor, events: Int16(POLLIN), revents: 0)
            check(Darwin.poll(&descriptor, 1, 2_000) == 1, "watchdog wakes reader while main thread blocked")
            check(output.isClosed && read(reader).0 == 0, "watchdog independently closes stdin")
        }
        print("PASS: \(checks) PreviewPipe local checks (no app, subprocess or device access)")
    }
}
