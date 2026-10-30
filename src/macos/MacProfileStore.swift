// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import Foundation
import Darwin

struct MacProfileLoadResult: Sendable {
    let profile: MacBindingProfile
    /// A MacText key, deliberately not a filesystem error or private path.
    let warning: String?
}

/// Mac-only persistence. Loading never creates, repairs, migrates or rewrites a file.
struct MacProfileStore: Sendable {
    typealias LoadResult = MacProfileLoadResult
    private let fileURL: URL

    init(fileURL: URL = Self.defaultURL) { self.fileURL = fileURL }
    func load() -> MacProfileLoadResult { Self.load(from: fileURL) }
    func save(profile: MacBindingProfile) throws { try Self.save(profile: profile, to: fileURL) }

    enum StoreError: Error { case unsafeDestination, invalidDocument }

    static var defaultURL: URL {
        FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/Application Support/PSKeyboard", isDirectory: true)
            .appendingPathComponent("mac-bindings.json")
    }
    static let maximumFileBytes = 16_384

    private struct Document: Codable {
        let platform: String
        let version: Int
        let keys: [UInt16?]
        let rapidFireModes: [Int]
        let intervalMilliseconds: [UInt64]
    }

    static func load(from fileURL: URL = defaultURL) -> LoadResult {
        do {
            guard try regularFileExists(at: fileURL) else {
                return LoadResult(profile: .default, warning: nil)
            }
            let handle = try FileHandle(forReadingFrom: fileURL)
            defer { try? handle.close() }
            let data = try handle.read(upToCount: maximumFileBytes + 1) ?? Data()
            guard data.count <= maximumFileBytes,
                  let object = try JSONSerialization.jsonObject(with: data) as? [String: Any],
                  Set(object.keys) == Set(["platform", "version", "keys", "rapidFireModes", "intervalMilliseconds"]) else {
                throw StoreError.invalidDocument
            }
            let document = try JSONDecoder().decode(Document.self, from: data)
            guard document.keys.count == MacAction.allCases.count,
                  document.rapidFireModes.count == MacAction.allCases.count,
                  document.intervalMilliseconds.count == MacAction.allCases.count else {
                throw StoreError.invalidDocument
            }
            var bindings: [MacBinding] = []
            for index in MacAction.allCases.indices {
                guard let mode = MacRepeatMode(rawValue: document.rapidFireModes[index]) else {
                    throw StoreError.invalidDocument
                }
                bindings.append(MacBinding(keyCode: document.keys[index], repeatMode: mode,
                                           intervalMilliseconds: document.intervalMilliseconds[index]))
            }
            let profile = try MacBindingProfile(platform: document.platform, version: document.version,
                                                 bindings: bindings)
            return LoadResult(profile: profile, warning: nil)
        } catch {
            return LoadResult(profile: .default, warning: "ProfileLoadWarning")
        }
    }

    /// Called only by an explicit Save action. The previous bytes are backed up
    /// before an atomic replacement; an invalid prior file is backed up unchanged.
    static func save(profile: MacBindingProfile, to fileURL: URL = defaultURL) throws {
        let directory = fileURL.deletingLastPathComponent()
        let manager = FileManager.default
        try manager.createDirectory(at: directory, withIntermediateDirectories: true,
                                    attributes: [.posixPermissions: 0o700])
        let document = Document(platform: MacBindingProfile.platform,
                                version: MacBindingProfile.schemaVersion,
                                keys: profile.bindings.map(\.keyCode),
                                rapidFireModes: profile.bindings.map { $0.repeatMode.rawValue },
                                intervalMilliseconds: profile.bindings.map(\.intervalMilliseconds))
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(document)
        guard data.count <= maximumFileBytes else { throw StoreError.invalidDocument }

        let temporary = directory.appendingPathComponent(".mac-bindings-\(UUID().uuidString).tmp")
        let backupTemporary = directory.appendingPathComponent(".mac-bindings-backup-\(UUID().uuidString).tmp")
        defer {
            try? manager.removeItem(at: temporary)
            try? manager.removeItem(at: backupTemporary)
        }
        if try regularFileExists(at: fileURL) {
            let backup = backupURL(for: fileURL)
            _ = try regularFileExists(at: backup)
            try manager.copyItem(at: fileURL, to: backupTemporary)
            try manager.setAttributes([.posixPermissions: 0o600], ofItemAtPath: backupTemporary.path)
            try synchronizeFile(at: backupTemporary)
            try atomicRename(from: backupTemporary, to: backup)
        }
        try data.write(to: temporary, options: .withoutOverwriting)
        try manager.setAttributes([.posixPermissions: 0o600], ofItemAtPath: temporary.path)
        try synchronizeFile(at: temporary)
        // Recheck immediately before replacement: never follow a symlink target.
        _ = try regularFileExists(at: fileURL)
        try atomicRename(from: temporary, to: fileURL)
    }

    static func backupURL(for fileURL: URL = defaultURL) -> URL {
        fileURL.appendingPathExtension("backup")
    }

    private static func regularFileExists(at url: URL) throws -> Bool {
        var info = stat()
        if lstat(url.path, &info) != 0 {
            if errno == ENOENT { return false }
            throw POSIXError(POSIXErrorCode(rawValue: errno) ?? .EIO)
        }
        guard info.st_mode & S_IFMT == S_IFREG else { throw StoreError.unsafeDestination }
        return true
    }

    private static func synchronizeFile(at url: URL) throws {
        let handle = try FileHandle(forWritingTo: url)
        defer { try? handle.close() }
        try handle.synchronize()
    }

    private static func atomicRename(from source: URL, to destination: URL) throws {
        guard rename(source.path, destination.path) == 0 else {
            throw POSIXError(POSIXErrorCode(rawValue: errno) ?? .EIO)
        }
    }
}
