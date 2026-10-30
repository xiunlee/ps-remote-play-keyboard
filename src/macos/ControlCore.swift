// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Pure input lifecycle and repeat timing. No keyboard hooks, HID, UI, files,
// network, process inspection or clocks. The caller supplies synthetic/events
// and monotonic timestamps, and serializes access on one actor/queue.

public enum MacAction: Int, CaseIterable, Sendable {
    // Keep this order aligned with research/native-merge/keyboard_merge.h.
    case leftStickUp, leftStickDown, leftStickLeft, leftStickRight
    case dpadUp, dpadDown, dpadLeft, dpadRight
    case cross, circle, square, triangle
    case l1, r1, l2, r2, create, options, l3, r3, ps, touchClick

    public var mask: UInt32 { UInt32(1) << rawValue }
    public static let allMask: UInt32 = (UInt32(1) << 22) - 1
}

public enum MacRepeatMode: Int, Sendable { case off = 0, hold = 1, toggle = 2 }

public struct MacBinding: Equatable, Sendable {
    // Hardware key codes, not Unicode, Windows virtual keys or HID usages.
    // In macOS keyCode 0 is A; nil alone means unassigned.
    public let keyCode: UInt16?
    public let repeatMode: MacRepeatMode
    public let intervalMilliseconds: UInt64

    public init(keyCode: UInt16?, repeatMode: MacRepeatMode = .off,
                intervalMilliseconds: UInt64 = 100) {
        self.keyCode = keyCode
        self.repeatMode = repeatMode
        self.intervalMilliseconds = intervalMilliseconds
    }
}

public enum MacProfileError: Error, Equatable {
    case unsupportedPlatform, unsupportedVersion, wrongActionCount
    case unsupportedOrReservedKey(UInt16), duplicateKey(UInt16), invalidInterval
}

public struct MacBindingProfile: Equatable, Sendable {
    // A future serializer must emit and validate both fields. Windows format
    // 1/2 cannot be passed in as Mac hardware codes, even where numbers overlap.
    public static let platform = "macOS-hardware-keycode"
    public static let schemaVersion = 1
    public static let pauseKeyCode: UInt16 = 0x64 // kVK_F8
    public static let minInterval: UInt64 = 20
    public static let maxInterval: UInt64 = 5000
    public let bindings: [MacBinding]

    public init(platform: String = Self.platform, version: Int = Self.schemaVersion,
                bindings: [MacBinding]) throws {
        guard platform == Self.platform else { throw MacProfileError.unsupportedPlatform }
        guard version == Self.schemaVersion else { throw MacProfileError.unsupportedVersion }
        guard bindings.count == MacAction.allCases.count else { throw MacProfileError.wrongActionCount }
        var keys: Set<UInt16> = []
        for binding in bindings {
            guard (Self.minInterval...Self.maxInterval).contains(binding.intervalMilliseconds) else {
                throw MacProfileError.invalidInterval
            }
            if let key = binding.keyCode {
                guard Self.isAllowedKey(key) else { throw MacProfileError.unsupportedOrReservedKey(key) }
                guard keys.insert(key).inserted else { throw MacProfileError.duplicateKey(key) }
            }
        }
        self.bindings = bindings
    }

    public static let `default`: MacBindingProfile = {
        // Local macOS SDK Events.h constants. Positions follow the existing
        // product layout, but these integers are deliberately Mac-specific.
        let keys: [UInt16] = [
            0x0D, 0x01, 0x00, 0x02, // W S A D
            0x73, 0x77, 0x72, 0x74, // Home End Help/Insert PageUp
            0x7D, 0x7C, 0x7B, 0x7E, // Down Right Left Up arrows
            0x0E, 0x75, 0x0C, 0x79, // E ForwardDelete Q PageDown
            0x30, 0x24, 0x03, 0x31, // Tab Return F Space
            0x7A, 0x0F              // F1 R
        ]
        return try! MacBindingProfile(bindings: keys.map { MacBinding(keyCode: $0) })
    }()

    public static func isAllowedKey(_ key: UInt16) -> Bool {
        // Explicit physical-key whitelist from the SDK's Events.h. F8,
        // modifiers, media keys and undefined codes cannot be game bindings.
        if key <= 0x33 { return true } // ANSI/ISO characters + Return/Tab/Space/Delete
        return [UInt16](arrayLiteral:
            0x35, 0x40, 0x41, 0x43, 0x45, 0x47, 0x4B, 0x4C, 0x4E,
            0x4F, 0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57,
            0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F,
            0x60, 0x61, 0x62, 0x63, 0x65, 0x66, 0x67, 0x68, 0x69,
            0x6A, 0x6B, 0x6D, 0x6F, 0x71, 0x72, 0x73, 0x74, 0x75,
            0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E
        ).contains(key)
    }

    public subscript(action: MacAction) -> MacBinding { bindings[action.rawValue] }

    public func action(forKeyCode key: UInt16) -> MacAction? {
        MacAction.allCases.first { bindings[$0.rawValue].keyCode == key }
    }

    public func withBinding(_ action: MacAction, keyCode: UInt16?) throws -> Self {
        var changed = bindings
        let old = self[action]
        changed[action.rawValue] = MacBinding(keyCode: keyCode, repeatMode: old.repeatMode,
                                             intervalMilliseconds: old.intervalMilliseconds)
        return try Self(bindings: changed)
    }

