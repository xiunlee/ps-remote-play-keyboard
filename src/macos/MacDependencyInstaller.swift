// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import Foundation
import CryptoKit
import Darwin

enum MacDependencyError: Error, LocalizedError {
    case failed(String)
    var errorDescription: String? {
        switch self { case .failed(let code): return code }
    }
}

/// Obtains unmodified vendor components from the official source. No service,
/// privilege changes, automatic licensing, or vendor re-signing.
@MainActor
final class MacDependencyInstaller {
    static let directory = FileManager.default.homeDirectoryForCurrentUser
        .appendingPathComponent("Library/Application Support/PSKeyboard/Dependencies", isDirectory: true)
    private let clientHash = "edfbba188187711a3707929aefff68a9b0c260224f28831d1103aa4c325aa437"
    private let serverHash = "09deced5586ed37b8b3db7a6187d84029f65d487c3c2b24a949b993b3d582077"
    private let clientURL = URL(string: "https://www.virtualhere.com/sites/default/files/usbclient/VirtualHereUniversal.dmg")!
    private let serverURL = URL(string: "https://www.virtualhere.com/sites/default/files/usbserver/vhusbdarm64")!

    func prepare(progress: @escaping (String) -> Void) async throws {
        let manager = FileManager.default
        let directory = Self.directory
        try manager.createDirectory(at: directory, withIntermediateDirectories: true,
                                    attributes: [.posixPermissions: 0o700])
        guard directory.resolvingSymlinksInPath().standardizedFileURL == directory.standardizedFileURL else {
            throw MacDependencyError.failed("vendor_directory_symlink")
        }
        let lock = open(directory.appendingPathComponent(".install.lock").path,
                        O_CREAT | O_RDWR | O_NOFOLLOW | O_CLOEXEC, 0o600)
        guard lock >= 0 else { throw MacDependencyError.failed("vendor_setup_lock_failed") }
        defer { _ = flock(lock, LOCK_UN); close(lock) }
        guard flock(lock, LOCK_EX | LOCK_NB) == 0 else { throw MacDependencyError.failed("vendor_setup_in_progress") }
        let client = directory.appendingPathComponent("VirtualHereUniversal.app", isDirectory: true)
        let server = directory.appendingPathComponent("vhusbdarm64")
        progress("Checking required components…")
        var clientReady = false, serverReady = false
        if Self.itemExists(client) {
            do { try await verifyClient(client); clientReady = true }
            catch is CancellationError { throw CancellationError() }
            catch { try Task.checkCancellation() }
        }
        if Self.itemExists(server) {
            do {
                try await Self.verifyHash(server, expected: serverHash, maximumBytes: 2_000_000)
                try manager.setAttributes([.posixPermissions: 0o700], ofItemAtPath: server.path)
                serverReady = true
            } catch is CancellationError { throw CancellationError() }
            catch { try Task.checkCancellation() }
        }
        if clientReady && serverReady { progress("Required components are ready."); return }
        // Never replace an owned client while another session is using it.
        if !clientReady, Self.itemExists(client) {
            let executable = client.appendingPathComponent("Contents/MacOS/VirtualHereUniversal").path
            let processes = try await Self.run("/bin/ps", ["-axo", "comm="], timeout: 5)
            guard !String(decoding: processes, as: UTF8.self).split(separator: "\n").contains(where: {
                $0.trimmingCharacters(in: .whitespaces) == executable
            }) else { throw MacDependencyError.failed("vendor_client_in_use") }
        }

        let temporary = directory.appendingPathComponent(".prepare-" + UUID().uuidString, isDirectory: true)
        try manager.createDirectory(at: temporary, withIntermediateDirectories: false,
                                    attributes: [.posixPermissions: 0o700])
        var mountMayExist = false
        defer { if !mountMayExist { try? manager.removeItem(at: temporary) } }
        if !serverReady {
            progress("Downloading the controller component…")
            let staged = temporary.appendingPathComponent("vhusbdarm64")
            try await download(serverURL, to: staged, expectedHash: serverHash, maximumBytes: 2_000_000)
            try manager.setAttributes([.posixPermissions: 0o700], ofItemAtPath: staged.path)
            try Task.checkCancellation()
            try Self.replaceOwnedItem(staged, destination: server, directory: directory)
        }
        if !clientReady {
            progress("Downloading VirtualHere…")
            let diskImage = temporary.appendingPathComponent("VirtualHereUniversal.dmg")
            try await download(clientURL, to: diskImage, expectedHash: clientHash, maximumBytes: 60_000_000)
            let mount = temporary.appendingPathComponent("mount", isDirectory: true)
            try manager.createDirectory(at: mount, withIntermediateDirectories: false)
            do {
                // The known private mount point also lets cancellation clean up
                // an image attached just before hdiutil was interrupted.
                mountMayExist = true
                let mounted = try await Self.run("/usr/bin/hdiutil", ["attach", "-readonly", "-nobrowse", "-noautoopen",
                    "-mountpoint", mount.path, "-plist", diskImage.path])
                guard let plist = try PropertyListSerialization.propertyList(from: mounted, format: nil) as? [String: Any],
                      let entities = plist["system-entities"] as? [[String: Any]],
                      entities.contains(where: { $0["mount-point"] as? String == mount.path }) else {
                    throw MacDependencyError.failed("vendor_image_mount_failed")
                }
                let mountedApp = mount.appendingPathComponent("VirtualHereUniversal.app")
                try await verifyClient(mountedApp)
                let stagedApp = temporary.appendingPathComponent("VirtualHereUniversal.app")
                _ = try await Self.run("/usr/bin/ditto", [mountedApp.path, stagedApp.path])
                try await verifyClient(stagedApp)
                try Task.checkCancellation()
                _ = try await Self.run("/usr/bin/hdiutil", ["detach", mount.path], timeout: 10, cancellable: false)
                mountMayExist = false
                try Task.checkCancellation()
                try Self.replaceOwnedItem(stagedApp, destination: client, directory: directory)
            } catch {
                if mountMayExist {
                    if (try? await Self.run("/usr/bin/hdiutil", ["detach", mount.path], timeout: 10, cancellable: false)) != nil {
                        mountMayExist = false
                    }
                    // A failed detach retains only our private staging directory;
                    // do not recursively remove a potentially mounted volume.
                }
                throw error
            }
        }
        progress("Required components are ready.")
    }

