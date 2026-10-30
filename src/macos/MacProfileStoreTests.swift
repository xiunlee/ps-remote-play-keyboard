// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Isolated temporary files only; never reads or writes the user's profile.
import Foundation

@main
struct MacProfileStoreTests {
    static func main() throws {
        let manager = FileManager.default
        let directory = manager.temporaryDirectory.appendingPathComponent("PSKeyboard-profile-tests-\(UUID())")
        try manager.createDirectory(at: directory, withIntermediateDirectories: false)
        defer { try? manager.removeItem(at: directory) }
        var checks = 0
        func check(_ value: Bool, _ label: String) {
            checks += 1
            precondition(value, label)
        }
        func expectSaveFailure(_ store: MacProfileStore) {
            do { try store.save(profile: .default); preconditionFailure("save must fail") }
            catch { checks += 1 }
        }

        let file = directory.appendingPathComponent("settings/mac-bindings.json")
        let store = MacProfileStore(fileURL: file)
        let missing = store.load()
        check(missing.profile == .default && missing.warning == nil, "missing profile uses defaults")
        check(!manager.fileExists(atPath: file.deletingLastPathComponent().path), "load creates no directory")

        var changed = try MacBindingProfile.default.withBinding(.cross, keyCode: nil)
        changed = try changed.withRepeat(.leftStickLeft, mode: .hold, intervalMilliseconds: 20)
        changed = try changed.withRepeat(.options, mode: .toggle, intervalMilliseconds: 5000)
        try store.save(profile: changed)
        let originalBytes = try Data(contentsOf: file)
        let loaded = store.load()
        check(loaded.profile == changed && loaded.warning == nil, "all bindings and repeat data round trip")
        check(loaded.profile[.leftStickLeft].keyCode == 0, "hardware code zero remains A")
        check(loaded.profile[.cross].keyCode == nil, "only null means unassigned")
        let permissions = try manager.attributesOfItem(atPath: file.path)[.posixPermissions] as? NSNumber
        check(permissions?.intValue == 0o600, "profile is private to the user")
        let backup = MacProfileStore.backupURL(for: file)
        check(!manager.fileExists(atPath: backup.path), "initial save does not invent an old backup")

        try store.save(profile: .default)
        check(try Data(contentsOf: backup) == originalBytes, "backup preserves exact old bytes")
        let secondBytes = try Data(contentsOf: file)
        try store.save(profile: changed)
        check(try Data(contentsOf: backup) == secondBytes, "one backup is updated to the previous file")

        let goodObject = try JSONSerialization.jsonObject(with: originalBytes) as! [String: Any]
        func rejects(_ bytes: Data, _ label: String) throws {
            try bytes.write(to: file)
            let result = store.load()
            check(result.profile == .default && result.warning == "ProfileLoadWarning", label)
            check(try Data(contentsOf: file) == bytes, "load preserves \(label)")
            check(try Data(contentsOf: backup) == secondBytes, "load does not change the backup")
        }
        try rejects(Data("{ broken JSON".utf8), "malformed JSON")
        for (key, value) in [("platform", "Windows-virtual-key" as Any), ("version", 2 as Any),
                             ("future", true as Any)] {
            var object = goodObject
            object[key] = value
            try rejects(JSONSerialization.data(withJSONObject: object), "unsupported \(key)")
        }
        var windows = goodObject
        windows.removeValue(forKey: "platform")
        try rejects(JSONSerialization.data(withJSONObject: windows), "Windows or untagged profile")
        for key in ["keys", "rapidFireModes", "intervalMilliseconds"] {
            var object = goodObject
            object[key] = [0]
            try rejects(JSONSerialization.data(withJSONObject: object), "wrong count: \(key)")
        }
        for invalidKey in [0x35 as Any, 0x3B as Any, 0x7F as Any, true as Any, -1 as Any, 65536 as Any] {
            var object = goodObject
            var keys = object["keys"] as! [Any]
            keys[0] = invalidKey
            object["keys"] = keys
            try rejects(JSONSerialization.data(withJSONObject: object), "reserved/invalid key \(invalidKey)")
        }
        var duplicate = goodObject
        var keys = duplicate["keys"] as! [Any]
        keys[0] = keys[1]
        duplicate["keys"] = keys
        try rejects(JSONSerialization.data(withJSONObject: duplicate), "duplicate key")
        for invalidInterval in [0, 19, 5001] {
            var object = goodObject
            var intervals = object["intervalMilliseconds"] as! [Any]
            intervals[0] = invalidInterval
            object["intervalMilliseconds"] = intervals
            try rejects(JSONSerialization.data(withJSONObject: object), "invalid interval \(invalidInterval)")
        }
        var invalidMode = goodObject
        var modes = invalidMode["rapidFireModes"] as! [Any]
        modes[0] = 3
        invalidMode["rapidFireModes"] = modes
        try rejects(JSONSerialization.data(withJSONObject: invalidMode), "unknown repeat mode")
        let oversized = Data(repeating: 32, count: MacProfileStore.maximumFileBytes + 1)
        try rejects(oversized, "oversized file")
        try store.save(profile: changed)
        check(try Data(contentsOf: backup) == oversized, "explicit save backs up invalid bytes unchanged")
        check(store.load().profile == changed, "explicit save replaces rejected profile")

        try manager.removeItem(at: backup)
        try manager.createDirectory(at: backup, withIntermediateDirectories: false)
        let beforeFailure = try Data(contentsOf: file)
        expectSaveFailure(store)
        check(try Data(contentsOf: file) == beforeFailure, "backup failure preserves active file")
        check(try manager.contentsOfDirectory(atPath: file.deletingLastPathComponent().path).sorted()
              == ["mac-bindings.json", "mac-bindings.json.backup"], "failed save cleans only its temporary files")
        try manager.removeItem(at: backup)

        let target = directory.appendingPathComponent("separate-user-file.json")
        let targetBytes = Data("must stay unchanged".utf8)
        try targetBytes.write(to: target)
        try manager.removeItem(at: file)
        try manager.createSymbolicLink(at: file, withDestinationURL: target)
        check(store.load().warning != nil, "symlink profile is rejected on load")
        expectSaveFailure(store)
        check(try Data(contentsOf: target) == targetBytes, "symlink target remains unchanged")
        try manager.removeItem(at: file)
        try manager.createDirectory(at: file, withIntermediateDirectories: false)
        check(store.load().warning != nil, "directory is rejected on load")
        expectSaveFailure(store)

        print("MacProfileStoreTests: \(checks) checks passed")
    }
}
