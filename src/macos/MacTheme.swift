// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import AppKit
import CoreText

/// Shared native presentation tokens. Appearance is resolved when drawn, so a
/// running window follows the system's light/dark appearance without rebuilding.
@MainActor
enum MacTheme {
    static let surface = color("surface", light: 0xF8F8F6, dark: 0x1F1F1E)
    static let card = color("card", light: 0xFFFFFF, dark: 0x2C2C2A)
    static let border = color("border", light: 0xD7D7D4, dark: 0x3C3C3A)
    static let textPrimary = color("textPrimary", light: 0x262626, dark: 0xD4D4D4)
    static let textSecondary = color("textSecondary", light: 0x737373, dark: 0xA3A3A3)
    static let textTertiary = color("textTertiary", light: 0xA3A3A3, dark: 0x737373)
    static let chip = color("chip", light: 0xE5E5E5, dark: 0x3C3C3A)
    static let hover = color("hover", light: 0xE5E5E5, dark: 0x3C3C3A)
    static let inverseBG = color("inverseBG", light: 0x000000, dark: 0xFFFFFF)
    static let inverseFG = color("inverseFG", light: 0xFFFFFF, dark: 0x000000)
    static let focus = color("focus", light: 0x417CDD, dark: 0x417CDD).withAlphaComponent(0.5)
    static let error = color("error", light: 0xD91F37, dark: 0xD91F37)
    static let warning = color("warning", light: 0xF3A115, dark: 0xF3A115)
    private static var fontsRegistered = false

    /// App-local registration; no font is installed in the user's font folders.
    static func registerFonts() {
        guard !fontsRegistered else { return }
        fontsRegistered = true
        guard let url = Bundle.main.url(forResource: "InterVariable", withExtension: "ttf", subdirectory: "Fonts")
                ?? Bundle.main.url(forResource: "InterVariable", withExtension: "ttf") else { return }
        CTFontManagerRegisterFontsForURL(url as CFURL, .process, nil)
    }

    static func font(_ size: CGFloat, weight: NSFont.Weight = .regular) -> NSFont {
        registerFonts()
        let numericWeight: Double = weight >= .bold ? 700 : weight >= .semibold ? 600 : weight >= .medium ? 500 : 400
        let descriptor = NSFontDescriptor(fontAttributes: [
            .name: "InterVariable", .size: size,
            .variation: [NSNumber(value: 0x77676874): NSNumber(value: numericWeight)]
        ])
        return NSFont(descriptor: descriptor, size: size) ?? .systemFont(ofSize: size, weight: weight)
    }

    static func hairline() -> NSView {
        let line = MacHairlineView()
        line.translatesAutoresizingMaskIntoConstraints = false
        line.heightAnchor.constraint(equalToConstant: 1).isActive = true
        line.setAccessibilityElement(false)
        return line
    }

    static func mixed(_ color: NSColor, toward foreground: NSColor, fraction: CGFloat,
                      appearance: NSAppearance) -> NSColor {
        var background = color
        var text = foreground
        appearance.performAsCurrentDrawingAppearance {
            background = color.usingColorSpace(.sRGB) ?? color
            text = foreground.usingColorSpace(.sRGB) ?? foreground
        }
        return background.blended(withFraction: fraction, of: text) ?? background
    }

    private static func color(_ name: String, light: UInt32, dark: UInt32) -> NSColor {
        NSColor(name: NSColor.Name("PSKeyboard." + name)) { appearance in
            let value = appearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua ? dark : light
            return NSColor(srgbRed: CGFloat((value >> 16) & 255) / 255,
                           green: CGFloat((value >> 8) & 255) / 255,
                           blue: CGFloat(value & 255) / 255, alpha: 1)
        }
    }
}

@MainActor
private final class MacHairlineView: NSView {
    override func draw(_ dirtyRect: NSRect) {
        MacTheme.border.setFill()
        bounds.fill()
    }
    override func viewDidChangeEffectiveAppearance() { super.viewDidChangeEffectiveAppearance(); needsDisplay = true }
}

@MainActor
final class MacSurfaceView: NSView {
    enum Background { case surface, card }
    var background: Background { didSet { needsDisplay = true } }
    var cornerRadius: CGFloat { didSet { needsDisplay = true } }
    var bordered: Bool { didSet { needsDisplay = true } }

    init(background: Background = .surface, cornerRadius: CGFloat = 0, bordered: Bool = false) {
        self.background = background
        self.cornerRadius = cornerRadius
        self.bordered = bordered
        super.init(frame: .zero)
        translatesAutoresizingMaskIntoConstraints = false
    }

    required init?(coder: NSCoder) { fatalError("MacSurfaceView is created programmatically") }

    override func draw(_ dirtyRect: NSRect) {
        let frame = bordered ? bounds.insetBy(dx: 0.5, dy: 0.5) : bounds
        let shape = NSBezierPath(roundedRect: frame, xRadius: cornerRadius, yRadius: cornerRadius)
        (background == .surface ? MacTheme.surface : MacTheme.card).setFill()
        shape.fill()
        if bordered {
            MacTheme.border.setStroke()
            shape.lineWidth = 1
            shape.stroke()
        }
    }

    override func viewDidChangeEffectiveAppearance() { super.viewDidChangeEffectiveAppearance(); needsDisplay = true }
}

/// A native NSButton with a neutral pill presentation. NSButton continues to own
/// target/action, key equivalents, Space activation, accessibility and tracking.
@MainActor
final class MacPillButton: NSButton {
    enum Style { case primary, secondary, cta, quiet }
    var style: Style { didSet { needsDisplay = true } }
    var controlHeight: CGFloat = 32 {
        didSet { invalidateIntrinsicContentSize(); needsDisplay = true }
    }
    private var hovered = false
    private var hoverArea: NSTrackingArea?

