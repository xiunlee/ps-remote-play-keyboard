// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import Foundation

/// Names of physical Mac keys; these are never Windows virtual-key codes.
@MainActor
enum MacKeyNames {
    static func display(_ keyCode: UInt16?) -> String {
        guard let keyCode else { return MacText.t("Unassigned") }
        if let name = characters[keyCode] { return name }
        if let number = functionKeys[keyCode] { return "F\(number)" }
        if let name = keypad[keyCode] { return MacText.t("KeyNumPad") + " " + name }
        switch keyCode {
        case 0x24: return MacText.t("Return (Enter)")
        case 0x30: return MacText.t("KeyTab")
        case 0x31: return MacText.t("KeySpace")
        case 0x33: return "⌫ " + MacText.t("KeyBackspace")
        case 0x35: return MacText.t("KeyEscape")
        case 0x47: return "⌧"
        case 0x4C: return MacText.t("KeyNumPad") + " " + MacText.t("KeyEnter")
        case 0x66: return "英数"
        case 0x68: return "かな"
        case 0x72: return "Help"
        case 0x73: return MacText.t("KeyHome")
        case 0x74: return MacText.t("KeyPageUp")
        case 0x75: return "⌦ " + MacText.t("KeyDelete")
        case 0x77: return MacText.t("KeyEnd")
        case 0x79: return MacText.t("KeyPageDown")
        case 0x7B: return "←"
        case 0x7C: return "→"
        case 0x7D: return "↓"
        case 0x7E: return "↑"
        default: return MacText.t("KeyOther") + " " + String(format: "0x%02X", keyCode)
        }
    }

    private static let characters: [UInt16: String] = [
        0x00:"A", 0x01:"S", 0x02:"D", 0x03:"F", 0x04:"H", 0x05:"G",
        0x06:"Z", 0x07:"X", 0x08:"C", 0x09:"V", 0x0A:"§", 0x0B:"B",
        0x0C:"Q", 0x0D:"W", 0x0E:"E", 0x0F:"R", 0x10:"Y", 0x11:"T",
        0x12:"1", 0x13:"2", 0x14:"3", 0x15:"4", 0x16:"6", 0x17:"5",
        0x18:"=", 0x19:"9", 0x1A:"7", 0x1B:"−", 0x1C:"8", 0x1D:"0",
        0x1E:"]", 0x1F:"O", 0x20:"U", 0x21:"[", 0x22:"I", 0x23:"P",
        0x25:"L", 0x26:"J", 0x27:"'", 0x28:"K", 0x29:";", 0x2A:"\\",
        0x2B:",", 0x2C:"/", 0x2D:"N", 0x2E:"M", 0x2F:".", 0x32:"`",
        0x5D:"¥", 0x5E:"_"
    ]
    private static let functionKeys: [UInt16: Int] = [
        0x7A:1, 0x78:2, 0x63:3, 0x76:4, 0x60:5, 0x61:6, 0x62:7,
        0x64:8, 0x65:9, 0x6D:10, 0x67:11, 0x6F:12, 0x69:13, 0x6B:14,
        0x71:15, 0x6A:16, 0x40:17, 0x4F:18, 0x50:19, 0x5A:20
    ]
    private static let keypad: [UInt16: String] = [
        0x41:".", 0x43:"*", 0x45:"+", 0x4B:"/", 0x4E:"−", 0x51:"=",
        0x52:"0", 0x53:"1", 0x54:"2", 0x55:"3", 0x56:"4", 0x57:"5",
        0x58:"6", 0x59:"7", 0x5B:"8", 0x5C:"9", 0x5F:","
    ]
}

extension MacAction {
    @MainActor var displayName: String { MacText.t("Action\(rawValue)") }
}

extension MacRepeatMode {
    @MainActor var displayName: String {
        switch self {
        case .off: return MacText.t("RepeatOff")
        case .hold: return MacText.t("RepeatHold")
        case .toggle: return MacText.t("RepeatToggle")
        }
    }
}
