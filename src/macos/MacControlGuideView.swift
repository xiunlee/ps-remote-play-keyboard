// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import AppKit

/// A passive, accessible key reference. It never captures keys or sends input.
@MainActor
final class MacControlGuideView: NSView {
    override var intrinsicContentSize: NSSize {
        NSSize(width: NSView.noIntrinsicMetric, height: 318)
    }

    init(profile: MacBindingProfile = .default) {
        super.init(frame: .zero)
        translatesAutoresizingMaskIntoConstraints = false
        setContentHuggingPriority(.defaultLow, for: .horizontal)
        setAccessibilityRole(.group)
        setAccessibilityLabel(MacText.t("BindingsHeading"))
        update(profile: profile)
    }

    func update(profile: MacBindingProfile) {
        subviews.forEach { $0.removeFromSuperview() }
        let movement = Self.section(title: MacText.t("Move and navigate"), content: Self.movementGuide(profile))
        let faces = Self.section(title: MacText.t("Face buttons"), content: Self.faceButtonDiamond(profile))
        let main = Self.stack([movement, faces], orientation: .horizontal, spacing: 32)
        main.distribution = .fillEqually
        main.alignment = .top
        main.heightAnchor.constraint(equalToConstant: 140).isActive = true

        let first = Self.detailRow([
            Self.detail(profile, actions: [.l1, .r1], title: "L1 / R1"),
            Self.detail(profile, actions: [.l2, .r2], title: "L2 / R2"),
            Self.detail(profile, actions: [.create], title: "Create"),
            Self.detail(profile, actions: [.options], title: "Options")
        ])
        let second = Self.detailRow([
            Self.detail(profile, actions: [.l3, .r3], title: "L3 / R3"),
            Self.detail(profile, actions: [.ps], title: "PS"),
            Self.detail(profile, actions: [.touchClick], title: MacText.t("Action21")),
            Self.detail(keys: "Esc", title: MacText.t("KeyboardTitle"),
                        accessibility: MacText.t("Enable (Esc)") + " / " + MacText.t("Pause (Esc)"))
        ])
        let note = Self.label(MacText.format("Circle: %@ · Options: %@ · Pause / enable: Esc",
                                              MacKeyNames.display(profile[.circle].keyCode),
                                              MacKeyNames.display(profile[.options].keyCode)),
                              size: 11, color: MacTheme.textSecondary, wraps: true)
        let divider = MacTheme.hairline()
        let body = Self.stack([main, divider, first, second, note], orientation: .vertical, spacing: 12)
        body.alignment = .leading
        addSubview(body)
        NSLayoutConstraint.activate([
            body.leadingAnchor.constraint(equalTo: leadingAnchor),
            body.trailingAnchor.constraint(equalTo: trailingAnchor),
            body.topAnchor.constraint(equalTo: topAnchor),
            body.bottomAnchor.constraint(lessThanOrEqualTo: bottomAnchor),
            main.widthAnchor.constraint(equalTo: body.widthAnchor),
            divider.widthAnchor.constraint(equalTo: body.widthAnchor),
            first.widthAnchor.constraint(equalTo: body.widthAnchor),
            second.widthAnchor.constraint(equalTo: body.widthAnchor),
            note.widthAnchor.constraint(equalTo: body.widthAnchor)
        ])
    }

    required init?(coder: NSCoder) {
        fatalError("MacControlGuideView is created programmatically")
    }

    private static func movementGuide(_ profile: MacBindingProfile) -> NSView {
        let stick = movementRow(profile, actions: [.leftStickUp, .leftStickDown, .leftStickLeft, .leftStickRight],
                                title: MacText.t("LeftStick"))
        let dpad = movementRow(profile, actions: [.dpadUp, .dpadDown, .dpadLeft, .dpadRight],
                               title: MacText.t("D-pad"))
        let rows = stack([stick, dpad], orientation: .vertical, spacing: 12)
        rows.alignment = .leading
        for row in [stick, dpad] { row.widthAnchor.constraint(equalTo: rows.widthAnchor).isActive = true }
        return rows
    }

    private static func movementRow(_ profile: MacBindingProfile, actions: [MacAction], title: String) -> NSView {
        let pairs = zip(["↑", "↓", "←", "→"], actions).map { direction, action -> NSView in
            let key = MacKeyNames.display(profile[action].keyCode)
            let cap = keycap(key, accessibility: action.displayName + ": " + key)
            let arrow = label(direction, size: 12, color: MacTheme.textSecondary)
            arrow.setAccessibilityElement(false)
            arrow.widthAnchor.constraint(equalToConstant: 12).isActive = true
            let pair = stack([arrow, cap], orientation: .horizontal, spacing: 4)
            pair.alignment = .centerY
            cap.setContentHuggingPriority(.defaultLow, for: .horizontal)
            return pair
        }
        let keys = stack(pairs, orientation: .horizontal, spacing: 10)
        keys.distribution = .fillEqually
        let heading = label(title, size: 11, color: MacTheme.textSecondary)
        let row = stack([heading, keys], orientation: .vertical, spacing: 5)
        row.alignment = .leading
        keys.widthAnchor.constraint(equalTo: row.widthAnchor).isActive = true
        return row
    }

