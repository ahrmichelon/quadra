import AppKit

/// Panel background: the window color at the opacity chosen in Settings, with no blur,
/// so the apps behind it stay visible.
final class PanelBackground: NSView {
    var opacity: CGFloat = CGFloat(Settings.defaultPanelOpacity) { didSet { needsDisplay = true } }

    override func draw(_ dirtyRect: NSRect) {
        let path = NSBezierPath(roundedRect: bounds.insetBy(dx: 0.5, dy: 0.5), xRadius: 14, yRadius: 14)
        NSColor.windowBackgroundColor.withAlphaComponent(opacity).setFill()
        path.fill()
        NSColor.separatorColor.setStroke()
        path.lineWidth = 1
        path.stroke()
    }
}

/// Title, buttons, display picker, grid and a one-line footer, laid out by hand.
final class GridContentView: NSView {
    let titleLabel = NSTextField(labelWithString: "")
    let gearButton = GridContentView.iconButton("gearshape", help: String(localized: "Settings"))
    let closeButton = GridContentView.iconButton("xmark.circle.fill", help: String(localized: "Close (esc)"))
    let screenPicker = NSSegmentedControl()
    let grid = GridView()
    let hintLabel = NSTextField(labelWithString: String(localized: "↩ full screen · esc closes"))

    private let columnsLabel = NSTextField(labelWithString: "")
    private let rowsLabel = NSTextField(labelWithString: "")
    private let columnsStepper = NSStepper()
    private let rowsStepper = NSStepper()
    private lazy var controls = NSStackView(views: [columnsLabel, columnsStepper, rowsLabel, rowsStepper])

    /// Called when the user changes columns or rows with the steppers.
    var onSizeChange: ((_ cols: Int, _ rows: Int) -> Void)?
    /// Called with the index of the display picked in the display picker.
    var onScreenChange: ((Int) -> Void)?
    var onGear: ((NSButton) -> Void)?
    var onClose: (() -> Void)?

    override var isFlipped: Bool { true }

    private static func iconButton(_ symbol: String, help: String) -> NSButton {
        let b = NSButton()
        b.image = NSImage(systemSymbolName: symbol, accessibilityDescription: help)
        b.isBordered = false
        b.imagePosition = .imageOnly
        b.contentTintColor = .secondaryLabelColor
        b.refusesFirstResponder = true
        b.toolTip = help
        return b
    }

    init() {
        super.init(frame: .zero)
        titleLabel.font = .systemFont(ofSize: 13, weight: .semibold)
        titleLabel.lineBreakMode = .byTruncatingTail

        gearButton.target = self
        gearButton.action = #selector(gearClicked)
        closeButton.target = self
        closeButton.action = #selector(closeClicked)

        screenPicker.controlSize = .small
        screenPicker.font = .systemFont(ofSize: 11)
        screenPicker.segmentDistribution = .fillEqually
        screenPicker.trackingMode = .selectOne
        screenPicker.refusesFirstResponder = true
        screenPicker.target = self
        screenPicker.action = #selector(screenPicked)

        grid.toolTip = String(localized:
            "Drag to choose the area · click another window to switch · ←→↑↓ halves · ↩ full screen · esc closes")

        for (label, stepper) in [(columnsLabel, columnsStepper), (rowsLabel, rowsStepper)] {
            label.font = .monospacedDigitSystemFont(ofSize: 11, weight: .regular)
            label.alignment = .right
            label.widthAnchor.constraint(equalToConstant: 64).isActive = true
            stepper.minValue = Double(Settings.sizeRange.lowerBound)
            stepper.maxValue = Double(Settings.sizeRange.upperBound)
            stepper.increment = 1
            stepper.valueWraps = false
            stepper.controlSize = .small
            stepper.refusesFirstResponder = true
            stepper.target = self
            stepper.action = #selector(stepperChanged)
        }
        controls.orientation = .horizontal
        controls.alignment = .centerY
        controls.spacing = 2
        controls.setCustomSpacing(10, after: columnsStepper)

        hintLabel.font = .systemFont(ofSize: 11)
        hintLabel.textColor = .secondaryLabelColor
        hintLabel.alignment = .right
        hintLabel.lineBreakMode = .byTruncatingHead

        // A soft glow in the background color keeps the text readable over the apps behind.
        for label in [titleLabel, hintLabel, columnsLabel, rowsLabel] {
            let glow = NSShadow()
            glow.shadowColor = NSColor.windowBackgroundColor
            glow.shadowBlurRadius = 4
            glow.shadowOffset = .zero
            label.shadow = glow
        }

        [titleLabel, gearButton, closeButton, screenPicker, grid, controls, hintLabel].forEach(addSubview)
    }

