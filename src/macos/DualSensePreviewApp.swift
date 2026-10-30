// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import AppKit
@preconcurrency import ApplicationServices
import Carbon
import Foundation
import Darwin

@MainActor
final class DualSensePreview: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    let status = NSTextField(wrappingLabelWithString: "尚未連接；鍵盤控制暫停。")
    let connect = MacPillButton(title: "連接並啟用控制", target: nil, action: nil, style: .cta)
    let toggle = MacPillButton(title: "暫停（Esc）", target: nil, action: nil, style: .primary)
    let stop = MacPillButton(title: "中斷", target: nil, action: nil, style: .quiet)
    let diagnostics = NSTextField(wrappingLabelWithString: "遊戲按鍵：0 次收到／0 次接受　控制狀態送出：0 次")
    let permissionStatus = NSTextField(labelWithString: "")
    let usageDetails = NSStackView()
    let spinner = NSProgressIndicator()
    var appearanceItems: [NSMenuItem] = []
    var permissionTimer: Timer?
    var lastPermissionState: Bool?
    let runtime = MacRuntimeController()
    let installer = MacDependencyInstaller()
    let profileStore = MacProfileStore()
    var editor: MacBindingEditor?
    var guide: MacControlGuideView!
    var configure: NSButton!
    var connectionTask: Task<Void, Never>?
    var stopTask: Task<Void, Never>?
    var preparing = false
    var terminating = false
    var profileNotice = ""
    var core = MacControlCore()
    var tap: CFMachPort?
    var source: CFRunLoopSource?
    var timer: Timer?
    var output: DualSenseRelaySocket?
    var sequence: UInt32 = 0
    var ended = false
    var failure = ""
    var lastWrite: UInt64 = 0
    var observedGamePresses: UInt64 = 0
    var acceptedGamePresses: UInt64 = 0
    var nonneutralFrames: UInt64 = 0
    var hadSonyForeground = false
    var lastDiagnosticWrite: UInt64 = 0
    var version: String { Bundle.main.object(forInfoDictionaryKey: "PSKeyboardDisplayVersion") as? String ?? "開發版" }

    func applicationDidFinishLaunching(_ notification: Notification) {
        MacTheme.registerFonts()
        applyAppearance(UserDefaults.standard.string(forKey: "AppAppearance") ?? "system")
        let loaded = profileStore.load()
        core = MacControlCore(profile: loaded.profile)
        profileNotice = loaded.warning.map { MacText.t($0) } ?? ""
        let brand = MacText.t("AppTitle")
        let menu = NSMenu(); let item = NSMenuItem(); menu.addItem(item)
        let appMenu = NSMenu()
        appMenu.addItem(withTitle: MacText.format("About %@", brand), action: #selector(showAbout), keyEquivalent: "")
        let appearanceItem = NSMenuItem(title: MacText.t("Appearance"), action: nil, keyEquivalent: "")
        let appearanceMenu = NSMenu()
        for (key, value) in [("System appearance", "system"), ("Light", "light"), ("Dark", "dark")] {
            let choice = NSMenuItem(title: MacText.t(key), action: #selector(changeAppearance(_:)), keyEquivalent: "")
            choice.target = self; choice.representedObject = value
            choice.state = (UserDefaults.standard.string(forKey: "AppAppearance") ?? "system") == value ? .on : .off
            appearanceItems.append(choice); appearanceMenu.addItem(choice)
        }
        appearanceItem.submenu = appearanceMenu; appMenu.addItem(appearanceItem)
        appMenu.addItem(NSMenuItem.separator())
        appMenu.addItem(withTitle: MacText.format("Quit %@", brand), action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        item.submenu = appMenu; NSApp.mainMenu = menu
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 880, height: 780),
                          styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.contentMinSize = NSSize(width: 780, height: 600)
        window.title = brand + " · Mac " + version
        window.titlebarAppearsTransparent = true
        window.backgroundColor = MacTheme.surface
        window.isReleasedWhenClosed = false
        window.contentView = MacSurfaceView(background: .surface)
        let title = text(brand, size: 24, weight: .medium)
        let versionLabel = text("Mac " + version, size: 12, secondary: true)
        let headingRow = row([title, spacer(), versionLabel], spacing: 16)
        let explanation = text(MacText.t("AppSubtitle"), size: 13, secondary: true)
        let header = column([headingRow, explanation], spacing: 8)
        connect.title = MacText.t("Connect and enable control"); connect.controlHeight = 36
        toggle.title = MacText.t("Pause (Esc)")
        stop.title = MacText.t("DisconnectButton")
        status.stringValue = MacText.t("Ready to connect.")
        status.font = MacTheme.font(13); status.textColor = MacTheme.textSecondary
        status.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        connect.target = self; connect.action = #selector(startOutput)
        toggle.target = self; toggle.action = #selector(toggleEnabled)
        stop.target = self; stop.action = #selector(stopOutput)
        configure = MacPillButton(title: MacText.t("SettingsButton"), target: self, action: #selector(editBindings), style: .secondary)
        let controls = row([connect, toggle, stop, spacer()], spacing: 8)
        spinner.style = .spinning; spinner.controlSize = .small; spinner.isDisplayedWhenStopped = false
        spinner.widthAnchor.constraint(equalToConstant: 16).isActive = true
        spinner.heightAnchor.constraint(equalToConstant: 16).isActive = true
        let statusRow = row([spinner, status], spacing: 8)
        statusRow.heightAnchor.constraint(greaterThanOrEqualToConstant: 24).isActive = true
        let controlSection = column([controls, statusRow], spacing: 12)
        guide = MacControlGuideView(profile: core.profile)
        let guideHeader = row([text(MacText.t("BindingsHeading"), size: 16, weight: .medium), spacer(), configure], spacing: 16)
        let guideSection = column([guideHeader, guide], spacing: 12)
        let hints = text(MacText.t("ControlsHelp") + "\n" + MacText.t("GameSuitability"), size: 13, secondary: true)
        let notice = text(profileNotice.isEmpty ? MacText.t("ProfilePreserved") : profileNotice, size: 12, secondary: true)
        usageDetails.orientation = .vertical; usageDetails.alignment = .leading; usageDetails.spacing = 8
        usageDetails.addArrangedSubview(hints); usageDetails.addArrangedSubview(notice)
        usageDetails.isHidden = profileNotice.isEmpty
        permissionStatus.font = MacTheme.font(12); permissionStatus.textColor = MacTheme.textSecondary
        let access = MacPillButton(title: MacText.t("Open Accessibility Settings"), target: self, action: #selector(openPermissions), style: .quiet)
        let instructions = MacPillButton(title: MacText.t(usageDetails.isHidden ? "Usage instructions" : "Hide instructions"), target: self,
                                         action: #selector(toggleInstructions(_:)), style: .quiet)
        let details = MacPillButton(title: MacText.t("Show diagnostics"), target: self, action: #selector(toggleDiagnostics(_:)), style: .quiet)
        let permissionRow = row([permissionStatus, access, spacer()], spacing: 8)
        let disclosureRow = row([instructions, details, spacer()], spacing: 8)
        let utilities = column([permissionRow, disclosureRow], spacing: 4)
        diagnostics.isHidden = true
        diagnostics.font = MacTheme.font(12); diagnostics.textColor = MacTheme.textSecondary
        diagnostics.stringValue = MacText.format("Received: %@ · Accepted: %@ · Output: %@", "0", "0", "0")
        let free = text(MacText.t("FreeOpenSource"), size: 12, secondary: true)
        let cats = text(MacText.t("CatsSupport"), size: 12, secondary: true)
        let independent = text(MacText.t("IndependentProject"), size: 12, secondary: true)
        let sponsor = MacPillButton(title: MacText.t("SponsorButton"), target: self, action: #selector(openSponsor), style: .secondary)
        let feedback = MacPillButton(title: MacText.t("FeedbackButton"), target: self, action: #selector(openFeedback), style: .secondary)
        let licenses = MacPillButton(title: MacText.t("Components and licenses"), target: self, action: #selector(showComponents), style: .quiet)
        let supportActions = row([sponsor, feedback, licenses, spacer()], spacing: 8)
        let support = column([free, cats, independent, supportActions], spacing: 8)
        let firstLine = MacTheme.hairline(); let lastLine = MacTheme.hairline()
        let stack = column([header, controlSection, firstLine, guideSection, lastLine, support, utilities, usageDetails, diagnostics], spacing: 16)
        let scroll = NSScrollView()
        scroll.hasVerticalScroller = true; scroll.autohidesScrollers = true; scroll.drawsBackground = false
        scroll.translatesAutoresizingMaskIntoConstraints = false
        let document = MacFlippedDocumentView()
        document.translatesAutoresizingMaskIntoConstraints = false
        scroll.documentView = document
        stack.translatesAutoresizingMaskIntoConstraints = false
        document.addSubview(stack); window.contentView!.addSubview(scroll)
        NSLayoutConstraint.activate([
            scroll.leadingAnchor.constraint(equalTo: window.contentView!.leadingAnchor),
            scroll.trailingAnchor.constraint(equalTo: window.contentView!.trailingAnchor),
            scroll.topAnchor.constraint(equalTo: window.contentView!.topAnchor),
            scroll.bottomAnchor.constraint(equalTo: window.contentView!.bottomAnchor),
            document.widthAnchor.constraint(equalTo: scroll.contentView.widthAnchor),
            stack.leadingAnchor.constraint(equalTo: document.leadingAnchor, constant: 24),
            stack.trailingAnchor.constraint(equalTo: document.trailingAnchor, constant: -24),
            stack.topAnchor.constraint(equalTo: document.topAnchor, constant: 24),
            stack.bottomAnchor.constraint(equalTo: document.bottomAnchor, constant: -24)
        ])
        for view in [header, controlSection, firstLine, guideSection, lastLine, support, utilities, usageDetails, diagnostics] {
            view.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true
        }
        for (container, children) in [(header, [headingRow, explanation] as [NSView]),
                                      (controlSection, [controls, statusRow]), (guideSection, [guideHeader, guide!]),
                                      (support, [free, cats, independent, supportActions]), (utilities, [permissionRow, disclosureRow]), (usageDetails, [hints, notice])] {
            for child in children { child.widthAnchor.constraint(equalTo: container.widthAnchor).isActive = true }
        }
        toggle.isEnabled = false; stop.isEnabled = false
        runtime.onFailure = { [weak self] code in
            guard let self else { return }
            self.failure = MacText.format("The controller connection stopped. Details: %@", code)
            self.stopOutput()
        }
        window.center(); window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
        NSWorkspace.shared.notificationCenter.addObserver(self, selector: #selector(focusChanged),
            name: NSWorkspace.didActivateApplicationNotification, object: nil)
        for name in [NSWorkspace.sessionDidResignActiveNotification, NSWorkspace.willSleepNotification] {
            NSWorkspace.shared.notificationCenter.addObserver(self, selector: #selector(stopOutput), name: name, object: nil)
        }
        saveDiagnostics(force: true)
        refreshPermissionState()
        permissionTimer = Timer.scheduledTimer(timeInterval: 1, target: self,
            selector: #selector(refreshPermissionState), userInfo: nil, repeats: true)
    }
    private func text(_ value: String, size: CGFloat, weight: NSFont.Weight = .regular, secondary: Bool = false) -> NSTextField {
        let label = NSTextField(wrappingLabelWithString: value)
        label.font = MacTheme.font(size, weight: weight)
        label.textColor = secondary ? MacTheme.textSecondary : MacTheme.textPrimary
        return label
    }
    private func row(_ views: [NSView], spacing: CGFloat) -> NSStackView {
        let stack = NSStackView(views: views); stack.orientation = .horizontal
        stack.alignment = .centerY; stack.spacing = spacing; return stack
    }
    private func column(_ views: [NSView], spacing: CGFloat) -> NSStackView {
        let stack = NSStackView(views: views); stack.orientation = .vertical
        stack.alignment = .leading; stack.spacing = spacing; return stack
    }
    private func spacer() -> NSView {
        let view = NSView(); view.setContentHuggingPriority(.defaultLow, for: .horizontal); return view
    }
    private func applyAppearance(_ value: String) {
        NSApp.appearance = value == "light" ? NSAppearance(named: .aqua) : value == "dark" ? NSAppearance(named: .darkAqua) : nil
        window?.backgroundColor = MacTheme.surface
        window?.contentView?.needsDisplay = true
    }
    @objc private func changeAppearance(_ sender: NSMenuItem) {
        guard let value = sender.representedObject as? String, ["system", "light", "dark"].contains(value) else { return }
        UserDefaults.standard.set(value, forKey: "AppAppearance"); applyAppearance(value)
        for item in appearanceItems { item.state = (item.representedObject as? String) == value ? .on : .off }
    }
    @objc private func toggleInstructions(_ sender: NSButton) {
        usageDetails.isHidden.toggle()
        sender.title = MacText.t(usageDetails.isHidden ? "Usage instructions" : "Hide instructions")
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        guard !terminating else { return .terminateLater }
        terminating = true
        permissionTimer?.invalidate()
        stopOutput()
        Task { await stopTask?.value; NSApp.reply(toApplicationShouldTerminate: true) }
        return .terminateLater
    }
    func applicationWillTerminate(_ notification: Notification) { permissionTimer?.invalidate(); closeKeyboardOutput() }
    @objc func refreshPermissionState() {
        let granted = AXIsProcessTrusted()
        permissionStatus.stringValue = granted ? MacText.t("Accessibility: allowed") : MacText.t("Accessibility: permission needed")
        permissionStatus.textColor = MacTheme.textSecondary
        if lastPermissionState != granted {
            lastPermissionState = granted
            saveDiagnostics(force: true)
        }
    }
    @objc func toggleDiagnostics(_ sender: NSButton) {
        diagnostics.isHidden.toggle()
        sender.title = diagnostics.isHidden ? MacText.t("Show diagnostics") : MacText.t("Hide diagnostics")
        if !diagnostics.isHidden {
            diagnostics.stringValue = MacText.format("Received: %@ · Accepted: %@ · Output: %@", String(observedGamePresses), String(acceptedGamePresses), String(nonneutralFrames))
        }
    }
    @objc func openPermissions() {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(options)
        NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!)
    }
    var now: UInt64 { DispatchTime.now().uptimeNanoseconds / 1_000_000 }
    func sonyForeground() -> Bool {
        guard let app = NSWorkspace.shared.frontmostApplication,
              app.bundleIdentifier == "com.playstation.RemotePlay" else { return false }
        return app.executableURL?.resolvingSymlinksInPath().path == "/Applications/RemotePlay.app/Contents/MacOS/RemotePlay"
    }
    func updateEnvironment() {
        let flags = CGEventSource.flagsState(.combinedSessionState)
        let forbidden: CGEventFlags = [.maskCommand, .maskControl, .maskAlternate, .maskShift]
        let foreground = sonyForeground()
        hadSonyForeground = hadSonyForeground || foreground
        core.updateEnvironment(foreground: foreground, connected: output?.isOpen == true,
                               modifiersDown: !flags.intersection(forbidden).isEmpty, editing: editor != nil)
    }
    @objc func focusChanged() { updateEnvironment(); saveDiagnostics(force: true) }
    @objc func toggleEnabled() { updateEnvironment(); core.setEnabled(!core.enabled); showState() }
    func showState() {
        guard output != nil, failure.isEmpty else { return }
        let message = MacText.t(core.active ? "KeyboardActive" : core.enabled ? "KeyboardWaiting" : "KeyboardPaused")
        if status.stringValue != message { status.stringValue = message }
        status.textColor = MacTheme.textPrimary
        let toggleTitle = core.enabled ? MacText.t("Pause (Esc)") : MacText.t("Enable (Esc)")
        if toggle.title != toggleTitle { toggle.title = toggleTitle }
        if !diagnostics.isHidden {
            diagnostics.stringValue = MacText.format("Received: %@ · Accepted: %@ · Output: %@", String(observedGamePresses), String(acceptedGamePresses), String(nonneutralFrames))
        }
        saveDiagnostics()
    }
    @objc func startOutput() {
        guard output == nil, !preparing, stopTask == nil, editor == nil else { return }
        guard AXIsProcessTrusted() else {
            status.stringValue = MacText.t("Allow Accessibility for this app, then connect again."); return
        }
        guard !IsSecureEventInputEnabled() else {
            status.stringValue = MacText.t("Secure keyboard input is active. Leave the password field and try again."); return
        }
        failure = ""; preparing = true; spinner.startAnimation(nil)
        connect.isEnabled = false; stop.isEnabled = true; configure.isEnabled = false
        status.stringValue = MacText.t("Preparing components…")
        connectionTask = Task { [weak self] in
            guard let self else { return }
            defer { self.connectionTask = nil }
            do {
                try await installer.prepare { [weak self] message in self?.status.stringValue = MacText.t(message) }
                try Task.checkCancellation()
                try await runtime.start { [weak self] message in self?.status.stringValue = MacText.t(message) }
                try Task.checkCancellation()
                try beginKeyboardCapture()
                preparing = false; spinner.stopAnimation(nil); configure.isEnabled = true
                showState()
            } catch {
                closeKeyboardOutput()
                await runtime.stop()
                preparing = false; spinner.stopAnimation(nil)
                if !Task.isCancelled {
                    failure = MacText.format("Could not start controller output. Please retry. Details: %@", error.localizedDescription)
                    status.stringValue = failure
                    connect.isEnabled = runtime.cleanupConfirmed; stop.isEnabled = false; configure.isEnabled = true
                    if !runtime.cleanupConfirmed { status.stringValue += "\n" + MacText.t("CleanupIncomplete") }
                }
                saveDiagnostics(force: true)
            }
        }
    }
    private func beginKeyboardCapture() throws {
        guard AXIsProcessTrusted(), !IsSecureEventInputEnabled() else { throw MacDependencyError.failed("keyboard_permission_unavailable") }
        let mask = (CGEventMask(1) << CGEventType.keyDown.rawValue) |
                   (CGEventMask(1) << CGEventType.keyUp.rawValue) |
                   (CGEventMask(1) << CGEventType.flagsChanged.rawValue)
        guard let newTap = CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap,
                options: .defaultTap, eventsOfInterest: mask, callback: { _, type, event, refcon in
            guard let refcon else { return Unmanaged.passUnretained(event) }
            let consume = MainActor.assumeIsolated {
                Unmanaged<DualSensePreview>.fromOpaque(refcon).takeUnretainedValue().handle(type, event)
            }
            return consume ? nil : Unmanaged.passUnretained(event)
        }, userInfo: Unmanaged.passUnretained(self).toOpaque()) else {
            throw MacDependencyError.failed("keyboard_capture_unavailable")
        }
        tap = newTap
        source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, newTap, 0)
        do { output = try DualSenseRelaySocket() }
        catch {
            removeCapture(); throw error
        }
        guard let source else { throw MacDependencyError.failed("keyboard_event_source_unavailable") }
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: newTap, enable: true)
        sequence = 0; lastWrite = 0; failure = ""; ended = false
        // A new tap cannot know which releases occurred while capture was off.
        core = MacControlCore(profile: core.profile)
        observedGamePresses = 0; acceptedGamePresses = 0; nonneutralFrames = 0
        hadSonyForeground = false
        updateEnvironment(); core.setEnabled(true)
        connect.isEnabled = false; toggle.isEnabled = true; stop.isEnabled = true
        output?.startWatchdog()
        tick()
        guard output != nil else { return }
        let outputTimer = Timer(timeInterval: 0.008, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.tick() }
        }
        timer = outputTimer; RunLoop.main.add(outputTimer, forMode: .common)
        showState()
    }
    func handle(_ type: CGEventType, _ event: CGEvent) -> Bool {
        if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
            failure = MacText.t("FailureKeyboardSetup"); stopOutput(); return false
        }
        updateEnvironment()
        if type == .flagsChanged { return false }
        let key = UInt16(event.getIntegerValueField(.keyboardEventKeycode))
        let repeated = event.getIntegerValueField(.keyboardEventAutorepeat) != 0
        let gamePress = type == .keyDown && !repeated && core.foreground && core.profile.action(forKeyCode: key) != nil
        if gamePress { observedGamePresses += 1 }
        // PID metadata is not a reliable physical/synthetic-event flag on macOS.
        // Reject only our own process; never reject another keyboard source
        // solely because it supplies a nonzero PID. No synthetic events are generated.
        let consumed = core.process(keyCode: key, isDown: type == .keyDown, isRepeat: repeated,
            injected: event.getIntegerValueField(.eventSourceUnixProcessID) == Int64(getpid()), atMilliseconds: now)
        if gamePress && consumed && core.active { acceptedGamePresses += 1 }
        showState(); return consumed
    }
    func tick() {
        guard !IsSecureEventInputEnabled() else {
            failure = MacText.t("Secure keyboard input is active. Leave the password field and try again."); stopOutput(); return
        }
        updateEnvironment()
        guard let output else { return }
        let sample = now
        guard sample >= lastWrite, sample - lastWrite >= 8 else { return }
        do {
            let actions = core.evaluate(atMilliseconds: sample)
            let frame = try DualSenseV5Protocol.encode(actionMask: actions, sequence: sequence)
            guard output.write(frame) else { throw POSIXError(.EPIPE) }
            if actions != 0 { nonneutralFrames += 1 }
            sequence &+= 1; lastWrite = sample
        } catch { failure = MacText.t("FailureSend"); stopOutput(); return }
        showState()
    }
    @objc func stopOutput() {
        connectionTask?.cancel()
        closeKeyboardOutput()
        guard stopTask == nil else { return }
        connect.isEnabled = false; toggle.isEnabled = false; stop.isEnabled = false
        configure?.isEnabled = false
        status.stringValue = MacText.t("Stopping…"); spinner.startAnimation(nil)
        let pendingStart = connectionTask
        stopTask = Task { [weak self] in
            guard let self else { return }
            await runtime.stop()
            await pendingStart?.value
            await runtime.stop()
            preparing = false; spinner.stopAnimation(nil)
            connect.isEnabled = runtime.cleanupConfirmed; configure?.isEnabled = true
            status.textColor = MacTheme.textSecondary
            status.stringValue = failure.isEmpty ? MacText.t("Ready to connect.") : failure
            if !runtime.cleanupConfirmed { status.stringValue += "\n" + MacText.t("CleanupIncomplete") }
            saveDiagnostics(force: true)
            stopTask = nil
        }
    }
    private func closeKeyboardOutput() {
        core.setEnabled(false)
        if let output {
            if let neutral = try? DualSenseV5Protocol.encode(actionMask: 0, sequence: sequence) { _ = output.write(neutral) }
            output.close(); ended = true
        }
        output = nil; timer?.invalidate(); timer = nil; removeCapture()
        core.updateEnvironment(foreground: false, connected: false, editing: editor != nil)
        toggle.isEnabled = false
    }
    @objc func editBindings() {
        guard editor == nil, !preparing, stopTask == nil else { return }
        core.setEnabled(false)
        let settings = MacBindingEditor(profile: core.profile, onSave: { [weak self] profile in
            guard let self else { return }
            try self.profileStore.save(profile: profile)
            self.core.applyProfile(profile)
            self.guide.update(profile: profile)
        }, onClose: { [weak self] in
            guard let self else { return }
            self.editor = nil; self.updateEnvironment(); self.showState()
        })
        editor = settings
        updateEnvironment(); showState()
        settings.show(relativeTo: window)
    }
    @objc func openSponsor() {
        core.setEnabled(false); showState()
        if !NSWorkspace.shared.open(MacSupportLinks.sponsorURL) { alert(MacText.t("SponsorFailure")) }
    }
    @objc func openFeedback() {
        core.setEnabled(false); showState()
        guard let url = MacSupportLinks.feedbackURL(version: version) else {
            alert(MacText.t("Feedback link is unavailable.")); return
        }
        if !NSWorkspace.shared.open(url) { alert(MacText.format("FeedbackFailure", MacSupportLinks.feedbackAddress)) }
    }
    @objc func showAbout() {
        core.setEnabled(false); showState()
        alert(MacText.t("AppTitle") + "\n" + MacText.format("Mac %@ · Apple Silicon · macOS 15+", version) +
              "\n" + MacText.t("AppSubtitle") + "\nGPL-3.0-only · xiunlee\n" +
              MacText.t("All features are free. Donations are optional.") + "\n\n" + MacText.t("IndependentProject"))
    }
    @objc func showComponents() {
        core.setEnabled(false); showState()
        let panel = NSAlert()
        panel.messageText = MacText.t("Components and licenses")
        panel.informativeText = MacText.t("The Mac version uses VirtualHere and the built-in macOS virtualization framework. Required vendor components are downloaded from VirtualHere when first connecting. No Docker or Parallels is needed.")
        for key in ["Close", "VirtualHere client license", "VirtualHere server license", "Open source license"] {
            panel.addButton(withTitle: MacText.t(key))
        }
        panel.beginSheetModal(for: window) { response in
            let urls = ["https://www.virtualhere.com/node/316", "https://www.virtualhere.com/embedded_server_license",
                        "https://github.com/xiunlee/ps-remote-play-keyboard/blob/codex/commercial-preview/LICENSE"]
            let index = response.rawValue - NSApplication.ModalResponse.alertSecondButtonReturn.rawValue
            if index == 2, let url = Bundle.main.url(forResource: "THIRD-PARTY-NOTICES", withExtension: "txt") {
                NSWorkspace.shared.open(url)
            } else if urls.indices.contains(index), let url = URL(string: urls[index]) { NSWorkspace.shared.open(url) }
        }
    }
    private func alert(_ message: String) {
        let panel = NSAlert(); panel.messageText = MacText.t("AppTitle"); panel.informativeText = message
        panel.addButton(withTitle: MacText.t("Close")); panel.beginSheetModal(for: window)
    }
    /// Aggregate diagnostics only: no key codes, characters, PIDs or input payloads.
    func saveDiagnostics(force: Bool = false) {
        let sample = now
        guard force || sample - lastDiagnosticWrite >= 1000 else { return }
        lastDiagnosticWrite = sample
        let record: [String: Any] = ["version": version, "accessibilityGranted": AXIsProcessTrusted(),
            "connected": output?.isOpen == true, "armed": core.enabled,
            "hadSonyForeground": hadSonyForeground, "observedGamePresses": observedGamePresses,
            "acceptedGamePresses": acceptedGamePresses, "nonneutralFrames": nonneutralFrames,
            "ended": ended, "failure": failure, "rawKeyLogging": false]
        do {
            let directory = FileManager.default.homeDirectoryForCurrentUser
                .appendingPathComponent("Library/Application Support/PSKeyboard/Diagnostics", isDirectory: true)
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true,
                                                   attributes: [.posixPermissions: 0o700])
            let data = try JSONSerialization.data(withJSONObject: record, options: [.sortedKeys])
            try data.write(to: directory.appendingPathComponent("latest.json"), options: .atomic)
        } catch { /* Diagnostics must not keep a failed output session alive. */ }
    }
    func removeCapture() {
        if let tap { CGEvent.tapEnable(tap: tap, enable: false); CFMachPortInvalidate(tap) }
        if let source { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }
        tap = nil; source = nil
    }
}

@main
struct DualSensePreviewMain {
    static func main() {
        let app = NSApplication.shared
        let delegate = DualSensePreview()
        app.delegate = delegate; app.setActivationPolicy(.regular); app.run()
        withExtendedLifetime(delegate) {}
    }
}

@MainActor
private final class MacFlippedDocumentView: NSView {
    override var isFlipped: Bool { true }
}