    private func download(_ url: URL, to destination: URL, expectedHash: String,
                          maximumBytes: Int) async throws {
        var request = URLRequest(url: url)
        request.timeoutInterval = 90
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 30
        configuration.timeoutIntervalForResource = 90
        let session = URLSession(configuration: configuration)
        defer { session.invalidateAndCancel() }
        let (file, response) = try await session.download(for: request)
        defer { try? FileManager.default.removeItem(at: file) }
        guard let response = response as? HTTPURLResponse, response.statusCode == 200,
              response.url?.scheme == "https", response.url?.host == "www.virtualhere.com",
              let size = try file.resourceValues(forKeys: [.fileSizeKey]).fileSize,
              size > 0, size <= maximumBytes else {
            throw MacDependencyError.failed("vendor_download_failed")
        }
        try await Self.verifyHash(file, expected: expectedHash, maximumBytes: maximumBytes)
        try Task.checkCancellation()
        try FileManager.default.moveItem(at: file, to: destination)
    }

    nonisolated static func verifyHash(_ url: URL, expected: String, maximumBytes: Int) async throws {
        let worker = Task.detached(priority: .utility) {
            let descriptor = open(url.path, O_RDONLY | O_NOFOLLOW | O_CLOEXEC)
            guard descriptor >= 0 else { throw MacDependencyError.failed("vendor_component_unreadable") }
            defer { close(descriptor) }
            var metadata = stat()
            guard fstat(descriptor, &metadata) == 0, metadata.st_mode & S_IFMT == S_IFREG,
                  metadata.st_size > 0, metadata.st_size <= maximumBytes else {
                throw MacDependencyError.failed("vendor_component_size")
            }
            var hash = SHA256(), count = 0
            var buffer = [UInt8](repeating: 0, count: 65_536)
            while true {
                try Task.checkCancellation()
                let length = read(descriptor, &buffer, buffer.count)
                if length < 0 && errno == EINTR { continue }
                guard length >= 0 else { throw MacDependencyError.failed("vendor_component_unreadable") }
                if length == 0 { break }
                count += length
                guard count <= maximumBytes else { throw MacDependencyError.failed("vendor_component_size") }
                hash.update(data: buffer.prefix(length))
            }
            let actual = hash.finalize().map { String(format: "%02x", $0) }.joined()
            guard actual == expected else { throw MacDependencyError.failed("vendor_component_version_changed") }
        }
        try await withTaskCancellationHandler { try await worker.value } onCancel: { worker.cancel() }
    }

