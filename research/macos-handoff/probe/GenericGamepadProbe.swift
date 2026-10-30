// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Research candidate. Default path is pure memory only. Live mode was NOT run.
import Foundation
import CoreHID
import AppKit
import Security
import Darwin

struct ProbeFailure: Error, CustomStringConvertible {
    let description: String
    init(_ message: String) { description = message }
}

enum Contract {
    static let product = "PSKeyboard isolated generic HID probe"
    // Experimental, local-only identity. Not an allocated USB VID; never Sony.
    static let vendor: UInt32 = 0
    static let productID: UInt32 = 0x5053
    // Game pad: report 1, button 1, seven padding bits, signed X/Y axes.
    static let descriptor = Data([
        0x05,0x01, 0x09,0x05, 0xA1,0x01, 0x85,0x01,
        0x05,0x09, 0x09,0x01, 0x15,0x00, 0x25,0x01,
        0x75,0x01, 0x95,0x01, 0x81,0x02,
        0x75,0x07, 0x95,0x01, 0x81,0x03,
        0x05,0x01, 0x09,0x30, 0x09,0x31, 0x15,0x81, 0x25,0x7F,
        0x75,0x08, 0x95,0x02, 0x81,0x02, 0xC0
    ])
    static let neutral = Data([1, 0, 0, 0])
    static let pressed = Data([1, 1, 0, 0])
    static let reports = [neutral, pressed, neutral]

    static func selfTest() throws {
        guard descriptor.prefix(6) == Data([0x05,0x01,0x09,0x05,0xA1,0x01]),
              descriptor.last == 0xC0,
              reports.allSatisfy({ $0.count == 4 && $0[0] == 1 }),
              reports[0] == reports[2], reports[1][1] == 1,
              reports.allSatisfy({ $0[2] == 0 && $0[3] == 0 }) else {
            throw ProbeFailure("pure report contract failed")
        }
    }
}

@available(macOS 15, *)
actor Delegate: HIDVirtualDeviceDelegate {
    var current = Contract.neutral
    func setCurrent(_ value: Data) { current = value }
    func hidVirtualDevice(_ device: HIDVirtualDevice, receivedSetReportRequestOfType type: HIDReportType,
                          id: HIDReportID?, data: Data) async throws {
        // This descriptor has no output/feature reports.
        throw ProbeFailure("unsupported set report")
    }
    func hidVirtualDevice(_ device: HIDVirtualDevice, receivedGetReportRequestOfType type: HIDReportType,
                          id: HIDReportID?, maxSize: Int) async throws -> Data {
        guard type == .input, id?.rawValue == 1, maxSize >= current.count else {
            throw ProbeFailure("unsupported get report")
        }
        return current
    }
}

@available(macOS 15, *)
actor Observation {
    var removed: Set<HIDDeviceClient.DeviceReference> = []
    var reports: [Data] = []
    func didRemove(_ reference: HIDDeviceClient.DeviceReference) { removed.insert(reference) }
    func didRead(_ data: Data) { if reports.count < 8 { reports.append(data) } }
    func result(_ reference: HIDDeviceClient.DeviceReference) -> (Bool, Bool) {
        // Deliberately strict. A different callback byte convention fails the
        // candidate and must be understood before changing the expected data.
        (removed.contains(reference), reports == Contract.reports)
    }
}

@MainActor
func requireSonyAbsent() throws {
    guard !NSWorkspace.shared.runningApplications.contains(where: {
        $0.bundleIdentifier == "com.playstation.RemotePlay" ||
        $0.executableURL?.lastPathComponent == "RemotePlay"
    }) else {
        throw ProbeFailure("Sony Remote Play is running; probe refused. It was not stopped.")
    }
}

func requireEntitlement() throws {
    var code: SecCode?
    guard SecCodeCopySelf([], &code) == errSecSuccess, let code else {
        throw ProbeFailure("cannot inspect probe signing identity")
    }
    var staticCode: SecStaticCode?
    guard SecCodeCopyStaticCode(code, [], &staticCode) == errSecSuccess, let staticCode else {
        throw ProbeFailure("cannot inspect static signing identity")
    }
    var info: CFDictionary?
    guard SecCodeCopySigningInformation(staticCode, SecCSFlags(rawValue: kSecCSSigningInformation), &info) == errSecSuccess,
          let values = info as? [String: Any],
          let entitlements = values[kSecCodeInfoEntitlementsDict as String] as? [String: Any],
          entitlements["com.apple.developer.hid.virtual.device"] as? Bool == true else {
        throw ProbeFailure("missing com.apple.developer.hid.virtual.device; no device creation attempted")
    }
    // Presence does not prove provisioning authorization. The system still
    // enforces that separately. This program never signs or requests access.
}

