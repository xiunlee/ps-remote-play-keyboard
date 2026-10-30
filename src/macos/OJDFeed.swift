// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Independent stdin-schema adapter for the pinned OpenJoystickDriver CLI.
// No subprocess, pipe, keyboard, device, filesystem or clock access.
import Foundation

public enum OJDFeedProfile: String, CaseIterable, Sendable {
    case generic = "generic"
    case sdlCompatible = "sdl-compatible"
    case gameControllerCompatible = "game-controller-compatible"

    public var cliName: String { rawValue }
    public var cliArguments: [String] {
        ["--no-input", "--json", "--timeout", "5", "virtual", "feed", "--as", cliName]
    }
}

public enum OJDFeedError: Error, Equatable {
    case unknownActions(UInt32)
    case unsupportedActions(UInt32)
    case invalidFrameInterval
    case noWriteToAcknowledge
    case noEOFToAcknowledge
}

public struct OJDFeedFrame: Encodable, Equatable, Sendable {
    public let buttons: [String]
    public let dpad: [String]
    public let axes: [String: Double]
}

public enum OJDFeedEncoder {
    public static let upstreamCommit = "3552be925d7dacef3fd895ac0c6cbf39e9d3cd70"
    public static let unsupportedActions: UInt32 = MacAction.touchClick.mask
    public static let supportedActions: UInt32 = MacAction.allMask & ~unsupportedActions
    public static let minimumFrameMilliseconds: UInt64 = 8

    // All three pinned profiles carry the fields used here, including Guide.
    // Guide is an Xbox/generic control, NOT evidence that Sony treats it as PS.
    // TouchpadClick decodes upstream but is dropped by its report mapping, so
    // reject it here rather than writing a superficially valid no-op frame.
    public static func frame(actions: UInt32, profile: OJDFeedProfile) throws -> OJDFeedFrame {
        let unknown = actions & ~MacAction.allMask
        guard unknown == 0 else { throw OJDFeedError.unknownActions(unknown) }
        let unsupported = actions & unsupportedActions
        guard unsupported == 0 else { throw OJDFeedError.unsupportedActions(unsupported) }
        func held(_ action: MacAction) -> Bool { actions & action.mask != 0 }
        func difference(_ positive: MacAction, _ negative: MacAction) -> Int {
            (held(positive) ? 1 : 0) - (held(negative) ? 1 : 0)
        }
        let x = difference(.leftStickRight, .leftStickLeft)
        let y = difference(.leftStickDown, .leftStickUp)
        let scale = x != 0 && y != 0 ? sqrt(0.5) : 1.0
        let dx = difference(.dpadRight, .dpadLeft)
        let dy = difference(.dpadDown, .dpadUp)
        var dpad: [String] = []
        if dy < 0 { dpad.append("DpadUp") }
        if dy > 0 { dpad.append("DpadDown") }
        if dx < 0 { dpad.append("DpadLeft") }
        if dx > 0 { dpad.append("DpadRight") }
        let mapping: [(MacAction, String)] = [
            (.cross, "South"), (.circle, "East"), (.square, "West"), (.triangle, "North"),
            (.l1, "LeftShoulder"), (.r1, "RightShoulder"),
            (.l2, "LeftTrigger"), (.r2, "RightTrigger"),
            (.create, "Back"), (.options, "Start"),
            (.l3, "LeftStickClick"), (.r3, "RightStickClick"), (.ps, "Guide")
        ]
        return OJDFeedFrame(
            buttons: mapping.filter { held($0.0) }.map(\.1).sorted(),
            dpad: dpad.sorted(),
            axes: [
                "LeftStickX": Double(x) * scale, "LeftStickY": Double(y) * scale,
                "RightStickX": 0, "RightStickY": 0,
                "LeftTrigger": held(.l2) ? 1 : 0, "RightTrigger": held(.r2) ? 1 : 0
            ])
    }

