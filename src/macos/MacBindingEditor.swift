// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import AppKit

/// An isolated draft: opening, capturing and cancelling never write a profile.
/// The caller pauses output before showing this sheet and owns the Save commit.
@MainActor
final class MacBindingEditor: NSObject, NSTableViewDataSource, NSTableViewDelegate,
                              NSWindowDelegate, NSTextFieldDelegate {
    private var draft: MacBindingProfile
    private let onSave: (MacBindingProfile) throws -> Void
    private let onClose: () -> Void
    private let sheet: NSWindow
    private let table = NSTableView()
    private let captureButton = MacPillButton(title: "", target: nil, action: nil, style: .primary)
    private let repeatMode = MacBindingPopup()
    private let interval = MacBindingIntervalField()
    private let repeatLabel = NSTextField(labelWithString: "")
    private let actionTitle = NSTextField(wrappingLabelWithString: "")
    private let currentKey = NSTextField(labelWithString: "")
    private let hint = NSTextField(wrappingLabelWithString: "")
    private var editingAction: MacAction?
    private var updatingControls = false
    private var capturing = false
    private var swallowedKeys = Set<UInt16>()
    private var eventMonitor: Any?
    private var presented = false
    private var closed = false

    init(profile: MacBindingProfile,
         onSave: @escaping (MacBindingProfile) throws -> Void,
         onClose: @escaping () -> Void) {
        draft = profile
        self.onSave = onSave
        self.onClose = onClose
        sheet = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 960, height: 760),
                         styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
        super.init()
        buildInterface()
    }

    func show(relativeTo parent: NSWindow) {
        guard !presented, !closed else { return }
        presented = true
        // This local monitor sees only events delivered to this application.
        // It is never a global keyboard hook and needs no privacy permission.
        eventMonitor = NSEvent.addLocalMonitorForEvents(matching: [.keyDown, .keyUp]) { [weak self] event in
            let consumed = MainActor.assumeIsolated {
                guard let self else { return false }
                return self.handleKey(event) == nil
            }
            return consumed ? nil : event
        }
        parent.beginSheet(sheet) { [self] _ in finishClose() }
        sheet.makeFirstResponder(table)
    }

    private func buildInterface() {
        sheet.title = MacText.t("BindingsTitle")
        sheet.minSize = NSSize(width: 900, height: 720)
        sheet.isReleasedWhenClosed = false
        sheet.delegate = self
        sheet.backgroundColor = MacTheme.surface
        let content = MacSurfaceView(background: .surface)
        sheet.contentView = content

        let root = NSStackView()
        root.orientation = .vertical
        root.alignment = .leading
        root.spacing = 16
        root.translatesAutoresizingMaskIntoConstraints = false
        content.addSubview(root)
        NSLayoutConstraint.activate([
            root.leadingAnchor.constraint(equalTo: content.leadingAnchor, constant: 24),
            root.trailingAnchor.constraint(equalTo: content.trailingAnchor, constant: -24),
            root.topAnchor.constraint(equalTo: content.topAnchor, constant: 20),
            root.bottomAnchor.constraint(equalTo: content.bottomAnchor, constant: -20)
        ])
        let title = NSTextField(labelWithString: MacText.t("BindingsTitle"))
        title.font = MacTheme.font(24, weight: .medium)
        title.textColor = MacTheme.textPrimary
        let heading = NSStackView(views: [title, wrapped("BindingsSubtitle", size: 13)])
        heading.orientation = .vertical
        heading.alignment = .leading
        heading.spacing = 6
        addFullWidth(heading, to: root)
        addFullWidth(MacTheme.hairline(), to: root)

        for (identifier, key, width) in [("action", "ActionColumn", 190.0),
                                         ("key", "KeyColumn", 92.0),
                                         ("repeat", "RepeatColumn", 132.0),
                                         ("interval", "RepeatIntervalLabel", 110.0)] {
            let column = NSTableColumn(identifier: NSUserInterfaceItemIdentifier(identifier))
            column.title = MacText.t(key)
            column.headerCell = MacBindingHeaderCell(textCell: MacText.t(key))
            column.width = width
            column.minWidth = identifier == "action" ? 170 : 80
            table.addTableColumn(column)
        }
        table.dataSource = self
        table.delegate = self
        table.rowHeight = 34
        table.intercellSpacing = NSSize(width: 0, height: 2)
        table.backgroundColor = MacTheme.surface
        table.usesAlternatingRowBackgroundColors = false
        table.allowsMultipleSelection = false
        table.allowsEmptySelection = false
        table.allowsColumnReordering = false
        table.columnAutoresizingStyle = .lastColumnOnlyAutoresizingStyle
        table.target = self
        table.doubleAction = #selector(beginCapture)
        table.setAccessibilityLabel(MacText.t("BindingsTitle"))
        let scroll = NSScrollView()
        scroll.documentView = table
        scroll.hasVerticalScroller = true
        scroll.hasHorizontalScroller = true
        scroll.autohidesScrollers = true
        scroll.drawsBackground = false
        scroll.borderType = .noBorder
        scroll.setContentHuggingPriority(.defaultLow, for: .horizontal)
        scroll.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        scroll.widthAnchor.constraint(greaterThanOrEqualToConstant: 500).isActive = true

        let details = NSStackView()
        details.orientation = .vertical
        details.alignment = .leading
        details.spacing = 12
        actionTitle.font = MacTheme.font(20, weight: .medium)
        actionTitle.textColor = MacTheme.textPrimary
        actionTitle.setContentCompressionResistancePriority(.required, for: .vertical)
        addFullWidth(actionTitle, to: details)
        let keyLabel = NSTextField(labelWithString: MacText.t("KeyColumn"))
        keyLabel.font = MacTheme.font(12)
        keyLabel.textColor = MacTheme.textSecondary
        currentKey.font = MacTheme.font(16, weight: .medium)
        currentKey.textColor = MacTheme.textPrimary
        currentKey.lineBreakMode = .byTruncatingTail
        let keyFrame = MacSurfaceView(background: .card, cornerRadius: 4, bordered: true)
        currentKey.translatesAutoresizingMaskIntoConstraints = false
        keyFrame.addSubview(currentKey)
        NSLayoutConstraint.activate([
            currentKey.leadingAnchor.constraint(equalTo: keyFrame.leadingAnchor, constant: 10),
            currentKey.trailingAnchor.constraint(equalTo: keyFrame.trailingAnchor, constant: -10),
            currentKey.topAnchor.constraint(equalTo: keyFrame.topAnchor, constant: 5),
            currentKey.bottomAnchor.constraint(equalTo: keyFrame.bottomAnchor, constant: -5),
            keyFrame.widthAnchor.constraint(lessThanOrEqualToConstant: 230)
        ])
        details.addArrangedSubview(horizontal([keyLabel, keyFrame]))

        configure(captureButton, key: "CaptureKey", action: #selector(beginCapture))
        captureButton.controlHeight = 36
        addFullWidth(captureButton, to: details)
        addFullWidth(button("ClearBinding", #selector(clearSelected), style: .secondary), to: details)
        addFullWidth(button("Restore selected action", #selector(restoreSelected), style: .quiet), to: details)
        addFullWidth(MacTheme.hairline(), to: details)

        repeatLabel.font = MacTheme.font(13, weight: .medium)
        repeatLabel.textColor = MacTheme.textPrimary
        addFullWidth(repeatLabel, to: details)
        repeatMode.font = MacTheme.font(13)
        repeatMode.addItems(withTitles: [MacRepeatMode.off.displayName,
                                        MacRepeatMode.hold.displayName, MacRepeatMode.toggle.displayName])
        repeatMode.target = self
        repeatMode.action = #selector(repeatChanged)
        repeatMode.setAccessibilityLabel(MacText.t("RepeatModeLabel"))
        repeatMode.heightAnchor.constraint(equalToConstant: 32).isActive = true
        addFullWidth(repeatMode, to: details)
        interval.font = MacTheme.font(13)
        interval.textColor = MacTheme.textPrimary
        interval.alignment = .center
        interval.delegate = self
        interval.target = self
        interval.action = #selector(repeatChanged)
        interval.widthAnchor.constraint(equalToConstant: 88).isActive = true
        interval.heightAnchor.constraint(equalToConstant: 32).isActive = true
        interval.setAccessibilityLabel(MacText.t("RepeatIntervalLabel"))
        interval.setAccessibilityHelp(MacText.t("Enter an interval from 20 to 5000 milliseconds."))
        let intervalLabel = NSTextField(labelWithString: MacText.t("RepeatIntervalLabel"))
        intervalLabel.font = MacTheme.font(13)
        intervalLabel.textColor = MacTheme.textSecondary
        intervalLabel.setContentHuggingPriority(.defaultLow, for: .horizontal)
        addFullWidth(horizontal([intervalLabel, interval]), to: details)
        addFullWidth(wrapped("RepeatHint"), to: details)

        // Keep a native table and native popup/text input so keyboard and
        // Accessibility semantics remain intact. The two regions share Surface.
        let detailDocument = MacBindingDetailDocument()
        details.translatesAutoresizingMaskIntoConstraints = false
        detailDocument.addSubview(details)
        let detailScroll = NSScrollView()
        detailScroll.drawsBackground = false
        detailScroll.borderType = .noBorder
        detailScroll.hasVerticalScroller = true
        detailScroll.autohidesScrollers = true
        detailScroll.scrollerStyle = .overlay
        detailScroll.documentView = detailDocument
        detailDocument.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([
            detailScroll.widthAnchor.constraint(equalToConstant: 300),
            detailDocument.widthAnchor.constraint(equalTo: detailScroll.contentView.widthAnchor),
            detailDocument.topAnchor.constraint(equalTo: detailScroll.contentView.topAnchor),
            detailDocument.leadingAnchor.constraint(equalTo: detailScroll.contentView.leadingAnchor),
            details.leadingAnchor.constraint(equalTo: detailDocument.leadingAnchor),
            details.trailingAnchor.constraint(equalTo: detailDocument.trailingAnchor),
            details.topAnchor.constraint(equalTo: detailDocument.topAnchor),
            details.bottomAnchor.constraint(equalTo: detailDocument.bottomAnchor)
        ])
        let divider = MacTheme.hairline()
        let verticalRule = MacBindingVerticalRule()
        verticalRule.widthAnchor.constraint(equalToConstant: 1).isActive = true
        let body = horizontal([scroll, verticalRule, detailScroll])
        body.alignment = .top
        body.spacing = 20
        addFullWidth(body, to: root)
        NSLayoutConstraint.activate([
            scroll.heightAnchor.constraint(equalTo: body.heightAnchor),
            detailScroll.heightAnchor.constraint(equalTo: body.heightAnchor),
            verticalRule.heightAnchor.constraint(equalTo: body.heightAnchor),
            body.heightAnchor.constraint(greaterThanOrEqualToConstant: 420)
        ])
        body.setContentHuggingPriority(.defaultLow, for: .vertical)
        body.setContentCompressionResistancePriority(.defaultLow, for: .vertical)
        addFullWidth(divider, to: root)

        hint.font = MacTheme.font(12)
        hint.setContentCompressionResistancePriority(.required, for: .vertical)
        hint.heightAnchor.constraint(greaterThanOrEqualToConstant: 32).isActive = true
        addFullWidth(hint, to: root)
        let footer = horizontal([])
        let reset = button("RestoreDefaults", #selector(restoreAll), style: .quiet)
        footer.addArrangedSubview(reset)
        let spacer = NSView()
        spacer.setContentHuggingPriority(.defaultLow, for: .horizontal)
        footer.addArrangedSubview(spacer)
        let cancel = button("Cancel", #selector(cancel), style: .secondary)
        cancel.keyEquivalent = "\u{1b}"
        let save = button("Save", #selector(save), style: .primary)
        save.keyEquivalent = "\r"
        footer.addArrangedSubview(cancel)
        footer.addArrangedSubview(save)
        addFullWidth(footer, to: root)
        addFullWidth(wrapped("Changes are saved only when you choose Save."), to: root)
        table.reloadData()
        table.selectRowIndexes(IndexSet(integer: 0), byExtendingSelection: false)
        loadSelectedControls()
        showHint("BindingsPausedHint")
    }

    private func wrapped(_ key: String, size: CGFloat = 12) -> NSTextField {
        let label = NSTextField(wrappingLabelWithString: MacText.t(key))
        label.font = MacTheme.font(size)
        label.textColor = MacTheme.textSecondary
        label.setContentCompressionResistancePriority(.required, for: .vertical)
        return label
    }

    private func horizontal(_ views: [NSView]) -> NSStackView {
        let stack = NSStackView(views: views)
        stack.orientation = .horizontal
        stack.alignment = .centerY
        stack.spacing = 8
        return stack
    }

    private func addFullWidth(_ view: NSView, to stack: NSStackView) {
        if view is MacPillButton { view.setContentHuggingPriority(.defaultLow, for: .horizontal) }
        stack.addArrangedSubview(view)
        view.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true
    }

    private func configure(_ button: NSButton, key: String, action: Selector) {
        button.title = MacText.t(key)
        button.target = self
        button.action = action
        button.setAccessibilityLabel(button.title)
    }

    private func button(_ key: String, _ action: Selector,
                        style: MacPillButton.Style) -> MacPillButton {
        let result = MacPillButton(title: MacText.t(key), target: self, action: action, style: style)
        result.setAccessibilityLabel(result.title)
        return result
    }

    func numberOfRows(in tableView: NSTableView) -> Int { MacAction.allCases.count }

    func tableView(_ tableView: NSTableView, viewFor tableColumn: NSTableColumn?, row: Int) -> NSView? {
        guard let action = MacAction(rawValue: row), let identifier = tableColumn?.identifier else { return nil }
        let binding = draft[action]
        let value: String
        switch identifier.rawValue {
        case "action": value = action.displayName
        case "key": value = MacKeyNames.display(binding.keyCode)
        case "repeat": value = binding.repeatMode.displayName
        default: value = String(binding.intervalMilliseconds)
        }
        let cell = NSTableCellView()
        let label = NSTextField(labelWithString: value)
        label.font = MacTheme.font(13, weight: identifier.rawValue == "key" ? .medium : .regular)
        label.textColor = MacTheme.textPrimary
        label.lineBreakMode = .byTruncatingTail
        label.toolTip = value
        label.translatesAutoresizingMaskIntoConstraints = false
        cell.addSubview(label)
        cell.textField = label
        NSLayoutConstraint.activate([
            label.leadingAnchor.constraint(equalTo: cell.leadingAnchor, constant: 8),
            label.trailingAnchor.constraint(equalTo: cell.trailingAnchor, constant: -8),
            label.centerYAnchor.constraint(equalTo: cell.centerYAnchor)
        ])
        cell.setAccessibilityLabel(action.displayName + ": " + value)
        return cell
    }

    func tableView(_ tableView: NSTableView, rowViewForRow row: Int) -> NSTableRowView? {
        MacBindingRowView()
    }

    func selectionShouldChange(in tableView: NSTableView) -> Bool { commitRepeat() }

    func tableViewSelectionDidChange(_ notification: Notification) {
        endCapture()
        loadSelectedControls()
    }

    private var selectedAction: MacAction? { MacAction(rawValue: table.selectedRow) }

    private func loadSelectedControls() {
        let wasUpdating = updatingControls
        updatingControls = true
        defer { updatingControls = wasUpdating }
        editingAction = selectedAction
        repeatMode.isEnabled = editingAction != nil
        interval.isEnabled = editingAction != nil
        guard let action = editingAction else { return }
        let binding = draft[action]
        actionTitle.stringValue = action.displayName
        currentKey.stringValue = MacKeyNames.display(binding.keyCode)
        currentKey.toolTip = currentKey.stringValue
        repeatLabel.stringValue = MacText.t("RepeatModeLabel")
        repeatMode.selectItem(at: binding.repeatMode.rawValue)
        interval.stringValue = String(binding.intervalMilliseconds)
    }

    @discardableResult
    private func commitRepeat() -> Bool {
        guard !updatingControls, let action = editingAction else { return true }
        let value = interval.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !value.isEmpty, value.allSatisfy({ $0.isASCII && $0.isNumber }),
              let milliseconds = UInt64(value),
              let mode = MacRepeatMode(rawValue: repeatMode.indexOfSelectedItem) else {
            showHint("Enter an interval from 20 to 5000 milliseconds.", error: true)
            return false
        }
        do {
            draft = try draft.withRepeat(action, mode: mode, intervalMilliseconds: milliseconds)
            table.reloadData(forRowIndexes: IndexSet(integer: action.rawValue),
                             columnIndexes: IndexSet(integersIn: 2...3))
            return true
        } catch {
            showHint("Enter an interval from 20 to 5000 milliseconds.", error: true)
            return false
        }
    }

    @objc private func repeatChanged() {
        endCapture()
        if commitRepeat() { showHint("BindingsPausedHint") }
    }

    func controlTextDidBeginEditing(_ notification: Notification) { endCapture() }
    func controlTextDidEndEditing(_ notification: Notification) { _ = commitRepeat() }

    @objc private func beginCapture() {
        guard selectedAction != nil, commitRepeat() else { return }
        sheet.makeFirstResponder(captureButton)
        capturing = true
        showHint("CapturePrompt")
        hint.textColor = MacTheme.textPrimary
    }

    private func endCapture() {
        guard capturing else { return }
        capturing = false
        showHint("BindingsPausedHint")
    }

    private func handleKey(_ event: NSEvent) -> NSEvent? {
        let code = event.keyCode
        if event.type == .keyUp {
            return swallowedKeys.remove(code) != nil ? nil : event
        }
        if swallowedKeys.contains(code) { return nil }
        guard capturing, event.window === sheet, !event.isARepeat else { return event }
        swallowedKeys.insert(code)
        let forbidden: NSEvent.ModifierFlags = [.command, .control, .option, .shift]
        guard event.modifierFlags.intersection(forbidden).isEmpty,
              MacBindingProfile.isAllowedKey(code) else {
            if code == MacBindingProfile.pauseKeyCode { capturing = false }
            showHint("BindingInvalid", error: true)
            return nil
        }
        if setBinding(code) { endCapture() }
        return nil
    }

    @discardableResult
    private func setBinding(_ key: UInt16?) -> Bool {
        guard let action = selectedAction else { return false }
        do {
            draft = try draft.withBinding(action, keyCode: key)
            refreshDraft()
            return true
        } catch MacProfileError.duplicateKey {
            showHint("BindingConflict", error: true)
        } catch {
            showHint("BindingInvalid", error: true)
        }
        return false
    }

    @objc private func clearSelected() {
        guard commitRepeat() else { return }
        endCapture()
        if setBinding(nil) { showHint("BindingsPausedHint") }
    }

    @objc private func restoreSelected() {
        guard let action = selectedAction else { return }
        endCapture()
        var bindings = draft.bindings
        bindings[action.rawValue] = MacBindingProfile.default[action]
        do {
            draft = try MacBindingProfile(bindings: bindings)
            refreshDraft()
            showHint("BindingsPausedHint")
        } catch MacProfileError.duplicateKey {
            showHint("BindingConflict", error: true)
        } catch {
            showHint("BindingInvalid", error: true)
        }
    }

    @objc private func restoreAll() {
        endCapture()
        draft = .default
        refreshDraft()
        showHint("BindingsPausedHint")
    }

    private func refreshDraft() {
        // reloadData may ask selectionShouldChange. Do not commit the stale
        // popup values over a newly restored/default profile during that reload.
        updatingControls = true
        defer { updatingControls = false }
        table.reloadData()
        loadSelectedControls()
    }

    private func showHint(_ key: String, error: Bool = false) {
        hint.stringValue = MacText.t(key)
        hint.textColor = error ? MacTheme.error : MacTheme.textSecondary
        hint.setAccessibilityLabel(hint.stringValue)
    }

    @objc private func save() {
        guard commitRepeat() else { return }
        endCapture()
        do {
            try onSave(draft)
            close(response: .OK)
        } catch {
            showHint("ProfileSaveFailure", error: true)
        }
    }

    @objc private func cancel() { close(response: .cancel) }

    private func close(response: NSApplication.ModalResponse) {
        endCapture()
        if let parent = sheet.sheetParent { parent.endSheet(sheet, returnCode: response) }
        else { sheet.orderOut(nil); finishClose() }
    }

    private func finishClose() {
        guard !closed else { return }
        closed = true
        if let eventMonitor { NSEvent.removeMonitor(eventMonitor); self.eventMonitor = nil }
        swallowedKeys.removeAll()
        onClose()
    }

    func windowShouldClose(_ sender: NSWindow) -> Bool { cancel(); return false }
    func windowDidResignKey(_ notification: Notification) { endCapture() }
}

// Presentation-only subclasses retain the native table, popup and field
// interaction models while using the same semantic palette in both appearances.
@MainActor
private final class MacBindingDetailDocument: NSView {
    override var isFlipped: Bool { true }
}

@MainActor
private final class MacBindingVerticalRule: NSView {
    override func draw(_ dirtyRect: NSRect) {
        MacTheme.border.setFill()
        bounds.fill()
    }

    override func viewDidChangeEffectiveAppearance() {
        super.viewDidChangeEffectiveAppearance()
        needsDisplay = true
    }
}

@MainActor
private final class MacBindingRowView: NSTableRowView {
    override var interiorBackgroundStyle: NSView.BackgroundStyle { .normal }

    override func drawSelection(in dirtyRect: NSRect) {
        guard isSelected else { return }
        let frame = bounds.insetBy(dx: 2, dy: 1)
        let shape = NSBezierPath(roundedRect: frame, xRadius: 8, yRadius: 8)
        MacTheme.chip.setFill()
        shape.fill()
        MacTheme.border.setStroke()
        shape.lineWidth = 1
        shape.stroke()
    }

    override func viewDidChangeEffectiveAppearance() {
        super.viewDidChangeEffectiveAppearance()
        needsDisplay = true
    }
}

@MainActor
private final class MacBindingHeaderCell: NSTableHeaderCell {
    override func draw(withFrame cellFrame: NSRect, in controlView: NSView) {
        MacTheme.surface.setFill()
        cellFrame.fill()
        let attributes: [NSAttributedString.Key: Any] = [
            .font: MacTheme.font(12, weight: .medium),
            .foregroundColor: MacTheme.textSecondary
        ]
        let text = NSAttributedString(string: stringValue, attributes: attributes)
        let height = text.size().height
        text.draw(in: NSRect(x: cellFrame.minX + 8,
                             y: cellFrame.midY - height / 2,
                             width: max(0, cellFrame.width - 16), height: height))
        MacTheme.border.setFill()
        NSRect(x: cellFrame.minX, y: cellFrame.maxY - 1,
               width: cellFrame.width, height: 1).fill()
    }
}

@MainActor
private final class MacBindingPopup: NSPopUpButton {
    init() {
        super.init(frame: .zero, pullsDown: false)
        isBordered = false
        focusRingType = .exterior
    }

    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    override func draw(_ dirtyRect: NSRect) {
        let shape = NSBezierPath(roundedRect: bounds.insetBy(dx: 0.5, dy: 0.5),
                                 xRadius: 16, yRadius: 16)
        let fill = isHighlighted
            ? MacTheme.mixed(MacTheme.card, toward: MacTheme.textPrimary, fraction: 0.172,
                             appearance: effectiveAppearance)
            : MacTheme.card
        (isEnabled ? fill : fill.withAlphaComponent(0.6)).setFill()
        shape.fill()
        MacTheme.border.setStroke()
        shape.lineWidth = 1
        shape.stroke()
        let color = isEnabled ? MacTheme.textPrimary : MacTheme.textSecondary
        let text = NSAttributedString(string: selectedItem?.title ?? "", attributes: [
            .font: MacTheme.font(13), .foregroundColor: color
        ])
        let height = text.size().height
        text.draw(in: NSRect(x: 14, y: bounds.midY - height / 2,
                             width: max(0, bounds.width - 42), height: height))
        let chevron = NSBezierPath()
        chevron.move(to: NSPoint(x: bounds.maxX - 22, y: bounds.midY - 2))
        chevron.line(to: NSPoint(x: bounds.maxX - 18, y: bounds.midY + 2))
        chevron.line(to: NSPoint(x: bounds.maxX - 14, y: bounds.midY - 2))
        color.setStroke()
        chevron.lineWidth = 1.4
        chevron.stroke()
    }

    override var focusRingMaskBounds: NSRect { bounds }

    override func drawFocusRingMask() {
        NSBezierPath(roundedRect: bounds, xRadius: 16, yRadius: 16).fill()
    }

    override func viewDidChangeEffectiveAppearance() {
        super.viewDidChangeEffectiveAppearance()
        needsDisplay = true
    }
}

@MainActor
private final class MacBindingIntervalCell: NSTextFieldCell {
    override func drawingRect(forBounds rect: NSRect) -> NSRect {
        let height = min(rect.height, ceil((font?.ascender ?? 13) - (font?.descender ?? -3)) + 2)
        return NSRect(x: rect.minX + 12, y: rect.midY - height / 2,
                      width: max(0, rect.width - 24), height: height)
    }
}

@MainActor
private final class MacBindingIntervalField: NSTextField {
    init() {
        super.init(frame: .zero)
        cell = MacBindingIntervalCell(textCell: "")
        isEditable = true
        isSelectable = true
        isBezeled = false
        isBordered = false
        drawsBackground = false
        focusRingType = .exterior
    }

    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    override func draw(_ dirtyRect: NSRect) {
        let shape = NSBezierPath(roundedRect: bounds.insetBy(dx: 0.5, dy: 0.5),
                                 xRadius: 16, yRadius: 16)
        MacTheme.card.setFill()
        shape.fill()
        MacTheme.border.setStroke()
        shape.lineWidth = 1
        shape.stroke()
        super.draw(dirtyRect)
    }

    override var focusRingMaskBounds: NSRect { bounds }

    override func drawFocusRingMask() {
        NSBezierPath(roundedRect: bounds, xRadius: 16, yRadius: 16).fill()
    }

    override func viewDidChangeEffectiveAppearance() {
        super.viewDidChangeEffectiveAppearance()
        needsDisplay = true
    }
}