    required init?(coder: NSCoder) { fatalError("not used") }

    /// Fills the content and returns the size the panel should have.
    /// Compact layout: title and buttons on top, the display picker (only with more than one
    /// display), the grid, and one footer line with columns/rows on the left and a hint on the right.
    func configure(title: String, cols: Int, rows: Int, gridSize: CGSize, current: CGRect?,
                   screenNames: [String], selectedScreen: Int) -> CGSize {
        let pad: CGFloat = 14
        let gridW = gridSize.width, gridH = gridSize.height

        titleLabel.stringValue = title
        grid.currentWindow = current
        grid.reset()
        setGridSize(cols: cols, rows: rows)

        titleLabel.frame = CGRect(x: pad, y: pad - 2, width: gridW - 50, height: 18)
        gearButton.frame = CGRect(x: pad + gridW - 42, y: pad - 3, width: 20, height: 20)
        closeButton.frame = CGRect(x: pad + gridW - 20, y: pad - 3, width: 20, height: 20)
        var y = titleLabel.frame.maxY + 6

        screenPicker.isHidden = screenNames.count < 2
        if !screenPicker.isHidden {
            screenPicker.segmentCount = screenNames.count
            for (i, name) in screenNames.enumerated() {
                screenPicker.setLabel(name, forSegment: i)
                screenPicker.setToolTip(name, forSegment: i)
            }
            screenPicker.selectedSegment = selectedScreen
            screenPicker.frame = CGRect(x: pad, y: y, width: gridW, height: 22)
            y = screenPicker.frame.maxY + 8
        }

        grid.frame = CGRect(x: pad, y: y, width: gridW, height: gridH)

        let footerY = grid.frame.maxY + 8
        let controlsW = controls.fittingSize.width
        controls.frame = CGRect(x: pad - 4, y: footerY, width: controlsW, height: 22)
        let hintX = controls.frame.maxX + 8
        hintLabel.frame = CGRect(x: hintX, y: footerY + 4, width: max(pad + gridW - hintX, 0), height: 15)

        let size = CGSize(width: gridW + pad * 2, height: footerY + 22 + pad - 4)
        frame = CGRect(origin: .zero, size: size)
        return size
    }

    /// Updates the grid and the steppers (without calling `onSizeChange`).
    func setGridSize(cols: Int, rows: Int) {
        grid.cols = cols
        grid.rows = rows
        grid.selection = nil
        columnsStepper.integerValue = cols
        rowsStepper.integerValue = rows
        columnsLabel.stringValue = String(localized: "Columns: \(cols)")
        rowsLabel.stringValue = String(localized: "Rows: \(rows)")
    }

    @objc private func stepperChanged() {
        let cols = columnsStepper.integerValue, rows = rowsStepper.integerValue
        setGridSize(cols: cols, rows: rows)
        onSizeChange?(cols, rows)
    }

    @objc private func screenPicked() { onScreenChange?(screenPicker.selectedSegment) }
    @objc private func gearClicked() { onGear?(gearButton) }
    @objc private func closeClicked() { onClose?() }
}

/// Floating, see-through panel that shows the grid without taking focus away from the
/// window's app.
final class GridPanel: NSPanel, NSWindowDelegate {
    let content = GridContentView()
    private let background = PanelBackground()

    /// Called with the chosen area, as fractions of the screen (0 to 1, origin at the top left).
    var onPick: ((CGRect) -> Void)?
    /// Called when the user closes the panel with esc or the close button.
    var onCancel: (() -> Void)?
    /// The panel lost focus: the user clicked another window or somewhere else.
    var onResignKey: (() -> Void)?
    /// The dragged area changed (nil when the drag ends), to show the preview on screen.
    var onPreview: ((CGRect?) -> Void)?
    /// The panel closed.
    var onHide: (() -> Void)?
    /// When the panel last closed (so a click on the menu bar icon doesn't reopen it at once).
    private(set) var lastHidden = Date.distantPast

    init() {
        super.init(contentRect: .zero, styleMask: [.borderless, .nonactivatingPanel],
                   backing: .buffered, defer: true)
        isFloatingPanel = true
        // One level above the outline and the preview, which sit under the panel.
        level = NSWindow.Level(rawValue: NSWindow.Level.floating.rawValue + 1)
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .transient]
        backgroundColor = .clear
        isOpaque = false
        hasShadow = true
        hidesOnDeactivate = false
        isReleasedWhenClosed = false
        delegate = self

