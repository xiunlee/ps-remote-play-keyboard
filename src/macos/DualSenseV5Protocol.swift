// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Pure keyboard action -> 33-byte control state -> 49-byte VPCM v5 frame.
// No USB report, physical input, network, device, UI, clock or sequence owner.
// References: research/native-merge/keyboard_merge.c,
// src/keyboard-pad/{KeyboardMapping,ViiperTransport}.cs,
// research/dualsense-v5-test/V5Protocol.cs and its fixed golden fixtures.

public enum DualSenseV5ProtocolError: Error, Equatable {
    case unknownActionBits(UInt32)
}

public enum DualSenseV5Protocol {
    public static let upstreamCommit = "b78e31e93e84b4cb7c4b4c15b8aace10749106d2"
    public static let deviceType = "dualsensegamepadv5"
    public static let headerSize = 16
    public static let inputSize = 33
    public static let frameSize = headerSize + inputSize
    public static let knownActionMask = MacAction.allMask

    public static func controlState(actionMask: UInt32, enabled: Bool = true) throws -> [UInt8] {
        let unknown = actionMask & ~knownActionMask
        guard unknown == 0 else { throw DualSenseV5ProtocolError.unknownActionBits(unknown) }
        let actions = enabled ? actionMask : 0
        func held(_ action: MacAction) -> Int { actions & action.mask == 0 ? 0 : 1 }
        var state = [UInt8](repeating: 0, count: inputSize)
        let x = held(.leftStickRight) - held(.leftStickLeft)
        let y = held(.leftStickDown) - held(.leftStickUp)
        if x != 0 && y != 0 {
            state[0] = UInt8(bitPattern: Int8(x * 90))
            state[1] = UInt8(bitPattern: Int8(y * 90))
        } else {
            state[0] = x < 0 ? 0x80 : x > 0 ? 0x7F : 0
            state[1] = y < 0 ? 0x80 : y > 0 ? 0x7F : 0
        }
        // Right stick stays at signed zero. The native DS4 converter first
        // maps buttons to 16 bits, then moves PS/touch to bits 16/17 for V5.
        let buttonMap: [(MacAction, UInt32)] = [
            (.square, 0x10), (.cross, 0x20), (.circle, 0x40), (.triangle, 0x80),
            (.l1, 0x100), (.r1, 0x200), (.l2, 0x400), (.r2, 0x800),
            (.create, 0x1000), (.options, 0x2000), (.l3, 0x4000), (.r3, 0x8000),
            (.ps, 0x10000), (.touchClick, 0x20000)
        ]
        var buttons: UInt32 = 0
        for (action, button) in buttonMap where held(action) != 0 { buttons |= button }
        putUInt32(buttons, into: &state, at: 4)
        let dx = held(.dpadRight) - held(.dpadLeft)
        let dy = held(.dpadDown) - held(.dpadUp)
        // V5 state D-pad is directional flags, not the USB report's 0...8 hat.
        if dy < 0 { state[8] |= 1 }
        if dy > 0 { state[8] |= 2 }
        if dx < 0 { state[8] |= 4 }
        if dx > 0 { state[8] |= 8 }
        if held(.l2) != 0 { state[9] = 255 }
        if held(.r2) != 0 { state[10] = 255 }
        state[15] = 0x80; state[20] = 0x80 // Inactive packed touch contacts.
        state[32] = 0xE0 // AccelZ = -8192 little endian; all other motion zero.
        return state
    }

    // Sequence is supplied by the owner of one stream. This method is pure:
    // it neither increments nor retries. After an uncertain write, discard
    // that stream; reconnect with a new owner and sequence zero.
    public static func encode(actionMask: UInt32, enabled: Bool = true,
                              sequence: UInt32) throws -> [UInt8] {
        let state = try controlState(actionMask: actionMask, enabled: enabled)
        var frame = [UInt8](repeating: 0, count: frameSize)
        frame[0...3] = [0x56, 0x50, 0x43, 0x4D] // VPCM
        frame[4] = 5; frame[5] = 1
        frame[6] = UInt8(inputSize) // UInt16 little endian; byte 7 remains zero.
        putUInt32(sequence, into: &frame, at: 8)
        frame.replaceSubrange(headerSize..<frameSize, with: state)
        // IEEE CRC covers version/type/length/sequence then payload. Magic
        // and the CRC field itself are excluded, matching the C# wire codec.
        putUInt32(crc32IEEE(Array(frame[4..<12]) + state), into: &frame, at: 12)
        return frame
    }

    static func crc32IEEE(_ bytes: [UInt8]) -> UInt32 {
        var crc = UInt32.max
        for byte in bytes {
            crc ^= UInt32(byte)
            for _ in 0..<8 { crc = (crc >> 1) ^ (crc & 1 == 0 ? 0 : 0xEDB88320) }
        }
        return ~crc
    }

    private static func putUInt32(_ value: UInt32, into bytes: inout [UInt8], at offset: Int) {
        for i in 0..<4 { bytes[offset + i] = UInt8(truncatingIfNeeded: value >> (i * 8)) }
    }
}