    public static func line(actions: UInt32, profile: OJDFeedProfile) throws -> Data {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys, .withoutEscapingSlashes]
        var data = try encoder.encode(frame(actions: actions, profile: profile))
        data.append(0x0A)
        return data
    }
}

public enum OJDFeedCommand: Equatable, Sendable {
    case writeLine(Data)
    case closeStdin
}

// Optional pure scheduling contract for the caller's I/O layer. At most one
// line is outstanding; every successful write must be acknowledged before
// another command is produced. Latest sampled state is used without a replay
// queue or catch-up burst. This cannot prove any bytes reached an OJD service.
public struct OJDFeedSession: Sendable {
    public enum Phase: Equatable, Sendable {
        case notStarted, running, stopping, awaitingStdinClose, stdinClosed
    }
    public let profile: OJDFeedProfile
    public let frameIntervalMilliseconds: UInt64
    public private(set) var phase: Phase = .notStarted
    public private(set) var ioFailed = false
    public var hasPendingWrite: Bool { pendingWrite != nil }
    private enum WriteKind: Sendable { case initial, sample, finalNeutral }
    private var pendingWrite: WriteKind?
    private var lastSuccessfulWrite: UInt64?
    private var lastTime: UInt64 = 0
    private var stopRequested = false
    private var finalNeutralWritten = false
    private var closeOffered = false

    public init(profile: OJDFeedProfile, frameIntervalMilliseconds: UInt64 = 8) throws {
        guard frameIntervalMilliseconds >= OJDFeedEncoder.minimumFrameMilliseconds else {
            throw OJDFeedError.invalidFrameInterval
        }
        self.profile = profile
        self.frameIntervalMilliseconds = frameIntervalMilliseconds
    }

    public mutating func requestStop() {
        guard phase != .stdinClosed else { return }
        stopRequested = true
        if phase != .awaitingStdinClose { phase = .stopping }
    }

    public mutating func next(actions: UInt32, atMilliseconds sample: UInt64) throws -> OJDFeedCommand? {
        let now = monotonic(sample)
        guard phase != .stdinClosed, pendingWrite == nil, !closeOffered else { return nil }
        if ioFailed || finalNeutralWritten {
            closeOffered = true
            phase = .awaitingStdinClose
            return .closeStdin
        }
        if let last = lastSuccessfulWrite, now - last < frameIntervalMilliseconds { return nil }
        let initial = lastSuccessfulWrite == nil
        let mask = initial || stopRequested ? 0 : actions
        do {
            let data = try OJDFeedEncoder.line(actions: mask, profile: profile)
            pendingWrite = stopRequested ? .finalNeutral : initial ? .initial : .sample
            return .writeLine(data)
        } catch {
            // A rejected frame must not leave the previous held state running
            // while later samples are accepted. Caller continues stop/neutral.
            requestStop()
            throw error
        }
    }

    public mutating func writeSucceeded(atMilliseconds sample: UInt64) throws {
        guard let kind = pendingWrite else { throw OJDFeedError.noWriteToAcknowledge }
        lastSuccessfulWrite = monotonic(sample)
        pendingWrite = nil
        if kind == .finalNeutral { finalNeutralWritten = true }
        phase = stopRequested ? .stopping : .running
    }

    // Broken pipe / partial write / timeout means delivery and cleanup are
    // unconfirmed. No more JSON is attempted; caller receives closeStdin next.
    public mutating func writeFailed() {
        guard phase != .stdinClosed else { return }
        ioFailed = true
        stopRequested = true
        pendingWrite = nil
        if !closeOffered { phase = .stopping }
    }

    public mutating func acknowledgeStdinClosed() throws {
        guard closeOffered && phase == .awaitingStdinClose else {
            throw OJDFeedError.noEOFToAcknowledge
        }
        phase = .stdinClosed
    }

    private mutating func monotonic(_ sample: UInt64) -> UInt64 {
        lastTime = max(lastTime, sample)
        return lastTime
    }
}
