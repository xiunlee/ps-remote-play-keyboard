// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Pure 64-byte USB input encoder. No device, feature report, keyboard, clock,
// Sony, file or network APIs. This is not a V5 network frame or a HID adapter.
// Field contract: fixed VIIPER b78e31e93e84b4cb7c4b4c15b8aace10749106d2
// device/dualsense/input_scheduler.go; action order: keyboard_merge.h.

public enum DualSenseUSBReportError: Error, Equatable {
    case unknownActionBits(UInt32)
}

public enum DualSenseUSBReport {
    public static let size = 64
    public static let knownActionMask: UInt32 = (1 << 22) - 1

    // The caller owns the session counters and supplies elapsed monotonic
    // microseconds. Building a report consumes no counters and changes no
    // state. A future output adapter must commit counters after its own
    // successful-send contract and discard uncertain sessions on failure.
    public static func encode(actionMask: UInt32, enabled: Bool = true,
                              sequence: UInt8, packetSequence: UInt32,
                              timestampMicroseconds: UInt64) throws -> [UInt8] {
        guard actionMask & ~knownActionMask == 0 else {
            throw DualSenseUSBReportError.unknownActionBits(actionMask & ~knownActionMask)
        }
        let actions = enabled ? actionMask : 0
        func down(_ bit: Int) -> Int { actions & (UInt32(1) << bit) == 0 ? 0 : 1 }
        var report = [UInt8](repeating: 0, count: size)
        report[0] = 0x01
        report[1] = 128; report[2] = 128
        report[3] = 128; report[4] = 128 // No right-stick direction mapping.

        let x = down(3) - down(2)
        let y = down(1) - down(0)
        if x != 0 && y != 0 {
            report[1] = UInt8(128 + x * 90)
            report[2] = UInt8(128 + y * 90)
        } else {
            report[1] = x < 0 ? 0 : x > 0 ? 255 : 128
            report[2] = y < 0 ? 0 : y > 0 ? 255 : 128
        }
        let hatX = down(7) - down(6)
        let hatY = down(5) - down(4)
        let hat: UInt8
        if hatY < 0 { hat = hatX < 0 ? 7 : hatX > 0 ? 1 : 0 }
        else if hatY > 0 { hat = hatX < 0 ? 5 : hatX > 0 ? 3 : 4 }
        else { hat = hatX < 0 ? 6 : hatX > 0 ? 2 : 8 }
        report[8] = hat

        // DS4 native output puts face/hat at byte 5, shoulders at 6, PS/touch
        // at 7, triggers at 8/9. DualSense USB places them at 8, 9, 10, 5/6.
        for (action, mask): (Int, UInt8) in [(10, 0x10), (8, 0x20), (9, 0x40), (11, 0x80)] {
            if down(action) != 0 { report[8] |= mask }
        }
        for action in 12...19 {
            if down(action) != 0 { report[9] |= UInt8(1) << (action - 12) }
        }
        if down(20) != 0 { report[10] |= 1 }
        if down(21) != 0 { report[10] |= 2 }
        if down(14) != 0 { report[5] = 255 }
        if down(15) != 0 { report[6] = 255 }

        report[7] = sequence
        putUInt32(packetSequence, into: &report, at: 12)
        // Neutral gyro=0, accel=(0, 0, -8192), little endian.
        report[26] = 0x00; report[27] = 0xe0
        // Truncate before wrapping multiplication: equivalent to the whole
        // elapsed-microseconds * 3 modulo 2^32, even near UInt64.max.
        let ticks = UInt32(truncatingIfNeeded: timestampMicroseconds) &* 3
        putUInt32(ticks, into: &report, at: 28)
        report[33] = 0x80; report[37] = 0x80 // No active touch contacts.
        report[42] = 9; report[43] = 9 // Neutral adaptive-trigger status.
        putUInt32(ticks, into: &report, at: 49) // Base DualSense, not Edge.
        report[53] = 0x2a // Synthetic fully-charged status; no battery reading.
        report[54] = 0x08 // Synthetic wired transport; no physical metadata.
        return report
    }

    private static func putUInt32(_ value: UInt32, into bytes: inout [UInt8], at offset: Int) {
        for index in 0..<4 { bytes[offset + index] = UInt8(truncatingIfNeeded: value >> (index * 8)) }
    }
}
