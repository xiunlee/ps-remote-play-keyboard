// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Fake clocks and synthetic key events only. No AppKit, HID or Sony access.
@main
struct MacControlCoreTests {
    static func main() throws {
        var checks = 0
        func check(_ condition: Bool, _ label: String) {
            checks += 1
            precondition(condition, label)
        }
        func rejects(_ label: String, _ body: () throws -> Void) {
            do { try body(); preconditionFailure(label) }
            catch { checks += 1 }
        }
        func armed(_ profile: MacBindingProfile = .default) -> MacControlCore {
            var value = MacControlCore(profile: profile)
            value.updateEnvironment(foreground: true, connected: true)
            value.setEnabled(true)
            return value
        }
        let defaults = MacBindingProfile.default
        check(defaults.bindings.count == 22, "22 ABI bindings")
        check(MacAction.touchClick.mask == 1 << 21 && MacAction.cross.mask == 1 << 8, "native action positions")
        check(defaults[.leftStickLeft].keyCode == 0, "Mac A is key code zero")
        check(defaults[.leftStickUp].keyCode == 0x0D, "Mac W differs from Windows W")
        let compactDefaults: [(MacAction, UInt16)] = [
            (.leftStickUp, 0x0D), (.leftStickDown, 0x01), (.leftStickLeft, 0x00), (.leftStickRight, 0x02),
            (.dpadUp, 0x7E), (.dpadDown, 0x7D), (.dpadLeft, 0x7B), (.dpadRight, 0x7C),
            (.triangle, 0x22), (.square, 0x26), (.cross, 0x28), (.circle, 0x25),
            (.l1, 0x0C), (.r1, 0x0E), (.l2, 0x06), (.r2, 0x08),
            (.create, 0x30), (.options, 0x24), (.l3, 0x03), (.r3, 0x31), (.ps, 0x23), (.touchClick, 0x11)
        ]
        for (action, key) in compactDefaults {
            check(defaults[action].keyCode == key && defaults.action(forKeyCode: key) == action,
                  "compact default maps the requested action and key in both directions")
        }
        let compactKeys = defaults.bindings.compactMap(\.keyCode)
        check(compactKeys.count == 22 && Set(compactKeys + [MacBindingProfile.pauseKeyCode]).count == 23,
              "22 mapped actions plus pause use 23 distinct keys")
        check(compactKeys.allSatisfy { $0 <= 0x33 || (0x7B...0x7E).contains($0) },
              "no default needs Fn, function keys, Home, End, Insert or Page keys")
        check(compactKeys.allSatisfy(MacBindingProfile.isAllowedKey), "all compact defaults are assignable")
        check(MacBindingProfile.pauseKeyCode == 0x35 && !MacBindingProfile.isAllowedKey(0x35), "Escape is the reserved pause key")
        check(defaults.bindings.allSatisfy { $0.repeatMode == .off && $0.intervalMilliseconds == 100 }, "default repeats off")
        rejects("Windows profile rejected") { _ = try MacBindingProfile(platform: "windows-vk", bindings: defaults.bindings) }
        rejects("other schema rejected") { _ = try MacBindingProfile(version: 2, bindings: defaults.bindings) }
        rejects("missing binding rejected") { _ = try MacBindingProfile(bindings: Array(defaults.bindings.dropLast())) }
        rejects("duplicate binding rejected") { _ = try defaults.withBinding(.cross, keyCode: defaults[.circle].keyCode) }
        rejects("Escape reserved") { _ = try defaults.withBinding(.cross, keyCode: 0x35) }
        check(try defaults.withBinding(.cross, keyCode: 0x64)[.cross].keyCode == 0x64,
              "F8 is no longer reserved and can be explicitly rebound")
        for key: UInt16 in [0x34, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3F, 0x48, 0x7F, 0xFFFF] {
            rejects("reserved/unsupported key") { _ = try defaults.withBinding(.cross, keyCode: key) }
        }
        for interval: UInt64 in [0, 19, 5001, UInt64.max] {
            rejects("invalid interval") { _ = try defaults.withRepeat(.cross, mode: .hold, intervalMilliseconds: interval) }
        }
        let noA = try defaults.withBinding(.leftStickLeft, keyCode: nil)
        check(noA.action(forKeyCode: 0) == nil && defaults.action(forKeyCode: 0) == .leftStickLeft,
              "nil is unassigned; original profile remains immutable")

        for action in MacAction.allCases {
            let key = defaults[action].keyCode!
            var core = armed()
            check(core.process(keyCode: key, isDown: true, atMilliseconds: 0), "owns game down")
            check(core.evaluate(atMilliseconds: 0) == action.mask, "each action exact bit")
            check(core.evaluate(atMilliseconds: 100_000) == action.mask, "normal press stays held")
            check(core.process(keyCode: key, isDown: false, atMilliseconds: 100_001), "owns paired release")
            check(core.evaluate(atMilliseconds: 100_001) == 0, "each action released")

            for mode in [MacRepeatMode.hold, .toggle] {
                core = armed(try defaults.withRepeat(action, mode: mode, intervalMilliseconds: 20))
                core.process(keyCode: key, isDown: true, atMilliseconds: 0)
                if mode == .toggle { core.process(keyCode: key, isDown: false, atMilliseconds: 1) }
                check(core.evaluate(atMilliseconds: 9) == action.mask, "minimum interval on half")
                check(core.evaluate(atMilliseconds: 10) == 0, "minimum interval off half")
                check(core.evaluate(atMilliseconds: 20) == action.mask, "minimum interval next cycle")
                core.updateEnvironment(foreground: false, connected: true)
                check(core.evaluate(atMilliseconds: 21) == 0, "focus loss releases each mode")
                core.updateEnvironment(foreground: true, connected: true)
                check(core.evaluate(atMilliseconds: 40) == 0, "focus return cannot resume each mode")
            }
        }

        let crossKey = defaults[.cross].keyCode!, squareKey = defaults[.square].keyCode!
        let cross = MacAction.cross.mask, square = MacAction.square.mask
        let toggle = try defaults.withRepeat(.cross, mode: .toggle, intervalMilliseconds: 100)
        var core = armed(toggle)
        core.process(keyCode: crossKey, isDown: true, atMilliseconds: 0)
        core.process(keyCode: crossKey, isDown: false, atMilliseconds: 1)
        check(core.evaluate(atMilliseconds: 2) == cross, "fast tap between output samples is retained")
        check(core.evaluate(atMilliseconds: 50) == 0, "released toggle off phase")
        check(core.evaluate(atMilliseconds: 100) == cross, "released toggle next cycle")
        check(!core.process(keyCode: crossKey, isDown: true, injected: true, atMilliseconds: 101), "injected down ignored")
        check(core.evaluate(atMilliseconds: 101) == cross, "injected down cannot flip toggle")
        core.process(keyCode: crossKey, isDown: true, atMilliseconds: 110)
        check(core.evaluate(atMilliseconds: 110) == 0, "second physical press stops toggle")
        check(core.process(keyCode: crossKey, isDown: true, isRepeat: true, atMilliseconds: 111), "OS repeat remains owned")
        check(core.evaluate(atMilliseconds: 111) == 0, "OS repeat cannot flip toggle")
        check(!core.process(keyCode: crossKey, isDown: false, injected: true, atMilliseconds: 112), "injected release ignored")
        core.process(keyCode: crossKey, isDown: true, atMilliseconds: 113)
        check(core.evaluate(atMilliseconds: 113) == 0, "injected release cannot manufacture fresh press")
        core.process(keyCode: crossKey, isDown: false, atMilliseconds: 114)
        core.process(keyCode: crossKey, isDown: true, atMilliseconds: 120)
        check(core.evaluate(atMilliseconds: 120) == cross, "fresh press toggles on")
        core.reset()
        check(core.evaluate(atMilliseconds: 121) == 0, "explicit reset clears all output")
        core.process(keyCode: crossKey, isDown: true, isRepeat: true, atMilliseconds: 122)
        check(core.evaluate(atMilliseconds: 122) == 0, "held through reset cannot re-arm")
        core.updateEnvironment(foreground: false, connected: true)
        check(core.process(keyCode: crossKey, isDown: false, atMilliseconds: 123), "captured release consumed outside Sony")
        core.updateEnvironment(foreground: true, connected: true)
        core.process(keyCode: crossKey, isDown: true, atMilliseconds: 124)
        check(core.evaluate(atMilliseconds: 124) == cross, "release then fresh press re-arms")

        core = armed(toggle)
        core.updateEnvironment(foreground: false, connected: true)
        check(!core.process(keyCode: crossKey, isDown: true, atMilliseconds: 0), "outside key is not captured")
        core.updateEnvironment(foreground: true, connected: true)
        check(!core.process(keyCode: crossKey, isDown: true, isRepeat: true, atMilliseconds: 1), "outside held key stays outside")
        check(core.evaluate(atMilliseconds: 1) == 0, "outside press cannot toggle on focus return")
        check(!core.process(keyCode: crossKey, isDown: false, atMilliseconds: 2), "outside release stays paired")
        core.process(keyCode: crossKey, isDown: true, atMilliseconds: 3)
        check(core.evaluate(atMilliseconds: 3) == cross, "fresh foreground press accepted")
        core.updateEnvironment(foreground: true, connected: true, modifiersDown: true)
        check(core.evaluate(atMilliseconds: 4) == 0, "modifier clears output")
        core.updateEnvironment(foreground: true, connected: true)
        core.process(keyCode: crossKey, isDown: true, atMilliseconds: 5)
        check(core.evaluate(atMilliseconds: 5) == 0, "modifier release cannot resume held input")

        core = armed()
        check(!core.process(keyCode: crossKey, isDown: true, isRepeat: true, atMilliseconds: 0), "first observed repeat cannot claim ownership")
        check(core.evaluate(atMilliseconds: 0) == 0, "first repeat does not arm")
        core.process(keyCode: crossKey, isDown: false, atMilliseconds: 1)
        core.process(keyCode: crossKey, isDown: true, atMilliseconds: 2)
        core.process(keyCode: squareKey, isDown: true, atMilliseconds: 2)
        check(core.evaluate(atMilliseconds: 2) == cross | square, "multiple ordinary actions combine")
        core.updateEnvironment(foreground: true, connected: false)
        check(!core.enabled && core.evaluate(atMilliseconds: 3) == 0, "disconnect pauses and releases")
        check(!core.setEnabled(true), "cannot enable disconnected")
        core.updateEnvironment(foreground: true, connected: true)
        check(!core.enabled && core.evaluate(atMilliseconds: 4) == 0, "reconnect does not automatically enable")
        core.setEnabled(true)
        check(core.evaluate(atMilliseconds: 5) == 0, "enable while held requires new presses")

        // A UI button can arm in the settings window without a function key.
        core = MacControlCore()
        core.updateEnvironment(foreground: false, connected: true)
        check(core.setEnabled(true) && core.enabled && !core.active, "UI enables standby outside Sony")
        check(core.evaluate(atMilliseconds: 0) == 0, "standby remains neutral")
        check(!core.process(keyCode: crossKey, isDown: true, atMilliseconds: 1), "outside game key passes through while armed")
        core.updateEnvironment(foreground: true, connected: true)
        check(core.active && !core.process(keyCode: crossKey, isDown: true, isRepeat: true, atMilliseconds: 2),
              "focus entry is active but does not capture a key already held")
        check(core.evaluate(atMilliseconds: 2) == 0, "held outside key stays neutral after UI arming")
        check(!core.process(keyCode: crossKey, isDown: false, atMilliseconds: 3), "old outside release passes through")
        check(core.process(keyCode: crossKey, isDown: true, atMilliseconds: 4) && core.evaluate(atMilliseconds: 4) == cross,
              "UI arming plus fresh Sony keydown produces output")
        core.updateEnvironment(foreground: false, connected: true)
        check(core.enabled && !core.active && core.evaluate(atMilliseconds: 5) == 0,
              "focus loss clears output but preserves standby enable state")

        core = MacControlCore()
        core.updateEnvironment(foreground: true, connected: true)
        check(!core.process(keyCode: 0x64, isDown: true, atMilliseconds: 0) && !core.enabled,
              "old F8 shortcut no longer toggles default enable state")
        check(!core.process(keyCode: 0x64, isDown: false, atMilliseconds: 1), "unbound F8 release passes through")

        // Escape only handles one real fresh down, and never outside Sony.
        core = MacControlCore()
        core.updateEnvironment(foreground: true, connected: true)
        check(core.process(keyCode: 0x35, isDown: true, atMilliseconds: 0) && core.enabled, "Escape enables")
        check(core.process(keyCode: 0x35, isDown: true, isRepeat: true, atMilliseconds: 1) && core.enabled, "Escape repeat ignored")
        check(core.process(keyCode: 0x35, isDown: false, atMilliseconds: 2), "Escape release paired")
        core.process(keyCode: crossKey, isDown: true, atMilliseconds: 3)
        check(core.evaluate(atMilliseconds: 3) == cross, "Escape enabled capture")
        core.process(keyCode: 0x35, isDown: true, atMilliseconds: 4)
        check(!core.enabled && core.evaluate(atMilliseconds: 4) == 0, "Escape pauses immediately")
        core.process(keyCode: 0x35, isDown: false, atMilliseconds: 5)
        core.process(keyCode: 0x35, isDown: true, atMilliseconds: 6)
        check(core.enabled && core.evaluate(atMilliseconds: 6) == 0, "Escape resume does not resurrect held action")
        core.process(keyCode: 0x35, isDown: false, atMilliseconds: 7)
        core.updateEnvironment(foreground: false, connected: true)
        check(!core.process(keyCode: 0x35, isDown: true, atMilliseconds: 8), "outside Escape passes through")
        core.updateEnvironment(foreground: true, connected: true)
        check(!core.process(keyCode: 0x35, isDown: true, isRepeat: true, atMilliseconds: 9) && core.enabled, "outside Escape cannot toggle on return")
        check(!core.process(keyCode: 0x35, isDown: false, atMilliseconds: 10), "outside Escape release remains paired")
        core.updateEnvironment(foreground: true, connected: true, modifiersDown: true)
        check(!core.process(keyCode: 0x35, isDown: true, atMilliseconds: 11) && core.enabled,
              "modified Escape cannot change enable state")
        core.process(keyCode: 0x35, isDown: false, atMilliseconds: 12)
        core.updateEnvironment(foreground: true, connected: true)
        check(!core.process(keyCode: 0x35, isDown: true, injected: true, atMilliseconds: 13) && core.enabled,
              "injected Escape cannot change enable state")

        // Profile edits retain old key ownership until release and pause.
        core = armed(toggle)
        core.process(keyCode: crossKey, isDown: true, atMilliseconds: 0)
        let remapped = try toggle.withBinding(.cross, keyCode: 0x0F) // ANSI R
        core.applyProfile(remapped)
        check(!core.enabled && core.evaluate(atMilliseconds: 1) == 0, "profile publish pauses and clears")
        core.setEnabled(true)
        check(core.process(keyCode: crossKey, isDown: true, isRepeat: true, atMilliseconds: 2), "old captured key repeat stays consumed")
        check(core.process(keyCode: crossKey, isDown: false, atMilliseconds: 3), "old captured key release stays consumed")
        check(core.evaluate(atMilliseconds: 3) == 0, "old binding does not produce new output")
        core.process(keyCode: 0x0F, isDown: true, atMilliseconds: 4)
        check(core.evaluate(atMilliseconds: 4) == cross, "new binding works on fresh press")
        core.updateEnvironment(foreground: true, connected: true, editing: true)
        check(!core.enabled && core.evaluate(atMilliseconds: 5) == 0, "editing pauses")
        core.updateEnvironment(foreground: true, connected: true)
        check(!core.enabled, "leaving editor does not resume")

        core = armed()
        check(!core.process(keyCode: 0x0F, isDown: true, atMilliseconds: 0), "unmapped down passes through")
        core.applyProfile(remapped)
        core.setEnabled(true)
        check(!core.process(keyCode: 0x0F, isDown: true, isRepeat: true, atMilliseconds: 1),
              "new mapping cannot capture an already passed down")
        check(core.evaluate(atMilliseconds: 1) == 0, "profile change cannot claim held outside typing")
        check(!core.process(keyCode: 0x0F, isDown: false, atMilliseconds: 2), "new mapping still passes old release")
        check(core.process(keyCode: 0x0F, isDown: true, atMilliseconds: 3), "new mapping captures a fresh press")
        check(core.evaluate(atMilliseconds: 3) == cross, "new mapped toggle begins only on fresh press")

        // Boundaries, independent phases and monotonic sampling.
        for interval: UInt64 in [21, 5000] {
            core = armed(try defaults.withRepeat(.cross, mode: .hold, intervalMilliseconds: interval))
            core.process(keyCode: crossKey, isDown: true, atMilliseconds: 0)
            check(core.evaluate(atMilliseconds: (interval - 1) / 2) == cross, "last on sample")
            check(core.evaluate(atMilliseconds: (interval + 1) / 2) == 0, "first off sample")
            check(core.evaluate(atMilliseconds: interval) == cross, "full period")
        }
        let mixed = try defaults.withRepeat(.cross, mode: .hold, intervalMilliseconds: 100)
            .withRepeat(.square, mode: .toggle, intervalMilliseconds: 200)
        core = armed(mixed)
        core.process(keyCode: crossKey, isDown: true, atMilliseconds: 0)
        core.process(keyCode: squareKey, isDown: true, atMilliseconds: 25)
        core.process(keyCode: squareKey, isDown: false, atMilliseconds: 26)
        check(core.evaluate(atMilliseconds: 60) == square, "independent phases")
        check(core.evaluate(atMilliseconds: 125) == cross, "different action off phase")
        check(core.evaluate(atMilliseconds: 225) == cross | square, "independent phases overlap")
        check(core.evaluate(atMilliseconds: 1_000_099) == square, "delayed sample has no catchup queue")
        check(core.evaluate(atMilliseconds: 10) == square, "older timestamp cannot rewind")
        core = armed(try defaults.withRepeat(.cross, mode: .hold, intervalMilliseconds: 100))
        core.process(keyCode: crossKey, isDown: true, atMilliseconds: UInt64.max - 100)
        check(core.evaluate(atMilliseconds: UInt64.max) == cross, "large timestamps do not overflow")
        check(!core.process(keyCode: 0xFFFF, isDown: true, atMilliseconds: UInt64.max), "out-of-range event ignored")
        print("MAC_CONTROL_CORE_PASS checks=\(checks) actions=22 synthetic_only=true keyboard_captured=false device_created=false sony_accessed=false")
    }
}
