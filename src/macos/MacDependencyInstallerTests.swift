// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Temporary filesystem and owned subprocess fixtures only. No network, installs,
// vendor executables, App launches, VM, USB or permission operations.
import Foundation
import CryptoKit
import Darwin

@main
@MainActor
struct MacDependencyInstallerTests {
    static func check(_ value: Bool) { precondition(value) }
    static func expectFailure(_ code: String, _ body: () async throws -> Void) async throws {
        do { try await body(); preconditionFailure("Expected \(code)") }
        catch let error as MacDependencyError { check(error.localizedDescription == code) }
    }
    static func main() async throws {
        if CommandLine.arguments.count > 1 {
            switch CommandLine.arguments[1] {
            case "--ignore-term":
                signal(SIGTERM, SIG_IGN)
                try String(getpid()).write(toFile: CommandLine.arguments[2], atomically: true, encoding: .utf8)
                while true { usleep(10_000) }
            case "--emit":
                usleep(50_000); FileHandle.standardOutput.write(Data("complete".utf8)); return
            default: preconditionFailure("Unsupported fixture")
            }
        }
        let fm = FileManager.default
        let temporary = fm.temporaryDirectory.resolvingSymlinksInPath().appendingPathComponent("pskeyboard-installer-" + UUID().uuidString)
        try fm.createDirectory(at: temporary, withIntermediateDirectories: false, attributes: [.posixPermissions: 0o700])
        defer { try? fm.removeItem(at: temporary) }
        let directory = temporary.appendingPathComponent("owned")
        try fm.createDirectory(at: directory, withIntermediateDirectories: false, attributes: [.posixPermissions: 0o700])
        let stage = directory.appendingPathComponent(".prepare-fixture")
        try fm.createDirectory(at: stage, withIntermediateDirectories: false)
        let destination = directory.appendingPathComponent("vhusbdarm64")
        let staged = stage.appendingPathComponent("vhusbdarm64")
        try Data("old".utf8).write(to: destination); try Data("new".utf8).write(to: staged)
        try MacDependencyInstaller.replaceOwnedItem(staged, destination: destination, directory: directory)
        check(try Data(contentsOf: destination) == Data("new".utf8))
        do {
            try MacDependencyInstaller.replaceOwnedItem(staged, destination: destination, directory: directory)
            preconditionFailure("Missing staging must restore the previous item")
        } catch { }
        check(try Data(contentsOf: destination) == Data("new".utf8))
        check(try fm.contentsOfDirectory(atPath: directory.path).filter { $0.hasPrefix(".previous-") }.isEmpty)
        let unrelated = temporary.appendingPathComponent("unrelated")
        try Data("untouched".utf8).write(to: unrelated)
        try fm.removeItem(at: destination); try fm.createSymbolicLink(at: destination, withDestinationURL: unrelated)
        try Data("repaired".utf8).write(to: staged)
        try MacDependencyInstaller.replaceOwnedItem(staged, destination: destination, directory: directory)
        check(try Data(contentsOf: unrelated) == Data("untouched".utf8))
        check(try Data(contentsOf: destination) == Data("repaired".utf8))
        try await expectFailure("vendor_replacement_scope") {
            try MacDependencyInstaller.replaceOwnedItem(staged, destination: unrelated, directory: directory)
        }
        let hash = SHA256.hash(data: Data("repaired".utf8)).map { String(format: "%02x", $0) }.joined()
        try await MacDependencyInstaller.verifyHash(destination, expected: hash, maximumBytes: 100)
        try await expectFailure("vendor_component_version_changed") {
            try await MacDependencyInstaller.verifyHash(destination, expected: String(repeating: "0", count: 64), maximumBytes: 100)
        }
        try await expectFailure("vendor_component_size") {
            try await MacDependencyInstaller.verifyHash(destination, expected: hash, maximumBytes: 2)
        }
        for _ in 0..<12 {
            let result = try await MacDependencyInstaller.run("/usr/bin/printf", ["final output"])
            check(String(decoding: result, as: UTF8.self) == "final output")
        }
        try await expectFailure("vendor_process_output_limit") {
            _ = try await MacDependencyInstaller.run("/usr/bin/yes", [], timeout: 2, maximumOutput: 64)
        }
        let cancel = Task { try await MacDependencyInstaller.run("/bin/sleep", ["10"]) }
        try await Task.sleep(for: .milliseconds(50))
        let begin = ProcessInfo.processInfo.systemUptime; cancel.cancel()
        do { _ = try await cancel.value; preconditionFailure("Expected cancellation") } catch is CancellationError { }
        check(ProcessInfo.processInfo.systemUptime - begin < 4)
        let pidFile = temporary.appendingPathComponent("fixture.pid")
        try await expectFailure("vendor_process_timeout") {
            _ = try await MacDependencyInstaller.run(CommandLine.arguments[0], ["--ignore-term", pidFile.path], timeout: 0.2)
        }
        let pid = Int32(try String(contentsOf: pidFile, encoding: .utf8))!
        check(kill(pid, 0) == -1 && errno == ESRCH)
        let cleanup = Task { try await MacDependencyInstaller.run(CommandLine.arguments[0], ["--emit"], cancellable: false) }
        cleanup.cancel()
        check(String(decoding: try await cleanup.value, as: UTF8.self) == "complete")
        print("Installer checks passed: repair, rollback, symlink scope, hash bounds, output bounds, cancellation, TERM/KILL and cleanup")
    }
}