    public func withRepeat(_ action: MacAction, mode: MacRepeatMode,
                           intervalMilliseconds: UInt64) throws -> Self {
        var changed = bindings
        changed[action.rawValue] = MacBinding(keyCode: self[action].keyCode, repeatMode: mode,
                                             intervalMilliseconds: intervalMilliseconds)
        return try Self(bindings: changed)
    }
}

public struct MacControlCore: Sendable {
    public private(set) var profile: MacBindingProfile
    public private(set) var enabled = false
    public private(set) var foreground = false
    public private(set) var connected = false
    public private(set) var modifiersDown = false
    public private(set) var editing = false

    private var physicalDown = Set<UInt16>()
    private var capturedDown = Set<UInt16>()
    private var disarmed = Set<UInt16>()
    private var ownedActions: [UInt16: MacAction] = [:]
    private var latched: UInt32 = 0
    private var phaseStarts = [UInt64](repeating: 0, count: 22)
    private var lastTime: UInt64 = 0

    public init(profile: MacBindingProfile = .default) { self.profile = profile }

    public var active: Bool { enabled && connected && foreground && !modifiersDown && !editing }

    // Call on actual foreground, connection, modifier or editing changes, and
    // before sampling output. Fn alone must not be treated as a forbidden
    // modifier: it can be required to generate F8/Home/etc. on compact Macs.
    public mutating func updateEnvironment(foreground: Bool, connected: Bool,
                                           modifiersDown: Bool = false, editing: Bool = false) {
        self.foreground = foreground
        self.connected = connected
        self.modifiersDown = modifiersDown
        self.editing = editing
        if !connected || editing { enabled = false }
        if !active { reset() }
    }

    // A UI button may arm while this app owns focus; output still requires
    // the official Sony app in front and a newly pressed game key.
    @discardableResult
    public mutating func setEnabled(_ value: Bool) -> Bool {
        reset()
        enabled = value && connected && !editing
        return enabled
    }

    // Call only after the caller successfully saves the user's chosen profile.
    // This pure method never writes files. Editing/publishing always pauses.
    public mutating func applyProfile(_ value: MacBindingProfile) {
        reset()
        enabled = false
        profile = value
    }

    // Focus loss, explicit pause, failed output and shutdown use this method.
    // Captured downs remain owned through repeats/up so suppression is paired.
    public mutating func reset() {
        disarmed.formUnion(physicalDown)
        latched = 0
        phaseStarts = [UInt64](repeating: 0, count: 22)
    }

    // Returns true only if an intercepting caller should consume this event.
    // Pass *all* physical down/up events while the tap is alive, including
    // outside Sony, so keys held before foreground entry cannot become input.
    // An injected event changes neither physical ownership nor repeat state.
    @discardableResult
    public mutating func process(keyCode: UInt16, isDown: Bool, isRepeat: Bool = false,
                                 injected: Bool = false, atMilliseconds: UInt64) -> Bool {
        guard !injected, keyCode < 128 else { return false }
        let now = monotonic(atMilliseconds)
        if !active { reset() }
        if !isDown {
            let consume = capturedDown.remove(keyCode) != nil
            physicalDown.remove(keyCode)
            disarmed.remove(keyCode)
            ownedActions.removeValue(forKey: keyCode)
            return consume
        }
        if physicalDown.contains(keyCode) { return capturedDown.contains(keyCode) }
        physicalDown.insert(keyCode)
        if isRepeat {
            // First observed event can be a repeat after a tap restart. Never
            // invent ownership of the unseen physical down.
            disarmed.insert(keyCode)
            return false
        }
        if keyCode == MacBindingProfile.pauseKeyCode {
            guard foreground && connected && !modifiersDown && !editing else {
                disarmed.insert(keyCode)
                return false
            }
            capturedDown.insert(keyCode)
            setEnabled(!enabled)
            return true
        }
        guard active, let action = profile.action(forKeyCode: keyCode) else {
            disarmed.insert(keyCode)
            return false
        }
        capturedDown.insert(keyCode)
        ownedActions[keyCode] = action
        let binding = profile[action]
        if binding.repeatMode == .toggle {
            latched ^= action.mask
            if latched & action.mask != 0 { phaseStarts[action.rawValue] = now }
        } else if binding.repeatMode == .hold { phaseStarts[action.rawValue] = now }
        return true
    }

    public mutating func evaluate(atMilliseconds: UInt64) -> UInt32 {
        let now = monotonic(atMilliseconds)
        guard active else { reset(); return 0 }
        var held: UInt32 = 0
        for (key, action) in ownedActions where physicalDown.contains(key) && !disarmed.contains(key) {
            held |= action.mask
        }
        var result: UInt32 = 0
        for action in MacAction.allCases {
            let binding = profile[action]
            guard binding.keyCode != nil else { continue }
            if binding.repeatMode == .off { result |= held & action.mask; continue }
            let repeating = binding.repeatMode == .toggle ? latched & action.mask != 0 : held & action.mask != 0
            if repeating {
                let phase = (now - phaseStarts[action.rawValue]) % binding.intervalMilliseconds
                if phase * 2 < binding.intervalMilliseconds { result |= action.mask }
            }
        }
        return result
    }

    private mutating func monotonic(_ sample: UInt64) -> UInt64 {
        lastTime = max(sample, lastTime)
        return lastTime
    }
}
