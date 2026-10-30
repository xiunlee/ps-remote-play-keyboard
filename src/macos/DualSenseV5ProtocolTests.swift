// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Offline: link the actual native C merge and independently apply the existing
// C# converter's DS4 -> DualSense -> packed-touch steps as a reference oracle.
// Compile with keyboard_merge.h as an imported C header; see test script.
@main
struct DualSenseV5ProtocolTests {
    static func main() throws {
        var checks = 0
        func check(_ value: Bool, _ label: String) {
            checks += 1
            precondition(value, label)
        }
        func reject(_ bits: UInt32, enabled: Bool = true) {
            do {
                _ = try DualSenseV5Protocol.encode(actionMask: bits, enabled: enabled, sequence: 0)
                preconditionFailure("unknown action must fail")
            } catch let error as DualSenseV5ProtocolError {
                check(error == .unknownActionBits(bits & ~MacAction.allMask), "unknown bits reported exactly")
            } catch { preconditionFailure("unexpected error") }
        }
        func read32(_ bytes: [UInt8], _ offset: Int) -> UInt32 {
            (0..<4).reduce(0) { $0 | (UInt32(bytes[offset + $1]) << ($1 * 8)) }
        }
        func mask(_ actions: [MacAction]) -> UInt32 { actions.reduce(0) { $0 | $1.mask } }

        // Different implementation path: execute the unchanged C merge, then
        // reproduce only the C# wire conversion from its 64-byte DS4 result.
        func nativeReference(_ actions: UInt32, enabled: Bool = true) -> [UInt8] {
            var source = [UInt8](repeating: 0, count: 64)
            source[0] = 1; source[1] = 128; source[2] = 128
            source[3] = 128; source[4] = 128; source[5] = 8
            var report = [UInt8](repeating: 0, count: 64)
            var held = PskbKeyboard(held: actions, enabled: enabled ? 1 : 0)
            let result = pskb_merge_keyboard(source, 64, &held, &report, 64)
            precondition(result == PSKB_MERGE_OK, "native C reference rejected valid mask")
            var state = [UInt8](repeating: 0, count: 33)
            for i in 0..<4 { state[i] = UInt8(truncatingIfNeeded: Int(report[i + 1]) - 128) }
            let ds4 = UInt32(report[5] & 0xF0) | (UInt32(report[6]) << 8) | UInt32(report[7] & 3)
            let buttons = (ds4 & 0xFFF0) | ((ds4 & 3) << 16)
            for i in 0..<4 { state[4 + i] = UInt8(truncatingIfNeeded: buttons >> (i * 8)) }
            state[8] = [UInt8](arrayLiteral: 1, 9, 8, 10, 2, 6, 4, 5, 0)[Int(report[5] & 15)]
            state[9] = report[8]; state[10] = report[9]
            state[15] = 0x80; state[20] = 0x80; state[32] = 0xE0
            return state
        }

        // Table-based CRC oracle is separate from the encoder's bitwise loop.
        let table: [UInt32] = (0..<256).map { index in
            var value = UInt32(index)
            for _ in 0..<8 { value = value & 1 != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1 }
            return value
        }
        func referenceCRC(_ bytes: [UInt8]) -> UInt32 {
            var crc = UInt32.max
            for byte in bytes { crc = table[Int((crc ^ UInt32(byte)) & 255)] ^ (crc >> 8) }
            return crc ^ UInt32.max
        }
        let ieee = Array("123456789".utf8)
        check(referenceCRC(ieee) == 0xCBF43926, "independent CRC standard vector")
        check(DualSenseV5Protocol.crc32IEEE(ieee) == 0xCBF43926, "encoder CRC standard vector")
        check(DualSenseV5Protocol.crc32IEEE([]) == 0, "empty IEEE CRC")
        check(DualSenseV5Protocol.inputSize == 33 && DualSenseV5Protocol.frameSize == 49, "control protocol has exact state and frame sizes")
        check(DualSenseV5Protocol.deviceType == "dualsensegamepadv5", "gamepad persona only")

        let neutralState = nativeReference(0)
        let neutralHeader: [UInt8] = [0x56, 0x50, 0x43, 0x4D, 5, 1, 33, 0, 0, 0, 0, 0, 0x21, 0xB8, 0x8D, 0xB8]
        let neutral = try DualSenseV5Protocol.encode(actionMask: 0, sequence: 0)
        check(neutral == neutralHeader + neutralState, "C# neutral golden frame CRC B88DB821")
        var crossState = neutralState; crossState[4] = 0x20
        let crossHeader: [UInt8] = [0x56, 0x50, 0x43, 0x4D, 5, 1, 33, 0, 0x78, 0x56, 0x34, 0x12, 0x84, 0xAC, 0x4F, 0x68]
        check(try DualSenseV5Protocol.encode(actionMask: MacAction.cross.mask, sequence: 0x12345678) == crossHeader + crossState,
              "C# Cross golden frame CRC 684FAC84 and little-endian sequence")

        for action in MacAction.allCases {
            let state = try DualSenseV5Protocol.controlState(actionMask: action.mask)
            check(state == nativeReference(action.mask), "each of 22 actions matches unchanged C plus existing conversion")
            check(state[2] == 0 && state[3] == 0, "right stick stays neutral")
            check(state[15] == 0x80 && state[20] == 0x80, "every action preserves inactive touches")
            check(Array(state[21..<32]).allSatisfy { $0 == 0 } && state[32] == 0xE0, "every action preserves neutral motion")
            check(try DualSenseV5Protocol.controlState(actionMask: action.mask, enabled: false) == neutralState, "disabled known action is neutral")
        }
        let ps = try DualSenseV5Protocol.controlState(actionMask: MacAction.ps.mask)
        let touch = try DualSenseV5Protocol.controlState(actionMask: MacAction.touchClick.mask)
        check(read32(ps, 4) == 0x10000 && read32(touch, 4) == 0x20000, "PS and touch click move into V5 bits 16 and 17")
        check(touch[15] == 0x80 && touch[20] == 0x80, "touch click never invents a touch contact")
        for (action, axis, expected) in [(MacAction.leftStickLeft, 0, UInt8(128)), (.leftStickRight, 0, UInt8(127)),
                                        (.leftStickUp, 1, UInt8(128)), (.leftStickDown, 1, UInt8(127))] {
            check(try DualSenseV5Protocol.controlState(actionMask: action.mask)[axis] == expected, "cardinal signed stick endpoints")
        }
        let diagonal = try DualSenseV5Protocol.controlState(actionMask: mask([.leftStickUp, .leftStickRight]))
        check(diagonal[0] == 90 && diagonal[1] == 166, "diagonal is signed +90/-90")
        let dpad = try DualSenseV5Protocol.controlState(actionMask: mask([.dpadUp, .dpadRight]))
        check(dpad[8] == 9, "V5 diagonal directional flags are not USB hat 1")
        let l2r2 = try DualSenseV5Protocol.controlState(actionMask: mask([.l2, .r2]))
        check(read32(l2r2, 4) == 0xC00 && l2r2[9] == 255 && l2r2[10] == 255, "triggers set digital and analog fields")

        // Exhaust the 256 combinations of stick and D-pad directions, and
        // all 16,384 subsets of the 14 digital buttons, including opposition.
        for directions: UInt32 in 0..<256 {
            check(try DualSenseV5Protocol.controlState(actionMask: directions) == nativeReference(directions), "exhaustive direction combinations")
        }
        for digital: UInt32 in 0..<(1 << 14) {
            let actions = digital << 8
            check(try DualSenseV5Protocol.controlState(actionMask: actions) == nativeReference(actions), "exhaustive digital button subsets")
        }
        var random: UInt32 = 0x0D5E1A5E
        for _ in 0..<2048 {
            random = random &* 1664525 &+ 1013904223
            let actions = random & MacAction.allMask
            let sequence = random ^ 0xF00DCAFE
            let packet = try DualSenseV5Protocol.encode(actionMask: actions, sequence: sequence)
            check(Array(packet[16...]) == nativeReference(actions), "mixed state matches independent native pipeline")
            check(read32(packet, 8) == sequence, "all sequence bits preserved")
            check(read32(packet, 12) == referenceCRC(Array(packet[4..<12]) + Array(packet[16...])), "mixed frame CRC agrees with independent oracle")
        }
        for sequence: UInt32 in [0, 1, 255, 256, 65535, 65536, 0x12345678, .max] {
            let packet = try DualSenseV5Protocol.encode(actionMask: 0, sequence: sequence)
            check(read32(packet, 8) == sequence && packet.count == 49, "sequence boundaries remain exactly 49 bytes")
            check(Array(packet[16...]) == neutralState, "sequence cannot alter control payload")
            check(read32(packet, 12) == referenceCRC(Array(packet[4..<12]) + neutralState), "sequence covered by CRC")
        }
        check(try DualSenseV5Protocol.encode(actionMask: MacAction.allMask, enabled: false, sequence: 0) == neutral, "disabled all-actions golden neutral")
        for bit in 22..<32 { reject(UInt32(1) << bit); reject(UInt32(1) << bit, enabled: false) }
        reject(.max)
        var core = MacControlCore()
        core.updateEnvironment(foreground: true, connected: true)
        core.setEnabled(true)
        core.process(keyCode: core.profile[.cross].keyCode!, isDown: true, atMilliseconds: 0)
        check(try DualSenseV5Protocol.encode(actionMask: core.evaluate(atMilliseconds: 0), sequence: 0x12345678) == crossHeader + crossState, "actual core maps into golden Cross frame")
        core.updateEnvironment(foreground: false, connected: true)
        check(try DualSenseV5Protocol.encode(actionMask: core.evaluate(atMilliseconds: 1), sequence: 0) == neutral, "actual core focus loss produces golden neutral")
        print("DUALSENSE_V5_PROTOCOL_PASS checks=\(checks) state_bytes=33 frame_bytes=49 synthetic_only=true sockets_opened=false devices_created=false sony_accessed=false")
    }
}