@available(macOS 15, *)
func withVirtualDevice(uniqueID: String, observation: Observation) async throws -> HIDDeviceClient.DeviceReference {
    try await requireSonyAbsent()
    let properties = HIDVirtualDevice.Properties(
        descriptor: Contract.descriptor, vendorID: Contract.vendor,
        productID: Contract.productID, transport: .virtual,
        product: Contract.product, manufacturer: "xiunlee research",
        uniqueID: uniqueID)
    guard let device = HIDVirtualDevice(properties: properties) else {
        throw ProbeFailure("HIDVirtualDevice creation denied or failed")
    }
    let reference = device.deviceReference
    let delegate = Delegate()
    await device.activate(delegate: delegate)
    guard let client = HIDDeviceClient(deviceReference: reference) else {
        throw ProbeFailure("cannot open own device for readback")
    }
    let stream = await client.monitorNotifications(reportIDsToMonitor: [HIDReportID(rawValue: 1)!...HIDReportID(rawValue: 1)!], elementsToMonitor: [])
    let reader = Task {
        do {
            for try await event in stream {
                if case .inputReport(let id, let data, _) = event, id?.rawValue == 1 {
                    await observation.didRead(data)
                }
            }
        } catch { /* A failing reader cannot produce a successful result. */ }
    }
    do {
        try await Task.sleep(for: .milliseconds(150))
        for report in Contract.reports {
            try await requireSonyAbsent()
            await delegate.setCurrent(report)
            try await device.dispatchInputReport(data: report, timestamp: SuspendingClock.now)
            try await Task.sleep(for: .milliseconds(150))
        }
    } catch {
        await delegate.setCurrent(Contract.neutral)
        try? await device.dispatchInputReport(data: Contract.neutral, timestamp: SuspendingClock.now)
        reader.cancel()
        throw error
    }
    reader.cancel()
    // No delegate/task retains the virtual device. Exiting this scope releases
    // our reference; the outer exact-match observer must confirm removal.
    return reference
}

@available(macOS 15, *)
func liveProbe() async throws {
    try requireEntitlement()
    try await requireSonyAbsent()
    // A deadline bounds only this process. A timeout is FAILURE, never proof
    // that kernel-side removal completed. Never kill another process.
    let watchdog = DispatchSource.makeTimerSource(queue: .global())
    watchdog.schedule(deadline: .now() + 8)
    watchdog.setEventHandler {
        let message = "PROBE_TIMEOUT cleanup=unconfirmed\n"
        FileHandle.standardError.write(Data(message.utf8))
        _exit(124)
    }
    watchdog.resume()
    defer { watchdog.cancel() }
    let uniqueID = "pskeyboard-probe-" + UUID().uuidString
    // Persistent fail-closed latch next to this local test binary. A failed
    // process must not silently start a second device on the next invocation.
    guard let executable = Bundle.main.executableURL?.resolvingSymlinksInPath(),
          executable.isFileURL else {
        throw ProbeFailure("cannot resolve the real probe binary path")
    }
    let pending = executable.deletingLastPathComponent().appendingPathComponent("pending-probe.txt")
    let pendingData = Data((uniqueID + "\n").utf8)
    let fd = open(pending.path, O_CREAT | O_EXCL | O_WRONLY | O_NOFOLLOW, S_IRUSR | S_IWUSR)
    guard fd >= 0 else { throw ProbeFailure("pending probe latch exists or cannot be created; resolve previous cleanup first") }
    let handle = FileHandle(fileDescriptor: fd, closeOnDealloc: true)
    try handle.write(contentsOf: pendingData)
    try handle.synchronize()
    try handle.close()
    let manager = HIDDeviceManager()
    let stream = await manager.monitorNotifications(matchingCriteria: [
        .init(vendorID: Contract.vendor, productID: Contract.productID,
              product: Contract.product, uniqueID: uniqueID)
    ])
    let observation = Observation()
    let monitor = Task {
        do {
            for try await event in stream {
                if case .deviceRemoved(let reference) = event {
                    await observation.didRemove(reference)
                }
            }
        } catch { /* Missing removal always fails below. */ }
    }
    defer { monitor.cancel() }
    let reference = try await withVirtualDevice(uniqueID: uniqueID, observation: observation)
    try await Task.sleep(for: .milliseconds(750))
    let (removed, readback) = await observation.result(reference)
    guard removed && readback else {
        throw ProbeFailure("readback=\(readback) own_device_removed=\(removed); do not create another device until resolved")
    }
    guard try Data(contentsOf: pending) == pendingData else {
        throw ProbeFailure("pending probe identity changed; latch retained")
    }
    try FileManager.default.removeItem(at: pending)
    print("GENERIC_HID_PROBE_PASS reports=3 readback=true own_device_removed=true sony_tested=false")
}

@main
struct Main {
    static func main() async {
        do {
            try Contract.selfTest()
            let args = Array(CommandLine.arguments.dropFirst())
            if args.isEmpty || args == ["--dry-run"] || args == ["--self-test"] {
                print("PROBE_DRY_RUN_PASS reports=3 descriptor_bytes=\(Contract.descriptor.count) device_created=false sony_accessed=false")
                return
            }
            guard args == ["--execute-generic-probe"] else {
                throw ProbeFailure("expected --dry-run, --self-test, or --execute-generic-probe")
            }
            if #available(macOS 15, *) { try await liveProbe() }
            else { throw ProbeFailure("CoreHID requires macOS 15+") }
        } catch {
            FileHandle.standardError.write(Data("PROBE_FAILED \(error)\n".utf8))
            exit(1)
        }
    }
}