    /// The positions match the controller: triangle above, cross below.
    private static func faceButtonDiamond(_ profile: MacBindingProfile) -> NSView {
        func cap(_ action: MacAction, _ symbol: String) -> NSView {
            let key = MacKeyNames.display(profile[action].keyCode)
            let cap = keycap(symbol + "  " + key, accessibility: action.displayName + ": " + key, size: 14)
            cap.widthAnchor.constraint(equalToConstant: 102).isActive = true
            return cap
        }
        let triangle = cap(.triangle, "△")
        let square = cap(.square, "□")
        let circle = cap(.circle, "○")
        let cross = cap(.cross, "×")
        let middle = stack([square, circle], orientation: .horizontal, spacing: 100)
        let diamond = stack([triangle, middle, cross], orientation: .vertical, spacing: 5)
        diamond.alignment = .centerX
        diamond.setAccessibilityRole(.group)
        diamond.setAccessibilityLabel(MacText.t("Face buttons"))
        return diamond
    }

    private static func detail(_ profile: MacBindingProfile, actions: [MacAction], title: String) -> NSView {
        let keys = actions.map { MacKeyNames.display(profile[$0].keyCode) }.joined(separator: " / ")
        let accessibility = actions.map { $0.displayName + ": " + MacKeyNames.display(profile[$0].keyCode) }.joined(separator: "; ")
        return detail(keys: keys, title: title, accessibility: accessibility)
    }

    private static func detail(keys: String, title: String, accessibility: String) -> NSView {
        let key = keycap(keys, accessibility: accessibility)
        let meaning = label(title, size: 11, color: MacTheme.textSecondary)
        meaning.alignment = .center
        meaning.setAccessibilityElement(false)
        meaning.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        let cell = stack([key, meaning], orientation: .vertical, spacing: 5)
        cell.alignment = .centerX
        NSLayoutConstraint.activate([
            key.widthAnchor.constraint(equalTo: cell.widthAnchor),
            meaning.widthAnchor.constraint(equalTo: cell.widthAnchor)
        ])
        return cell
    }

    private static func detailRow(_ cells: [NSView]) -> NSStackView {
        let row = stack(cells, orientation: .horizontal, spacing: 24)
        row.distribution = .fillEqually
        row.alignment = .top
        return row
    }

    private static func keycap(_ text: String, accessibility: String, size: CGFloat = 12) -> NSView {
        let cap = MacSurfaceView(background: .card, cornerRadius: 4, bordered: true)
        cap.setAccessibilityElement(false)
        cap.toolTip = accessibility
        let value = label(text, size: size, weight: .medium)
        value.alignment = .center
        value.lineBreakMode = .byTruncatingTail
        value.setAccessibilityLabel(accessibility)
        value.toolTip = accessibility
        value.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        cap.addSubview(value)
        NSLayoutConstraint.activate([
            cap.heightAnchor.constraint(equalToConstant: 28),
            cap.widthAnchor.constraint(greaterThanOrEqualToConstant: 36),
            value.leadingAnchor.constraint(equalTo: cap.leadingAnchor, constant: 8),
            value.trailingAnchor.constraint(equalTo: cap.trailingAnchor, constant: -8),
            value.centerYAnchor.constraint(equalTo: cap.centerYAnchor)
        ])
        return cap
    }

    private static func section(title: String, content: NSView) -> NSView {
        let heading = label(title, size: 13, weight: .medium)
        let section = stack([heading, content], orientation: .vertical, spacing: 12)
        section.alignment = .leading
        content.widthAnchor.constraint(equalTo: section.widthAnchor).isActive = true
        section.setAccessibilityRole(.group)
        section.setAccessibilityLabel(title)
        return section
    }

    private static func stack(_ views: [NSView], orientation: NSUserInterfaceLayoutOrientation,
                              spacing: CGFloat) -> NSStackView {
        let stack = NSStackView(views: views)
        stack.translatesAutoresizingMaskIntoConstraints = false
        stack.orientation = orientation
        stack.distribution = .fill
        stack.spacing = spacing
        return stack
    }

    private static func label(_ text: String, size: CGFloat,
                              weight: NSFont.Weight = .regular,
                              color: NSColor = MacTheme.textPrimary, wraps: Bool = false) -> NSTextField {
        let label = wraps ? NSTextField(wrappingLabelWithString: text) : NSTextField(labelWithString: text)
        label.translatesAutoresizingMaskIntoConstraints = false
        label.font = MacTheme.font(size, weight: weight)
        label.textColor = color
        label.isSelectable = false
        return label
    }
}
