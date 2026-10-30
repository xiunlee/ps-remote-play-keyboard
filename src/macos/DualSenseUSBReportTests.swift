// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Compile with the original keyboard_merge.h bridge and keyboard_merge.c.
// Synthetic C oracle plus independent USB byte expectations; no HID access.
@main
struct DualSenseUSBReportTests {
    static func main() throws {
        var checks = 0
        func check(_ condition: Bool, _ label: String) {
            checks += 1
            precondition(condition, label)
        }
        func frame(_ actions: UInt32 = 0, enabled: Bool = true, seq: UInt8 = 0,
                   packet: UInt32 = 0, micros: UInt64 = 0) throws -> [UInt8] {
            try DualSenseUSBReport.encode(actionMask: actions, enabled: enabled,
                sequence: seq, packetSequence: packet, timestampMicroseconds: micros)
        }
        func nativeDS4(_ actions: UInt32, enabled: Bool = true) -> [UInt8] {
            var input = [UInt8](repeating: 0, count: 64)
            input[0] = 1; input[1] = 128; input[2] = 128
            input[3] = 128; input[4] = 128; input[5] = 8
            var output = [UInt8](repeating: 0, count: 64)
            var state = PskbKeyboard(held: actions, enabled: enabled ? 1 : 0)
            let result = input.withUnsafeBufferPointer { source in
                output.withUnsafeMutableBufferPointer { destination in
                    pskb_merge_keyboard(source.baseAddress, source.count, &state,
                                        destination.baseAddress, destination.count)
                }
            }
            precondition(result == PSKB_MERGE_OK, "native oracle failure")
            return output
        }
        func compareNative(_ actions: UInt32) throws {
            let ds4 = nativeDS4(actions), ds5 = try frame(actions)
            check(Array(ds5[1...4]) == Array(ds4[1...4]), "native axes parity")
            check(ds5[5] == ds4[8] && ds5[6] == ds4[9], "native analog trigger relocation")
            check(ds5[8] == ds4[5] && ds5[9] == ds4[6], "native face hat shoulder relocation")
            check(ds5[10] == ds4[7] & 3, "native PS/touch relocation")
        }
        let neutral = try frame()
        check(neutral.count == 64 && neutral[0] == 1, "USB report 1 exact length")
        check(Array(neutral[1...4]) == [128, 128, 128, 128], "neutral axes")
        check(neutral[8] == 8, "neutral hat null state")
        check(Array(neutral[16...27]) == [0,0,0,0,0,0,0,0,0,0,0,0xe0], "neutral gravity")
        check(neutral[33] == 0x80 && neutral[37] == 0x80, "inactive touches")
        check(neutral[42] == 9 && neutral[43] == 9, "neutral trigger metadata")
        check(neutral[53] == 0x2a && neutral[54] == 8, "synthetic battery/wired metadata")
        check(Array(neutral[56...63]) == [UInt8](repeating: 0, count: 8), "no fabricated device authentication")
        let expected: [(Int, UInt8)] = [
            (2,0), (2,255), (1,0), (1,255),
            (8,0), (8,4), (8,6), (8,2),
            (8,0x28), (8,0x48), (8,0x18), (8,0x88),
            (9,1), (9,2), (9,4), (9,8),
            (9,0x10), (9,0x20), (9,0x40), (9,0x80), (10,1), (10,2)
        ]
        for bit in 0..<22 {
            let mask = UInt32(1) << bit
            let report = try frame(mask)
            var wanted = neutral
            wanted[expected[bit].0] = expected[bit].1
            if bit == 14 { wanted[5] = 255 }
            if bit == 15 { wanted[6] = 255 }
            check(report == wanted, "individual action exact 64-byte expectation")
            check(try frame(0) == neutral, "release restores all neutral fields")
            check(try frame(mask, enabled: false) == neutral, "disabled action stays neutral")
            try compareNative(mask)
        }
        // All four-key stick and hat combinations, including opposite keys.
        for mask: UInt32 in 0..<16 {
            try compareNative(mask)
            try compareNative(mask << 4)
        }
        check(try frame(0b1111) == neutral, "opposing stick axes cancel")
        check(try frame(0b1111_0000) == neutral, "opposing hat axes cancel")
        check(Array(try frame(0b0101)[1...2]) == [38,38], "up-left normalized diagonal")
        check(Array(try frame(0b1010)[1...2]) == [218,218], "down-right normalized diagonal")
        for left in 0..<22 {
            for right in 0..<22 { try compareNative((1 << left) | (1 << right)) }
        }
        var random: UInt32 = 0x13579bdf
        for _ in 0..<512 {
            random = random &* 1_664_525 &+ 1_013_904_223
            try compareNative(random & DualSenseUSBReport.knownActionMask)
        }
        let ordered = try frame(seq: 0xa5, packet: 0x12345678, micros: 0x11223344)
        check(ordered[7] == 0xa5, "byte sequence")
        check(Array(ordered[12...15]) == [0x78,0x56,0x34,0x12], "packet counter little endian")
        check(Array(ordered[28...31]) == [0xcc,0x99,0x66,0x33], "three ticks per microsecond")
        check(Array(ordered[49...52]) == Array(ordered[28...31]), "base controller timer2 matches")
        let maxCounters = try frame(seq: UInt8.max, packet: UInt32.max, micros: UInt64.max)
        check(maxCounters[7] == 255 && Array(maxCounters[12...15]) == [255,255,255,255], "maximum counters preserved")
        check(Array(maxCounters[28...31]) == [0xfd,0xff,0xff,0xff], "timestamp wraps without UInt64 overflow")
        let next = try frame(seq: UInt8.max &+ 1, packet: UInt32.max &+ 1, micros: 0x1_0000_0000)
        check(next == neutral, "caller counter and clock wraps encode neutral")
        check(try frame(micros: 1_431_655_765)[28] == 0xff, "timestamp before 32-bit wrap")
        check(Array(try frame(micros: 1_431_655_766)[28...31]) == [2,0,0,0], "timestamp after 32-bit wrap")
        check(try frame(1 << 8) == frame(1 << 8), "encoding does not consume session state")
        for invalid: UInt32 in [1 << 22, 1 << 31, UInt32.max] {
            for enabled in [true, false] {
                do {
                    _ = try frame(invalid, enabled: enabled)
                    preconditionFailure("unknown action bits accepted")
                } catch let error as DualSenseUSBReportError {
                    check(error == .unknownActionBits(invalid & ~DualSenseUSBReport.knownActionMask),
                          "unknown bits fail even when disabled")
                }
            }
        }
        print("DUALSENSE_USB_REPORT_PASS checks=\(checks) original_native_oracle=true synthetic_only=true device_created=false sony_tested=false")
    }
}
