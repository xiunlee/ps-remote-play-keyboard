// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import Darwin
import Dispatch
import Foundation

/// Owns one pipe writer. After initialization, use this wrapper for every write/close.
/// Closing stdin is a local fail-safe; it does not prove that OJD removed its device.
final class PreviewPipe: @unchecked Sendable {
    private let writer: FileHandle
    private let descriptor: Int32
    private let lock = NSLock()
    private var closed = false
    private var lastPulse: UInt64
    private var watchdog: DispatchSourceTimer?
    private let watchdogQueue = DispatchQueue(label: "pskeyboard.preview-pipe-watchdog", qos: .userInitiated)
    private static let staleNanoseconds: UInt64 = 250_000_000

    /// Takes ownership of the writer, including closing it if configuration fails.
    init(writer: FileHandle, now: UInt64 = DispatchTime.now().uptimeNanoseconds) throws {
        self.writer = writer
        descriptor = writer.fileDescriptor
        lastPulse = now
        do {
            let status = fcntl(descriptor, F_GETFL)
            guard status >= 0, fcntl(descriptor, F_SETFL, status | O_NONBLOCK) == 0 else {
                throw Self.currentError()
            }
            // Suppress SIGPIPE only on our fd; do not change the app's signal handlers.
            guard fcntl(descriptor, F_SETNOSIGPIPE, 1) == 0 else { throw Self.currentError() }
            let flags = fcntl(descriptor, F_GETFD)
            guard flags >= 0, fcntl(descriptor, F_SETFD, flags | FD_CLOEXEC) == 0 else {
                throw Self.currentError()
            }
        } catch {
            closed = true
            try? writer.close()
            throw error
        }
    }

    deinit { shutdown() }

    var isClosed: Bool {
        lock.lock(); defer { lock.unlock() }
        return closed
    }

    /// One atomic, nonblocking pipe write. Never queue or retry a stale/partial frame.
    @discardableResult
    func write(frame: Data) -> Bool {
        lock.lock(); defer { lock.unlock() }
        guard !closed else { return false }
        guard !frame.isEmpty, frame.count <= 512 else {
            closeLocked()
            return false
        }
        let count = frame.withUnsafeBytes { Darwin.write(descriptor, $0.baseAddress, $0.count) }
        guard count == frame.count else {
            closeLocked()
            return false
        }
        return true
    }

    /// Call from the main-thread tick. Writes deliberately do not count as a pulse.
    func pulse(at now: UInt64 = DispatchTime.now().uptimeNanoseconds) {
        lock.lock(); defer { lock.unlock() }
        if !closed, now >= lastPulse { lastPulse = now }
    }

    func check(now: UInt64 = DispatchTime.now().uptimeNanoseconds) {
        lock.lock(); defer { lock.unlock() }
        if !closed, now >= lastPulse, now - lastPulse >= Self.staleNanoseconds {
            closeLocked()
        }
    }

    /// Runs independently of AppKit's main run loop. Starting twice is harmless.
    func startWatchdog() {
        lock.lock(); defer { lock.unlock() }
        guard !closed, watchdog == nil else { return }
        let timer = DispatchSource.makeTimerSource(queue: watchdogQueue)
        timer.schedule(deadline: .now() + .milliseconds(50), repeating: .milliseconds(50))
        timer.setEventHandler { [weak self] in self?.check() }
        watchdog = timer
        timer.resume()
    }

    func close() {
        lock.lock(); defer { lock.unlock() }
        closeLocked()
    }

    func shutdown() { close() }

    private func closeLocked() {
        guard !closed else { return }
        closed = true
        watchdog?.cancel()
        watchdog = nil
        try? writer.close()
    }

    private static func currentError() -> POSIXError {
        POSIXError(POSIXErrorCode(rawValue: errno) ?? .EIO)
    }
}
