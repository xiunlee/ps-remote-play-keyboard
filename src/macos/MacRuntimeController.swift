// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import Foundation
import Darwin

struct MacRuntimeError: LocalizedError {
    let code: String
    var errorDescription: String? { code }
}

@MainActor
final class MacRuntimeController {
    var onFailure: ((String) -> Void)?
    private(set) var cleanupConfirmed = true
    private var process: Process?
    private var input: Pipe?
    private var output: Pipe?
    private var pending = Data()
    private var generation: UUID?
    private var readyContinuation: CheckedContinuation<Void,Error>?
    private var progress: ((String) -> Void)?
    private var stopping = false
    private var reportedFailure = false
    private var finalError: String?
    private var stopTask: Task<Void,Never>?

    func start(progress: @escaping (String) -> Void) async throws {
        guard process == nil,stopTask == nil else { throw MacRuntimeError(code:"already_running") }
        try Task.checkCancellation()
        let id = UUID(); generation = id; stopping = false; reportedFailure = false
        finalError = nil; pending = Data(); self.progress = progress
        let root = Bundle.main.bundleURL.appendingPathComponent("Contents")
        let helper = root.appendingPathComponent("Helpers/PSKeyboardRuntime")
        guard FileManager.default.isExecutableFile(atPath:helper.path) else { throw MacRuntimeError(code:"runtime_components_missing") }
        let child = Process(); let stdin = Pipe(); let stdout = Pipe()
        child.executableURL = helper
        child.arguments = [root.appendingPathComponent("Resources/Runtime").path,
                           root.appendingPathComponent("Helpers").path,
                           FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/PSKeyboard").path]
        child.standardInput = stdin; child.standardOutput = stdout; child.standardError = FileHandle.nullDevice
        process = child; input = stdin; output = stdout
        stdout.fileHandleForReading.readabilityHandler = { [weak self] handle in
            let bytes = handle.availableData
            Task { @MainActor [weak self] in self?.consume(bytes,generation:id) }
        }
        child.terminationHandler = { [weak self] child in
            let status = child.terminationStatus
            Task { @MainActor [weak self] in self?.terminated(status:status,generation:id) }
        }
        try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { continuation in
                readyContinuation = continuation
                do {
                    cleanupConfirmed = false
                    try child.run()
                    try? stdout.fileHandleForWriting.close(); try? stdin.fileHandleForReading.close()
                } catch {
                    cleanupConfirmed = true // No child was started.
                    readyContinuation = nil; clear(); continuation.resume(throwing:error)
                }
            }
        } onCancel: {
            Task { @MainActor [weak self] in
                guard self?.generation == id else { return }
                await self?.stop()
            }
        }
        try Task.checkCancellation()
        guard generation == id, !stopping, process?.isRunning == true else { throw MacRuntimeError(code:"cancelled") }
    }
    func stop() async {
        if let stopTask { await stopTask.value; return }
        guard let child = process else { return }
        let id = generation
        stopping = true
        readyContinuation?.resume(throwing:CancellationError()); readyContinuation = nil
        try? input?.fileHandleForWriting.close()
        let task = Task { @MainActor in
            // Explicit non-cancellable delay keeps cancellation from busy-spinning
            // the main actor while cleanup/termination callbacks need to run.
            let deadline = ContinuousClock.now + .seconds(45)
            while child.isRunning && ContinuousClock.now < deadline { await Self.delay() }
            if child.isRunning {
                child.terminate()
                let grace = ContinuousClock.now + .seconds(3)
                while child.isRunning && ContinuousClock.now < grace { await Self.delay() }
                if child.isRunning { _ = kill(child.processIdentifier,SIGKILL) }
            }
            // Let the termination callback consume the final structured stopped
            // record before clearing handles. No receipt is inferred from exit 0.
            let finalStatusDeadline = ContinuousClock.now + .seconds(1)
            while self.generation == id && !child.isRunning && ContinuousClock.now < finalStatusDeadline { await Self.delay() }
        }
        stopTask = task
        await task.value
        if generation == id { clear() }
        stopTask = nil
    }
    private static func delay() async {
        await withCheckedContinuation { continuation in DispatchQueue.main.asyncAfter(deadline:.now()+0.05) {continuation.resume()} }
    }
    private func consume(_ bytes:Data,generation id:UUID) {
        guard generation == id else { return }
        if bytes.isEmpty { output?.fileHandleForReading.readabilityHandler = nil; return }
        pending.append(bytes)
        guard pending.count <= 65536 else {
            finalError = "runtime_status_invalid"
            Task { await stop() }; return
        }
        while let newline = pending.firstIndex(of:10) {
            let line = pending[..<newline]; pending.removeSubrange(...newline)
            guard let record = try? JSONSerialization.jsonObject(with:line) as? [String:String],
                  let event = record["event"],let value = record["value"] else { continue }
            switch event {
            case "progress": if !stopping { progress?(value) }
            case "ready":
                if !stopping { readyContinuation?.resume(); readyContinuation = nil }
            case "error": finalError = value
            case "stopped": cleanupConfirmed = true
            case "cleanup": cleanupConfirmed = value == "confirmed"
            default: break
            }
        }
    }
    private func terminated(status:Int32,generation id:UUID) {
        guard generation == id else { return }
        // Consume already-buffered final status before deciding the reason.
        if let handle = output?.fileHandleForReading {
            handle.readabilityHandler = nil
            if let bytes = try? handle.readToEnd(), !bytes.isEmpty { consume(bytes,generation:id) }
        }
        let intentional = stopping
        let error = MacRuntimeError(code:finalError ?? (status == 0 ? "runtime_ended" : "runtime_failure"))
        readyContinuation?.resume(throwing:error); readyContinuation = nil
        if !intentional && !reportedFailure {
            reportedFailure = true; onFailure?(error.code)
        }
        clear()
    }
    private func clear() {
        output?.fileHandleForReading.readabilityHandler = nil
        try? input?.fileHandleForWriting.close(); try? output?.fileHandleForReading.close()
        process = nil; input = nil; output = nil; generation = nil; progress = nil
    }
}