    private func verifyClient(_ app: URL) async throws {
        guard try app.resourceValues(forKeys: [.isSymbolicLinkKey]).isSymbolicLink != true else {
            throw MacDependencyError.failed("vendor_component_symlink")
        }
        let infoURL = app.appendingPathComponent("Contents/Info.plist")
        guard let size = try infoURL.resourceValues(forKeys: [.fileSizeKey]).fileSize,
              size > 0, size <= 65_536 else { throw MacDependencyError.failed("vendor_client_metadata") }
        let info = try Data(contentsOf: infoURL)
        guard let plist = try PropertyListSerialization.propertyList(from: info, format: nil) as? [String: Any],
              plist["CFBundleIdentifier"] as? String == "com.virtualhere.client",
              plist["CFBundleShortVersionString"] as? String == "6.0.3" else {
            throw MacDependencyError.failed("vendor_client_version_changed")
        }
        _ = try await Self.run("/usr/bin/codesign", ["--verify", "--deep", "--strict", "-R",
            "=anchor apple generic and identifier \"com.virtualhere.client\" and certificate leaf[subject.OU] = \"N8C8ZTN347\"", app.path])
    }

    nonisolated private static func itemExists(_ url: URL) -> Bool {
        var value = stat()
        return lstat(url.path, &value) == 0
    }

    /// Commit only a fully verified staged component, and retain the previous
    /// owned item outside staging until replacement succeeds. Never traverse an
    /// old symlink when replacing or deleting it; its target is unrelated.
    nonisolated static func replaceOwnedItem(_ staged: URL, destination: URL, directory: URL) throws {
        let manager = FileManager.default
        let root = directory.standardizedFileURL
        guard root.resolvingSymlinksInPath() == root,
              destination.deletingLastPathComponent().standardizedFileURL == root,
              ["VirtualHereUniversal.app", "vhusbdarm64"].contains(destination.lastPathComponent),
              staged.standardizedFileURL.path.hasPrefix(root.path + "/.prepare-"),
              staged.resolvingSymlinksInPath() == staged.standardizedFileURL else {
            throw MacDependencyError.failed("vendor_replacement_scope")
        }
        let backup = root.appendingPathComponent(".previous-" + UUID().uuidString + "-" + destination.lastPathComponent)
        let hadPrevious = itemExists(destination)
        if hadPrevious { try manager.moveItem(at: destination, to: backup) }
        do {
            try manager.moveItem(at: staged, to: destination)
        } catch {
            if hadPrevious {
                do { try manager.moveItem(at: backup, to: destination) }
                catch { throw MacDependencyError.failed("vendor_replacement_restore_failed") }
            }
            throw error
        }
        if hadPrevious { try manager.removeItem(at: backup) }
    }