        background.wantsLayer = true
        background.addSubview(content)
        contentView = background

        content.grid.onPick = { [weak self] sel in
            guard let self else { return }
            self.onPick?(sel.unitRect(cols: self.content.grid.cols, rows: self.content.grid.rows))
        }
        content.grid.onSelectionChange = { [weak self] sel in
            guard let self else { return }
            self.onPreview?(sel?.unitRect(cols: self.content.grid.cols, rows: self.content.grid.rows))
        }
        content.grid.onKey = { [weak self] event in self?.handleKey(event) ?? false }
        content.onSizeChange = { [weak self] cols, rows in
            Settings.columns = cols
            Settings.rows = rows
            self?.makeFirstResponder(self?.content.grid)
        }
        content.onClose = { [weak self] in self?.cancel() }
    }

    override var canBecomeKey: Bool { true }

    /// Shows (or updates) the panel centered on `center` (AppKit coordinates).
    func show(title: String, current: CGRect?, gridSize: CGSize, screenNames: [String], selectedScreen: Int,
              center: CGPoint) {
        background.opacity = CGFloat(Settings.panelOpacity)
        let size = content.configure(title: title, cols: Settings.columns, rows: Settings.rows,
                                     gridSize: gridSize, current: current,
                                     screenNames: screenNames, selectedScreen: selectedScreen)
        setFrame(CGRect(x: (center.x - size.width / 2).rounded(), y: (center.y - size.height / 2).rounded(),
                        width: size.width, height: size.height), display: true)
        makeKeyAndOrderFront(nil)
        makeFirstResponder(content.grid)
        invalidateShadow()
    }

    func cancel() {
        orderOut(nil)
        onCancel?()
    }

    override func orderOut(_ sender: Any?) {
        let wasVisible = isVisible
        super.orderOut(sender)
        if wasVisible {
            lastHidden = Date()
            onHide?()
        }
    }

    func windowDidResignKey(_ notification: Notification) {
        if isVisible { onResignKey?() }
    }

    private func handleKey(_ event: NSEvent) -> Bool {
        let unit: CGRect
        switch event.keyCode {
        case 53: // esc
            cancel()
            return true
        case 123: unit = CGRect(x: 0, y: 0, width: 0.5, height: 1)    // ← left half
        case 124: unit = CGRect(x: 0.5, y: 0, width: 0.5, height: 1)  // → right half
        case 126: unit = CGRect(x: 0, y: 0, width: 1, height: 0.5)    // ↑ top half
        case 125: unit = CGRect(x: 0, y: 0.5, width: 1, height: 0.5)  // ↓ bottom half
        case 36, 76: unit = CGRect(x: 0, y: 0, width: 1, height: 1)   // ↩ full screen
        default: return false
        }
        onPick?(unit)
        return true
    }
}

/// See-through window that ignores clicks: draws the outline around the chosen window
/// or the preview of the destination area.
final class OverlayWindow: NSWindow {
    enum Style { case outline, fill }

    private final class Box: NSView {
        var style: Style = .outline

        override func draw(_ dirtyRect: NSRect) {
            let accent = NSColor.controlAccentColor
            switch style {
            case .outline:
                let path = NSBezierPath(roundedRect: bounds.insetBy(dx: 2, dy: 2), xRadius: 12, yRadius: 12)
                path.lineWidth = 4
                accent.setStroke()
                path.stroke()
            case .fill:
                let path = NSBezierPath(roundedRect: bounds.insetBy(dx: 1.5, dy: 1.5), xRadius: 10, yRadius: 10)
                accent.withAlphaComponent(0.22).setFill()
                path.fill()
                path.lineWidth = 3
                accent.withAlphaComponent(0.9).setStroke()
                path.stroke()
            }
        }
    }

    init(style: Style) {
        super.init(contentRect: .zero, styleMask: [.borderless], backing: .buffered, defer: true)
        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        ignoresMouseEvents = true
        level = .floating
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .transient, .ignoresCycle]
        isReleasedWhenClosed = false
        let box = Box()
        box.style = style
        contentView = box
    }

    /// Shows over `frame` (AppKit coordinates).
    func show(frame: CGRect) {
        setFrame(frame, display: true)
        orderFrontRegardless()
    }
}
