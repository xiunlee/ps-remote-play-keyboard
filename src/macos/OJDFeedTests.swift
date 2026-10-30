// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Synthetic masks and fake clocks only. Never launches OJD or opens a device.
import Foundation

@main
struct OJDFeedTests {
    static func main() throws {
        var checks = 0
        func check(_ value: Bool, _ label: String) {
            checks += 1
            precondition(value, label)
        }
        func rejects(_ expected: OJDFeedError, _ body: () throws -> Void) {
            do { try body(); preconditionFailure("Expected \(expected)") }
            catch let error as OJDFeedError { check(error == expected, "precise error") }
            catch { preconditionFailure("Unexpected error \(error)") }
        }
        func mask(_ actions: [MacAction]) -> UInt32 { actions.reduce(0) { $0 | $1.mask } }
        func frame(_ actions: [MacAction]) throws -> OJDFeedFrame {
            try OJDFeedEncoder.frame(actions: mask(actions), profile: .generic)
        }
        let neutral = try OJDFeedEncoder.frame(actions: 0, profile: .generic)
        let neutralLine = try OJDFeedEncoder.line(actions: 0, profile: .generic)
        let southLine = try OJDFeedEncoder.line(actions: MacAction.cross.mask, profile: .generic)
        check(neutral.buttons.isEmpty && neutral.dpad.isEmpty, "neutral has no digital controls")
        check(neutral.axes.count == 6 && neutral.axes.values.allSatisfy { $0 == 0 }, "all six neutral axes explicit")
        check(Set(neutral.axes.keys) == Set(["LeftStickX", "LeftStickY", "RightStickX", "RightStickY", "LeftTrigger", "RightTrigger"]), "exact raw axis keys")
        check(OJDFeedEncoder.supportedActions | OJDFeedEncoder.unsupportedActions == MacAction.allMask, "capabilities cover all actions")
        check(OJDFeedEncoder.unsupportedActions == MacAction.touchClick.mask, "only touch unsupported in report ABI")
        for profile in OJDFeedProfile.allCases {
            check(profile.cliArguments == ["--no-input", "--json", "--timeout", "5", "virtual", "feed", "--as", profile.rawValue], "pinned CLI arguments")
            check(try OJDFeedEncoder.line(actions: 0, profile: profile) == neutralLine, "same full-state neutral on all profiles")
            rejects(.unsupportedActions(MacAction.touchClick.mask)) {
                _ = try OJDFeedEncoder.line(actions: MacAction.cross.mask | MacAction.touchClick.mask, profile: profile)
            }
            rejects(.unknownActions(1 << 31)) {
                _ = try OJDFeedEncoder.line(actions: 1 << 31, profile: profile)
            }
            let digital: [(MacAction, String)] = [
                (.cross, "South"), (.circle, "East"), (.square, "West"), (.triangle, "North"),
                (.l1, "LeftShoulder"), (.r1, "RightShoulder"), (.l2, "LeftTrigger"), (.r2, "RightTrigger"),
                (.create, "Back"), (.options, "Start"), (.l3, "LeftStickClick"), (.r3, "RightStickClick"), (.ps, "Guide")
            ]
            for (action, name) in digital {
                let result = try OJDFeedEncoder.frame(actions: action.mask, profile: profile)
                check(result.buttons == [name] && result.dpad.isEmpty, "each digital action maps exactly once")
                check(result.axes["LeftTrigger"] == (action == .l2 ? 1 : 0), "L2 analog value agrees with button")
                check(result.axes["RightTrigger"] == (action == .r2 ? 1 : 0), "R2 analog value agrees with button")
            }
            check(try OJDFeedEncoder.frame(actions: MacAction.create.mask, profile: profile).buttons != ["Capture"], "Create maps Back, not Xbox capture")
            let all = try OJDFeedEncoder.frame(actions: OJDFeedEncoder.supportedActions, profile: profile)
            check(all.buttons.count == digital.count, "all supported digital actions coexist")
            check(all.dpad.isEmpty && all.axes["LeftStickX"] == 0 && all.axes["LeftStickY"] == 0, "all opposing directions cancel")
        }
        for (action, axis, value) in [
            (MacAction.leftStickUp, "LeftStickY", -1.0), (.leftStickDown, "LeftStickY", 1.0),
            (.leftStickLeft, "LeftStickX", -1.0), (.leftStickRight, "LeftStickX", 1.0)
        ] {
            let result = try frame([action])
            check(result.axes[axis] == value && result.buttons.isEmpty && result.dpad.isEmpty, "four stick directions have correct sign")
            check(result.axes["RightStickX"] == 0 && result.axes["RightStickY"] == 0, "no invented right stick input")
        }
        let diagonal = try frame([.leftStickUp, .leftStickRight])
        let x = diagonal.axes["LeftStickX"]!, y = diagonal.axes["LeftStickY"]!
        check(x > 0 && y < 0 && abs(x * x + y * y - 1) < 1e-12, "diagonal stays on unit circle")
        let canceledX = try frame([.leftStickLeft, .leftStickRight, .leftStickDown])
        check(canceledX.axes["LeftStickX"] == 0 && canceledX.axes["LeftStickY"] == 1, "cancel X before diagonal normalization")
        for (action, name) in [(MacAction.dpadUp, "DpadUp"), (.dpadDown, "DpadDown"), (.dpadLeft, "DpadLeft"), (.dpadRight, "DpadRight")] {
            let result = try frame([action])
            check(result.dpad == [name] && result.buttons.isEmpty, "exact dpad raw strings, not wiki aliases")
            check(result.axes.values.allSatisfy { $0 == 0 }, "dpad does not move stick")
        }
        check(try frame([.dpadUp, .dpadLeft]).dpad == ["DpadLeft", "DpadUp"], "diagonal dpad is two unique sorted raw values")
        check(try frame([.dpadUp, .dpadDown, .dpadLeft]).dpad == ["DpadLeft"], "opposing dpad directions cancel")
        let json = try JSONSerialization.jsonObject(with: neutralLine) as! [String: Any]
        check(Set(json.keys) == Set(["axes", "buttons", "dpad"]), "only supported frame fields emitted")
        check(neutralLine.last == 10 && neutralLine.dropLast().contains(10) == false, "exactly one LF delimiter")
        check(try OJDFeedEncoder.line(actions: 0, profile: .generic) == neutralLine, "deterministic JSON")
        check(!String(decoding: southLine, as: UTF8.self).contains("holdMilliseconds"), "no delayed frame hold requested")

        // Every write has an explicit completion; fake time is measured from
        // completion rather than a timer callback that may have been delayed.
        rejects(.invalidFrameInterval) { _ = try OJDFeedSession(profile: .generic, frameIntervalMilliseconds: 7) }
        var session = try OJDFeedSession(profile: .generic)
        check(session.phase == .notStarted && !session.hasPendingWrite, "initial idle session")
        rejects(.noWriteToAcknowledge) { try session.writeSucceeded(atMilliseconds: 0) }
        rejects(.noEOFToAcknowledge) { try session.acknowledgeStdinClosed() }
        check(try session.next(actions: MacAction.cross.mask, atMilliseconds: 0) == .writeLine(neutralLine), "first write is always neutral")
        check(session.hasPendingWrite, "write pending until caller acknowledges complete delivery")
        check(try session.next(actions: MacAction.circle.mask, atMilliseconds: 50) == nil, "no queue while previous write outstanding")
        try session.writeSucceeded(atMilliseconds: 50)
        check(session.phase == .running && !session.hasPendingWrite, "acknowledged initial write starts running")
        check(try session.next(actions: MacAction.cross.mask, atMilliseconds: 57) == nil, "less than 8ms cannot write")
        check(try session.next(actions: MacAction.cross.mask, atMilliseconds: 58) == .writeLine(southLine), "8ms accepts current state")
        try session.writeSucceeded(atMilliseconds: 58)
        check(try session.next(actions: 0, atMilliseconds: 1) == nil, "older clock cannot underflow or create catch-up")
        check(try session.next(actions: 0, atMilliseconds: 100_000) == .writeLine(neutralLine), "late sample uses latest neutral without replay")
        try session.writeSucceeded(atMilliseconds: 100_000)
        check(try session.next(actions: MacAction.cross.mask, atMilliseconds: 100_000) == nil, "no burst on same late timestamp")
        session.requestStop()
        session.requestStop()
        check(session.phase == .stopping, "stop is idempotent")
        check(try session.next(actions: MacAction.cross.mask, atMilliseconds: 100_007) == nil, "final neutral also respects minimum spacing")
        check(try session.next(actions: MacAction.cross.mask, atMilliseconds: 100_008) == .writeLine(neutralLine), "stop ignores held input and writes neutral")
        check(try session.next(actions: 0, atMilliseconds: 100_010) == nil, "EOF cannot precede final write completion")
        try session.writeSucceeded(atMilliseconds: 100_010)
        check(try session.next(actions: 0, atMilliseconds: 100_010) == .closeStdin, "EOF offered after acknowledged final neutral")
        session.requestStop()
        check(session.phase == .awaitingStdinClose, "stop cannot erase pending EOF acknowledgement")
        check(try session.next(actions: 0, atMilliseconds: 100_020) == nil, "EOF offered only once")
        try session.acknowledgeStdinClosed()
        check(session.phase == .stdinClosed && !session.ioFailed, "closed records local stdin only")
        session.requestStop()
        check(try session.next(actions: MacAction.cross.mask, atMilliseconds: .max) == nil, "closed session never restarts")

        var earlyStop = try OJDFeedSession(profile: .generic)
        earlyStop.requestStop()
        check(try earlyStop.next(actions: .max, atMilliseconds: 0) == .writeLine(neutralLine), "stop before start sends only neutral")
        try earlyStop.writeSucceeded(atMilliseconds: 0)
        check(try earlyStop.next(actions: 0, atMilliseconds: 0) == .closeStdin, "stop before start closes after neutral")

        var interrupted = try OJDFeedSession(profile: .generic)
        _ = try interrupted.next(actions: 0, atMilliseconds: 0)
        try interrupted.writeSucceeded(atMilliseconds: 0)
        _ = try interrupted.next(actions: MacAction.cross.mask, atMilliseconds: 8)
        interrupted.requestStop()
        try interrupted.writeSucceeded(atMilliseconds: 9)
        check(try interrupted.next(actions: MacAction.cross.mask, atMilliseconds: 17) == .writeLine(neutralLine), "stop during pending sample still requires later neutral")
        try interrupted.writeSucceeded(atMilliseconds: 17)
        check(try interrupted.next(actions: 0, atMilliseconds: 17) == .closeStdin, "pending sample stop closes normally")

        var failed = try OJDFeedSession(profile: .generic)
        _ = try failed.next(actions: 0, atMilliseconds: 0)
        failed.writeFailed()
        check(failed.ioFailed && !failed.hasPendingWrite, "partial write marks delivery unconfirmed")
        check(try failed.next(actions: MacAction.cross.mask, atMilliseconds: 0) == .closeStdin, "broken pipe cannot safely retry JSON")
        failed.writeFailed()
        check(failed.phase == .awaitingStdinClose, "duplicate failure cannot erase pending EOF acknowledgement")
        try failed.acknowledgeStdinClosed()
        check(failed.phase == .stdinClosed && failed.ioFailed, "local close preserves failure evidence")

        var rejected = try OJDFeedSession(profile: .generic)
        _ = try rejected.next(actions: 0, atMilliseconds: 0)
        try rejected.writeSucceeded(atMilliseconds: 0)
        rejects(.unsupportedActions(MacAction.touchClick.mask)) {
            _ = try rejected.next(actions: MacAction.touchClick.mask, atMilliseconds: 8)
        }
        check(rejected.phase == .stopping && !rejected.hasPendingWrite, "unsupported input stops accepted output")
        check(try rejected.next(actions: MacAction.cross.mask, atMilliseconds: 8) == .writeLine(neutralLine), "unsupported frame followed by neutral")

        var slow = try OJDFeedSession(profile: .generic, frameIntervalMilliseconds: 20)
        _ = try slow.next(actions: 0, atMilliseconds: UInt64.max - 20)
        try slow.writeSucceeded(atMilliseconds: UInt64.max - 20)
        check(try slow.next(actions: MacAction.cross.mask, atMilliseconds: UInt64.max - 1) == nil, "custom interval enforced near maximum clock")
        check(try slow.next(actions: MacAction.cross.mask, atMilliseconds: UInt64.max) == .writeLine(southLine), "clock maximum does not overflow")

        // Exercise the actual core-to-feed boundary with an unbound unsupported
        // control, toggle repetition and focus loss. No real keys are observed.
        var profile = try MacBindingProfile.default.withBinding(.touchClick, keyCode: nil)
        profile = try profile.withRepeat(.cross, mode: .toggle, intervalMilliseconds: 20)
        var core = MacControlCore(profile: profile)
        core.updateEnvironment(foreground: true, connected: true)
        core.setEnabled(true)
        let key = profile[.cross].keyCode!
        core.process(keyCode: key, isDown: true, atMilliseconds: 0)
        core.process(keyCode: key, isDown: false, atMilliseconds: 1)
        check(try OJDFeedEncoder.line(actions: core.evaluate(atMilliseconds: 8), profile: .generic) == southLine, "core toggle produces South frame")
        check(try OJDFeedEncoder.line(actions: core.evaluate(atMilliseconds: 16), profile: .generic) == neutralLine, "core off phase produces full neutral")
        core.updateEnvironment(foreground: false, connected: true)
        core.updateEnvironment(foreground: true, connected: true)
        check(try OJDFeedEncoder.line(actions: core.evaluate(atMilliseconds: 24), profile: .generic) == neutralLine, "focus return does not resume toggled output")
        print("PASS: \(checks) OJD feed pure checks (no subprocess, devices, keyboard or Sony access)")
    }
}