    /// Direct arguments, bounded nonblocking output, cancellation, and bounded
    /// TERM/KILL handling all run off MainActor. Cleanup may ignore cancellation.
    nonisolated static func run(_ executable: String, _ arguments: [String], timeout: TimeInterval = 45,
                                cancellable: Bool = true, maximumOutput: Int = 1_048_576) async throws -> Data {
        let cancellation = InstallerProcessCancellation()
        return try await withTaskCancellationHandler {
            if cancellable { try Task.checkCancellation() }
            return try await Task.detached(priority: .utility) {
            if cancellation.isCancelled { throw CancellationError() }
            let process = Process()
            process.executableURL = URL(fileURLWithPath: executable)
            process.arguments = arguments
            let output = Pipe()
            process.standardOutput = output
            process.standardError = FileHandle.nullDevice
            process.standardInput = FileHandle.nullDevice
            try process.run()
            try? output.fileHandleForWriting.close()
            defer { try? output.fileHandleForReading.close() }
            let descriptor = output.fileHandleForReading.fileDescriptor
            let flags = fcntl(descriptor, F_GETFL)
            guard flags >= 0, fcntl(descriptor, F_SETFL, flags | O_NONBLOCK) == 0 else {
                stopProcess(process)
                throw MacDependencyError.failed("vendor_process_pipe")
            }
            let deadline = ProcessInfo.processInfo.systemUptime + timeout
            var data = Data(), buffer = [UInt8](repeating: 0, count: 8192)
            do {
                while true {
                    if cancellation.isCancelled { throw CancellationError() }
                    guard ProcessInfo.processInfo.systemUptime < deadline else {
                        throw MacDependencyError.failed("vendor_process_timeout")
                    }
                    // Observe exit before draining, so a final write racing an
                    // earlier EAGAIN cannot be omitted from hdiutil's plist.
                    let wasRunning = process.isRunning
                    while true {
                        let count = read(descriptor, &buffer, buffer.count)
                        if count < 0 && errno == EINTR { continue }
                        if count < 0 && (errno == EAGAIN || errno == EWOULDBLOCK) { break }
                        guard count >= 0 else { throw MacDependencyError.failed("vendor_process_pipe") }
                        if count == 0 { break }
                        guard data.count + count <= maximumOutput else { throw MacDependencyError.failed("vendor_process_output_limit") }
                        data.append(contentsOf: buffer.prefix(count))
                    }
                    if !wasRunning {
                        guard process.terminationStatus == 0 else {
                            throw MacDependencyError.failed("vendor_" + URL(fileURLWithPath: executable).lastPathComponent + "_failed")
                        }
                        return data
                    }
                    pauseProcessPoll()
                }
            } catch { stopProcess(process); throw error }
            }.value
        } onCancel: { if cancellable { cancellation.cancel() } }
    }

    nonisolated private static func pauseProcessPoll() { Thread.sleep(forTimeInterval: 0.01) }

    nonisolated private static func stopProcess(_ process: Process) {
        if process.isRunning { process.terminate() }
        let deadline = ProcessInfo.processInfo.systemUptime + 1
        while process.isRunning && ProcessInfo.processInfo.systemUptime < deadline { Thread.sleep(forTimeInterval: 0.01) }
        if process.isRunning { _ = kill(process.processIdentifier, SIGKILL) }
        let killedDeadline = ProcessInfo.processInfo.systemUptime + 2
        while process.isRunning && ProcessInfo.processInfo.systemUptime < killedDeadline { Thread.sleep(forTimeInterval: 0.01) }
    }
}

private final class InstallerProcessCancellation: @unchecked Sendable {
    private let lock = NSLock()
    private var cancelled = false
    var isCancelled: Bool { lock.lock(); defer { lock.unlock() }; return cancelled }
    func cancel() { lock.lock(); cancelled = true; lock.unlock() }
}