    init(title: String, target: AnyObject?, action: Selector?, style: Style = .secondary) {
        self.style = style
        super.init(frame: .zero)
        self.title = title
        self.target = target
        self.action = action
        setButtonType(.momentaryPushIn)
        setAccessibilityRole(.button)
        setAccessibilityElement(true)
        isBordered = false
        focusRingType = .none
        font = MacTheme.font(13, weight: .medium)
        translatesAutoresizingMaskIntoConstraints = false
        setContentHuggingPriority(.required, for: .horizontal)
        setContentCompressionResistancePriority(.required, for: .horizontal)
    }

    required init?(coder: NSCoder) { fatalError("MacPillButton is created programmatically") }
    override var acceptsFirstResponder: Bool { isEnabled }
    override var isEnabled: Bool {
        didSet { if oldValue != isEnabled { needsDisplay = true } }
    }
    override var title: String {
        didSet {
            guard oldValue != title else { return }
            invalidateIntrinsicContentSize()
            needsDisplay = true
        }
    }

    override var intrinsicContentSize: NSSize {
        let textWidth = (title as NSString).size(withAttributes: [.font: font ?? MacTheme.font(13, weight: .medium)]).width
        let iconWidth: CGFloat = image != nil && imagePosition != .noImage ? (imagePosition == .imageOnly ? 16 : 24) : 0
        return NSSize(width: max(56, ceil((imagePosition == .imageOnly ? 0 : textWidth) + iconWidth) + 48),
                      height: controlHeight >= 36 ? 36 : 32)
    }

    override func updateTrackingAreas() {
        if let hoverArea { removeTrackingArea(hoverArea) }
        let area = NSTrackingArea(rect: .zero, options: [.mouseEnteredAndExited, .activeInKeyWindow, .inVisibleRect],
                                  owner: self, userInfo: nil)
        addTrackingArea(area)
        hoverArea = area
        super.updateTrackingAreas()
    }

    override func mouseEntered(with event: NSEvent) { hovered = true; needsDisplay = true }
    override func mouseExited(with event: NSEvent) { hovered = false; needsDisplay = true }
    override func becomeFirstResponder() -> Bool { let result = super.becomeFirstResponder(); needsDisplay = true; return result }
    override func resignFirstResponder() -> Bool { let result = super.resignFirstResponder(); needsDisplay = true; return result }
    override func viewDidChangeEffectiveAppearance() { super.viewDidChangeEffectiveAppearance(); needsDisplay = true }

    override func draw(_ dirtyRect: NSRect) {
        let foreground = style == .cta ? MacTheme.inverseFG : MacTheme.textPrimary
        let base: NSColor
        switch style {
        case .primary: base = MacTheme.chip
        case .secondary: base = MacTheme.card
        case .cta: base = MacTheme.inverseBG
        case .quiet: base = MacTheme.surface
        }
        var fill = base
        if isEnabled && (hovered || isHighlighted) {
            fill = MacTheme.mixed(base, toward: foreground, fraction: 0.08, appearance: effectiveAppearance)
            if isHighlighted { fill = MacTheme.mixed(fill, toward: foreground, fraction: 0.10, appearance: effectiveAppearance) }
        }
        let opacity: CGFloat = isEnabled ? 1 : 0.6
        let frame = bounds.insetBy(dx: 0.5, dy: 0.5)
        let shape = NSBezierPath(roundedRect: frame, xRadius: frame.height / 2, yRadius: frame.height / 2)
        if style != .quiet || (isEnabled && (hovered || isHighlighted)) {
            fill.withAlphaComponent(opacity).setFill()
            shape.fill()
        }
        if style == .secondary {
            MacTheme.border.withAlphaComponent(opacity).setStroke()
            shape.lineWidth = 1
            shape.stroke()
        }
        let paragraph = NSMutableParagraphStyle()
        paragraph.alignment = .center
        paragraph.lineBreakMode = .byTruncatingTail
        let attributes: [NSAttributedString.Key: Any] = [
            .font: font ?? MacTheme.font(13, weight: .medium),
            .foregroundColor: foreground.withAlphaComponent(opacity), .paragraphStyle: paragraph
        ]
        let size = (title as NSString).size(withAttributes: attributes)
        let hasImage = image != nil && imagePosition != .noImage
        let textWidth = imagePosition == .imageOnly ? 0 : size.width
        let groupWidth = textWidth + (hasImage ? (textWidth == 0 ? 16 : 24) : 0)
        let start = (bounds.width - groupWidth) / 2
        if let image, hasImage {
            let icon = image.withSymbolConfiguration(.init(paletteColors: [foreground])) ?? image
            icon.draw(in: NSRect(x: start, y: (bounds.height - 16) / 2, width: 16, height: 16),
                      from: .zero, operation: .sourceOver, fraction: opacity,
                      respectFlipped: true, hints: nil)
        }
        if imagePosition != .imageOnly {
            let x = hasImage ? start + 24 : 12
            (title as NSString).draw(in: NSRect(x: x, y: (bounds.height - size.height) / 2,
                width: hasImage ? textWidth : max(0, bounds.width - 24), height: size.height), withAttributes: attributes)
        }
        if isEnabled, window?.firstResponder === self {
            let focus = NSBezierPath(roundedRect: bounds.insetBy(dx: 2, dy: 2),
                                     xRadius: max(0, bounds.height / 2 - 2), yRadius: max(0, bounds.height / 2 - 2))
            MacTheme.focus.setStroke()
            focus.lineWidth = 2
            focus.stroke()
        }
    }
}
