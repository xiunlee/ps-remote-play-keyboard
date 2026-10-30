// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import Foundation

struct RuntimeFailure: Error, CustomStringConvertible, Sendable {
    let code: String
    init(_ code: String) { self.code = code }
    var description: String { code }
}

enum RuntimeWire {
    static let greeting = Array("PSKeyboard-DualSenseV5-Relay/1\n".utf8)
    static let commit = "b78e31e93e84b4cb7c4b4c15b8aace10749106d2"
    static let neutral: [UInt8] = {
        var value = [UInt8](repeating: 0, count: 33)
        value[15] = 128; value[20] = 128; value[32] = 224
        return value
    }()
    static func uint32(_ bytes: [UInt8], _ at: Int) -> UInt32 {
        (0..<4).reduce(0) { $0 | UInt32(bytes[at + $1]) << ($1 * 8) }
    }
    static func crc(_ bytes: [UInt8]) -> UInt32 {
        var value = UInt32.max
        for byte in bytes {
            value ^= UInt32(byte)
            for _ in 0..<8 { value = (value >> 1) ^ (value & 1 == 0 ? 0 : 0xEDB88320) }
        }
        return ~value
    }
    static func frame(payload: [UInt8], sequence: UInt32, kind: UInt8 = 1) -> [UInt8] {
        var bytes: [UInt8] = [86, 80, 67, 77, 5, kind, UInt8(truncatingIfNeeded: payload.count), UInt8(payload.count >> 8)]
        bytes += (0..<4).map { UInt8(truncatingIfNeeded: sequence >> ($0 * 8)) }
        let checksum = crc(Array(bytes[4..<12]) + payload)
        bytes += (0..<4).map { UInt8(truncatingIfNeeded: checksum >> ($0 * 8)) }
        return bytes + payload
    }
    static func validateInput(_ payload: [UInt8]) throws {
        guard payload.count == 33 else { throw RuntimeFailure("input_length") }
        let buttons = uint32(payload, 4)
        guard buttons & ~0x0003FFF0 == 0, payload[8] & 0xF0 == 0 else { throw RuntimeFailure("input_reserved_controls") }
        guard payload[2] == 0, payload[3] == 0 else { throw RuntimeFailure("input_right_stick") }
        guard payload[11..<15].allSatisfy({$0 == 0}), payload[16..<20].allSatisfy({$0 == 0}),
              payload[15] == 128, payload[20] == 128 else { throw RuntimeFailure("input_touch_state") }
        guard payload[21..<32].allSatisfy({$0 == 0}), payload[32] == 224 else { throw RuntimeFailure("input_motion_state") }
        guard payload[9] == (buttons & 0x400 == 0 ? 0 : 255), payload[10] == (buttons & 0x800 == 0 ? 0 : 255)
        else { throw RuntimeFailure("input_trigger_mismatch") }
    }
}

struct RuntimeFrameParser {
    let feedback: Bool
    private var bytes: [UInt8] = []
    private var expected: UInt32 = 0
    private var partialSince: Double?
    private var failed = false
    init(feedback: Bool = false) { self.feedback = feedback }
    mutating func checkDeadline(_ now: Double) throws {
        if let partialSince, now - partialSince >= 1.5 { failed = true; throw RuntimeFailure("frame_deadline") }
    }
    mutating func feed(_ data: [UInt8], at now: Double) throws -> [[UInt8]] {
        do {
            guard !failed else { throw RuntimeFailure("parser_closed") }
            try checkDeadline(now)
            guard !data.isEmpty else { throw RuntimeFailure(feedback ? "feedback_eof" : "frontend_eof") }
            guard bytes.count + data.count <= 65536 else { throw RuntimeFailure("frame_buffer_limit") }
            if bytes.isEmpty { partialSince = now }
            bytes += data
            var result: [[UInt8]] = []
            while bytes.count >= 16 {
                let size = Int(bytes[6]) | Int(bytes[7]) << 8
                let expectedSize = feedback ? [UInt8(0x81):474, 0x83:2396, 0x84:474][bytes[5]] : (bytes[5] == 1 ? 33 : nil)
                guard bytes[0..<4].elementsEqual([86,80,67,77]), bytes[4] == 5, expectedSize == size else { throw RuntimeFailure("frame_header") }
                guard RuntimeWire.uint32(bytes, 8) == expected else { throw RuntimeFailure("frame_sequence") }
                if bytes.count < 16 + size { break }
                let payload = Array(bytes[16..<(16 + size)])
                guard RuntimeWire.uint32(bytes, 12) == RuntimeWire.crc(Array(bytes[4..<12]) + payload) else { throw RuntimeFailure("frame_crc") }
                if feedback {
                    if bytes[5] == 0x83 && !payload.prefix(2).elementsEqual([218,1]) { throw RuntimeFailure("feedback_atomic_boundary") }
                } else { try RuntimeWire.validateInput(payload) }
                result.append(payload); bytes.removeFirst(16 + size); expected &+= 1
                partialSince = bytes.isEmpty ? nil : now
            }
            return result
        } catch { failed = true; throw error }
    }
}

