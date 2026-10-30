// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// mac-native.1 research frontend. No virtual-HID entitlement or private APIs.
import AppKit
@preconcurrency import ApplicationServices
import Carbon
import Foundation
import Darwin

@MainActor
final class KeyboardPreview: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    let status = NSTextField(wrappingLabelWithString: "尚未連接；不會自動擷取鍵盤。")
    let profilePicker = NSPopUpButton()
    let connect = NSButton(title: "連接測試輸出", target: nil, action: nil)
    let toggle = NSButton(title: "啟用／暫停（F8）", target: nil, action: nil)
    let stop = NSButton(title: "中斷", target: nil, action: nil)
    var core = MacControlCore()
    var tap: CFMachPort?
    var source: CFRunLoopSource?
    var timer: Timer?
    var child: Process?
    var input: Pipe?
    var writer: PreviewPipe?
    var stopping = false
    var selected: OJDFeedProfile = .generic
    var lastWriteTime: UInt64 = 0
    var lastError = ""
    var eventTapFault = false
    var cleanupPending = false
    var quitting = false
    var helper: URL { Bundle.main.bundleURL.deletingLastPathComponent().deletingLastPathComponent()
        .appendingPathComponent("ojd-runtime-0.6.0-alpha.1/ojd") }
    var latch: URL { helper.deletingLastPathComponent().appendingPathComponent("pskeyboard-pending-session.txt") }

    func applicationDidFinishLaunching(_ notification: Notification) {
        // Native hardware key codes; unsupported touch is unassigned for this
        // output adapter. This never reads or overwrites Windows user profiles.
        core = MacControlCore(profile: try! MacBindingProfile.default.withBinding(.touchClick, keyCode: nil))
        let menu = NSMenu(); let item = NSMenuItem(); menu.addItem(item)
        let appMenu = NSMenu(); appMenu.addItem(withTitle: "結束測試程式", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        item.submenu = appMenu; NSApp.mainMenu = menu
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 600, height: 410),
                          styleMask: [.titled, .closable, .miniaturizable], backing: .buffered, defer: false)
        window.title = "上班不要玩 · Mac 輸出研究版"
        window.isReleasedWhenClosed = false
        let title = NSTextField(labelWithString: "上班不要玩 · Mac 輸出研究版")
        title.font = .systemFont(ofSize: 23, weight: .semibold)
        let explanation = NSTextField(wrappingLabelWithString:
            "鍵盤 → 免費 OJD 輸出 → 官方 PS Remote Play\nSony 相容性待驗。此工具不是完成版；觸控板按下暫不支援，PS 鍵效果待驗。")
        profilePicker.addItems(withTitles: ["通用手把", "Xbox · GameController", "Xbox · SDL"])
        profilePicker.target = self; profilePicker.action = #selector(profileChanged)
        connect.target = self; connect.action = #selector(startOutput)
        toggle.target = self; toggle.action = #selector(toggleEnabled)
        stop.target = self; stop.action = #selector(stopOutput)
        let access = NSButton(title: "開啟輔助使用設定", target: self, action: #selector(openPermissions))
        let row = NSStackView(views: [connect, toggle, stop]); row.orientation = .horizontal
        let hints = NSTextField(wrappingLabelWithString:
            "連接後先保持暫停。切回 Remote Play，按 F8（部分鍵盤需 Fn＋F8）啟用。\nWASD：左搖桿　方向鍵：△／□／×／○\nHome／End／Insert／Page Up：十字鍵　E／Q：L1／L2\nDelete／Page Down：R1／R2　Tab／Enter：Create／Options\nF／空白：L3／R3　F1：Guide（PS 待驗）\n切窗會清除按住／連點，切回需重新按鍵。這版先用固定鍵位、連點關閉。")
        hints.font = .systemFont(ofSize: 12)
        status.textColor = .secondaryLabelColor
        let stack = NSStackView(views: [title, explanation, profilePicker, row, access, status, hints])
        stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 14
        stack.translatesAutoresizingMaskIntoConstraints = false
        window.contentView!.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: window.contentView!.leadingAnchor, constant: 24),
            stack.trailingAnchor.constraint(equalTo: window.contentView!.trailingAnchor, constant: -24),
            stack.topAnchor.constraint(equalTo: window.contentView!.topAnchor, constant: 22)
        ])
        toggle.isEnabled = false; stop.isEnabled = false
        window.center(); window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
        NSWorkspace.shared.notificationCenter.addObserver(self, selector: #selector(focusChanged),
            name: NSWorkspace.didActivateApplicationNotification, object: nil)
        for name in [NSWorkspace.sessionDidResignActiveNotification, NSWorkspace.willSleepNotification] {
            NSWorkspace.shared.notificationCenter.addObserver(self, selector: #selector(stopOutput), name: name, object: nil)
        }
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        if child != nil { quitting = true; stopOutput(); return .terminateLater }
        return .terminateNow
    }
    @objc func openPermissions() {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(options)
        NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!)
    }
    @objc func profileChanged() {
        guard child == nil else { return }
        selected = [.generic, .gameControllerCompatible, .sdlCompatible][profilePicker.indexOfSelectedItem]
    }
    func sonyForeground() -> Bool {
        guard let app = NSWorkspace.shared.frontmostApplication,
              app.bundleIdentifier == "com.playstation.RemotePlay" else { return false }
        return app.executableURL?.resolvingSymlinksInPath().path == "/Applications/RemotePlay.app/Contents/MacOS/RemotePlay"
    }
    var now: UInt64 { DispatchTime.now().uptimeNanoseconds / 1_000_000 }
    func updateEnvironment() {
        let flags = CGEventSource.flagsState(.combinedSessionState)
        let forbidden: CGEventFlags = [.maskCommand, .maskControl, .maskAlternate, .maskShift]
        core.updateEnvironment(foreground: sonyForeground(), connected: child?.isRunning == true && !eventTapFault,
                               modifiersDown: !flags.intersection(forbidden).isEmpty)
    }
    @objc func focusChanged() { updateEnvironment() }
    @objc func toggleEnabled() { updateEnvironment(); core.setEnabled(!core.enabled); showState() }
    func showState() {
        guard child != nil, !stopping, lastError.isEmpty else { return }
        status.stringValue = core.active ? "輸出已啟用；僅處理 Remote Play 前景按鍵。" :
            core.enabled ? "已待命；目前不在 Remote Play 前景，輸出中立。" : "已連接測試輸出；鍵盤控制暫停。"
    }
    @objc func startOutput() {
        guard child == nil, !cleanupPending else { return }
        guard FileManager.default.isExecutableFile(atPath: helper.path) else {
            status.stringValue = "未找到已驗證的 OJD CLI；先完成外部輸出元件準備。"; return
        }
        guard !FileManager.default.fileExists(atPath: latch.path) else {
            status.stringValue = "上一輪裝置清理尚待核對；先確認移除，才能重新測試。"; return
        }
        guard AXIsProcessTrusted() else {
            status.stringValue = "需要你在輔助使用設定加入並允許此測試 App，才能攔截遊戲鍵；尚未啟動輸出。"; return
        }
        guard !IsSecureEventInputEnabled() else {
            status.stringValue = "macOS 安全鍵盤輸入正在使用中；離開密碼輸入後再連接。"; return
        }
        eventTapFault = false; stopping = false; lastError = ""
        let mask = (CGEventMask(1) << CGEventType.keyDown.rawValue) |
                   (CGEventMask(1) << CGEventType.keyUp.rawValue) |
                   (CGEventMask(1) << CGEventType.flagsChanged.rawValue)
        guard let newTap = CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap,
                options: .defaultTap, eventsOfInterest: mask, callback: { _, type, event, refcon in
            guard let refcon else { return Unmanaged.passUnretained(event) }
            let consume = MainActor.assumeIsolated {
                let owner = Unmanaged<KeyboardPreview>.fromOpaque(refcon).takeUnretainedValue()
                return owner.handle(type, event) == nil
            }
            return consume ? nil : Unmanaged.passUnretained(event)
        }, userInfo: Unmanaged.passUnretained(self).toOpaque()) else {
            status.stringValue = "macOS 尚未允許事件攔截；請檢查此 App 的輔助使用權限。"; return
        }
        tap = newTap; source = CFMachPortCreateRunLoopSource(nil, newTap, 0)
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: newTap, enable: true)
        let latchFD = open(latch.path, O_CREAT | O_EXCL | O_WRONLY | O_NOFOLLOW, S_IRUSR | S_IWUSR)
        guard latchFD >= 0 else { removeCapture(); status.stringValue = "無法建立本輪清理標記；沒有啟動輸出。"; return }
        let marker = Data((UUID().uuidString + "\n").utf8)
        _ = marker.withUnsafeBytes { write(latchFD, $0.baseAddress, $0.count) }; close(latchFD)
        let pipe = Pipe(); input = pipe
        do { writer = try PreviewPipe(writer: pipe.fileHandleForWriting) }
        catch {
            try? pipe.fileHandleForReading.close(); try? pipe.fileHandleForWriting.close(); input = nil
            removeCapture(); cleanupPending = true; connect.isEnabled = false
            status.stringValue = "無法準備安全輸出管道；未建立手把。"; return
        }
        let process = Process(); process.executableURL = helper
        process.arguments = selected.cliArguments
        process.standardInput = pipe
        // No raw keyboard data, account information or game input is logged.
        process.standardOutput = FileHandle.nullDevice
        process.standardError = FileHandle.nullDevice
        process.terminationHandler = { [weak self] p in
            let code = p.terminationStatus
            DispatchQueue.main.async { MainActor.assumeIsolated { self?.childExited(code) } }
        }
        child = process
        do { try process.run() }
        catch {
            lastError = "輸出 CLI 無法啟動：\(error.localizedDescription)"
            writer?.shutdown(); writer = nil; try? pipe.fileHandleForReading.close(); input = nil
            child = nil; removeCapture(); cleanupPending = true; connect.isEnabled = false
            status.stringValue = lastError; return
        }
        try? pipe.fileHandleForReading.close()
        writer?.pulse(); writer?.startWatchdog()
        profilePicker.isEnabled = false; connect.isEnabled = false; toggle.isEnabled = true; stop.isEnabled = true
        updateEnvironment(); core.setEnabled(false); sendCurrent()
        let outputTimer = Timer(timeInterval: 0.008, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.tick() }
        }
        timer = outputTimer
        RunLoop.main.add(outputTimer, forMode: .common)
        showState()
    }
    func handle(_ type: CGEventType, _ event: CGEvent) -> Unmanaged<CGEvent>? {
        if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
            eventTapFault = true; core.setEnabled(false); stopOutput()
            status.stringValue = "事件攔截已失效；輸出停止，請重新檢查權限。"
            return Unmanaged.passUnretained(event)
        }
        updateEnvironment()
        if type == .flagsChanged { return Unmanaged.passUnretained(event) }
        let key = UInt16(event.getIntegerValueField(.keyboardEventKeycode))
        let consumed = core.process(keyCode: key, isDown: type == .keyDown,
            isRepeat: event.getIntegerValueField(.keyboardEventAutorepeat) != 0,
            injected: event.getIntegerValueField(.eventSourceUnixProcessID) != 0,
            atMilliseconds: now)
        showState()
        return consumed ? nil : Unmanaged.passUnretained(event)
    }
    func tick() {
        guard !IsSecureEventInputEnabled() else {
            lastError = "macOS 啟用安全鍵盤輸入，已停止本次輸出。"
            stopOutput(); return
        }
        updateEnvironment(); sendCurrent(); showState()
    }
    func sendCurrent() {
        guard let process = child, process.isRunning, let writer, !stopping else { return }
        let sample = now
        guard sample >= lastWriteTime && sample - lastWriteTime >= 8 else { return }
        let mask = core.evaluate(atMilliseconds: sample)
        do {
            let frame = try OJDFeedEncoder.line(actions: mask, profile: selected)
            // Every frame fits PIPE_BUF, so a nonblocking write is all-or-none.
            guard frame.count <= 512 else { throw NSError(domain: "PSKeyboard", code: 1) }
            guard writer.write(frame: frame) else { throw NSError(domain: "PSKeyboard", code: 2) }
            lastWriteTime = sample; writer.pulse()
        } catch {
            lastError = "輸出失敗，已停止並關閉自己的 feed。"; stopOutput(); status.stringValue = lastError
        }
    }
    @objc func stopOutput() {
        stopping = true; core.setEnabled(false)
        if let writer, child?.isRunning == true,
           let neutral = try? OJDFeedEncoder.line(actions: 0, profile: selected) { _ = writer.write(frame: neutral) }
        timer?.invalidate(); timer = nil
        writer?.shutdown(); writer = nil; input = nil
        removeCapture()
        toggle.isEnabled = false; stop.isEnabled = false
        guard let process = child else { return }
        status.stringValue = lastError.isEmpty ? "已嘗試送出中立並關閉 feed，等待自己的輸出程序退出。" : lastError
        DispatchQueue.main.asyncAfter(deadline: .now() + 3) { [weak self, weak process] in
            MainActor.assumeIsolated {
                guard let self, let process, self.child === process, process.isRunning else { return }
                self.cleanupPending = true
                self.lastError = "feed 未按時退出；只終止本次子程序。裝置清理待確認，禁止重新連接。"
                process.terminate()
                self.status.stringValue = self.lastError
            }
        }
    }
    func removeCapture() {
        if let tap { CGEvent.tapEnable(tap: tap, enable: false); CFMachPortInvalidate(tap) }
        if let source { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }
        tap = nil; source = nil
    }
    func childExited(_ code: Int32) {
        timer?.invalidate(); timer = nil; removeCapture()
        writer?.shutdown(); writer = nil; input = nil
        core.updateEnvironment(foreground: false, connected: false)
        child = nil
        // CLI exit is not proof of kernel-side device removal. Every session
        // therefore requires independent audit before another connection.
        cleanupPending = true
        connect.isEnabled = false; toggle.isEnabled = false; stop.isEnabled = false
        status.stringValue = "feed 已退出（\(code)）；裝置移除需另行核對。本研究版不自動重新接入。"
        if quitting { NSApp.reply(toApplicationShouldTerminate: true) }
    }
}

@main
struct PreviewMain {
    static func main() {
        let application = NSApplication.shared
        let owner = KeyboardPreview(); application.delegate = owner
        application.setActivationPolicy(.regular); application.run()
        withExtendedLifetime(owner) {}
    }
}