/// Healthy sessions have no lifetime cap. Only a missing input heartbeat expires.
struct RuntimeFeedClock {
    private var lastNow: Double
    private var lastInput: Double?
    private var connected = false
    private var nextOutput: Double
    private var sequence: UInt32 = 0
    private var latest = RuntimeWire.neutral
    init(now: Double) { lastNow = now; nextOutput = now }
    mutating func connect(at now: Double) throws {
        try check(now)
        guard !connected else { throw RuntimeFailure("second_frontend") }
        connected = true; lastInput = now
    }
    mutating func input(_ payload: [UInt8], at now: Double) throws {
        try check(now)
        guard connected else { throw RuntimeFailure("input_before_connect") }
        latest = payload; lastInput = now
    }
    mutating func check(_ now: Double) throws {
        guard now >= lastNow else { throw RuntimeFailure("clock_reversed") }
        lastNow = now
        if let lastInput, now - lastInput >= 0.250 { throw RuntimeFailure("frontend_stale") }
    }
    mutating func tick(_ now: Double) throws -> [UInt8]? {
        try check(now)
        guard now >= nextOutput else { return nil }
        let frame = RuntimeWire.frame(payload: connected ? latest : RuntimeWire.neutral, sequence: sequence)
        sequence &+= 1
        nextOutput += (floor((now - nextOutput) / 0.008) + 1) * 0.008
        return frame
    }
}

struct RuntimeVHCIImport: Equatable {
    let port:Int
    let localBusID:String
    /// Bounded metadata diagnostics. Never includes kernel socket pointers,
    /// serials, USB payloads, shell output or any other unrecognized columns.
    static func safeRows(_ status:String) -> [[String:String]] {
        status.split(whereSeparator:{$0.isNewline}).compactMap {line -> [String:String]? in
            let fields=line.split(whereSeparator:{$0 == " " || $0 == "\t" || $0 == "\r"})
            guard fields.count >= 5,fields.first == "hs" || fields.first == "ss",
                  let port=Int(fields[1]),let state=Int(fields[2]),let speed=Int(fields[3]),let remote=UInt32(fields[4],radix:16) else {return nil}
            let local=fields.count == 7 ? String(fields[6]) : "missing"
            let safeLocal=local.range(of:"^[0-9]+-[0-9]+(?:\\.[0-9]+)*$",options:.regularExpression) != nil ? local : "invalid"
            return ["hub":String(fields[0]),"port":String(port),"state":String(state),"speed":String(speed),"remoteID":String(format:"%08x",remote),"localBusID":safeLocal]
        }.prefix(32).map{$0}
    }
    /// Linux vhci status records the remote bus/dev pair as one hexadecimal
    /// value. This does not depend on usbip's optional /run bookkeeping text.
    static func matching(_ status:String,bus:UInt32,device:UInt32) throws -> Self? {
        guard bus <= 65535,device <= 65535 else {throw RuntimeFailure("usbip_remote_identity_range")}
        let expected = (bus << 16) | device
        var matches:[Self] = []
        for line in status.split(whereSeparator:{$0.isNewline}) {
            let fields = line.split(whereSeparator:{$0 == " " || $0 == "\t" || $0 == "\r"})
            guard fields.first == "hs" || fields.first == "ss" else {continue}
            guard fields.count >= 5, let state=Int(fields[2]) else {throw RuntimeFailure("usbip_status_format")}
            guard state == 6 else {continue} // Linux VDEV_ST_USED.
            guard fields.count == 7,let remote=UInt32(fields[4],radix:16),let port=Int(fields[1]),port >= 0,port < 65536 else {throw RuntimeFailure("usbip_status_format")}
            guard remote == expected else {continue}
            let local=String(fields[6])
            guard local.range(of:"^[0-9]+-[0-9]+(?:\\.[0-9]+)*$",options:.regularExpression) != nil else {throw RuntimeFailure("usbip_local_identity")}
            matches.append(Self(port:port,localBusID:local))
        }
        guard matches.count <= 1 else {throw RuntimeFailure("usbip_ambiguous_import")}
        return matches.first
    }
}
