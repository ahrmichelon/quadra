import AppKit

/// The grid of cells that stands for the screen. Drag across it to pick the window's area.
final class GridView: NSView {
    var cols = 6 { didSet { needsDisplay = true } }
    var rows = 6 { didSet { needsDisplay = true } }
    /// Where the window is now, as fractions of the screen (0 to 1, origin at the top left).
    var currentWindow: CGRect? { didSet { needsDisplay = true } }
    var selection: GridSelection? { didSet { needsDisplay = true } }

    var onPick: ((GridSelection) -> Void)?
    /// The area changes while the user drags (nil when the drag ends).
    var onSelectionChange: ((GridSelection?) -> Void)?
    /// Keys: returns true when the key was handled.
    var onKey: ((NSEvent) -> Bool)?

    private var dragStart: (col: Int, row: Int)?
    private var hover: (col: Int, row: Int)?
    private let spacing: CGFloat = 3

    override var isFlipped: Bool { true }
    override var acceptsFirstResponder: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    func reset() {
        selection = nil
        dragStart = nil
        hover = nil
    }

    // MARK: Drawing

    private var cellSize: CGSize {
        CGSize(width: (bounds.width - spacing * CGFloat(cols - 1)) / CGFloat(cols),
               height: (bounds.height - spacing * CGFloat(rows - 1)) / CGFloat(rows))
    }

    private func cellRect(col: Int, row: Int) -> CGRect {
        let s = cellSize
        return CGRect(x: CGFloat(col) * (s.width + spacing), y: CGFloat(row) * (s.height + spacing),
                      width: s.width, height: s.height)
    }

    private func cell(at point: CGPoint) -> (col: Int, row: Int) {
        let s = cellSize
        let col = Int(point.x / (s.width + spacing))
        let row = Int(point.y / (s.height + spacing))
        return (min(max(col, 0), cols - 1), min(max(row, 0), rows - 1))
    }

    override func draw(_ dirtyRect: NSRect) {
        // Translucent colors, so the see-through panel still shows the apps behind it.
        let accent = NSColor.controlAccentColor
        let border = NSColor.labelColor.withAlphaComponent(0.22)
        for row in 0..<rows {
            for col in 0..<cols {
                let color: NSColor
                if selection?.contains(col: col, row: row) == true {
                    color = accent.withAlphaComponent(0.7)
                } else if dragStart == nil, let h = hover, h.col == col, h.row == row {
                    color = accent.withAlphaComponent(0.35)
                } else {
                    color = NSColor.labelColor.withAlphaComponent(0.1)
                }
                let path = NSBezierPath(roundedRect: cellRect(col: col, row: row).insetBy(dx: 0.5, dy: 0.5),
                                        xRadius: 4, yRadius: 4)
                color.setFill()
                path.fill()
                border.setStroke()
                path.lineWidth = 1
                path.stroke()
            }
        }

        // Dashed outline showing where the window is now.
        if let w = currentWindow {
            let r = CGRect(x: w.minX * bounds.width, y: w.minY * bounds.height,
                           width: w.width * bounds.width, height: w.height * bounds.height)
                .intersection(bounds.insetBy(dx: 1, dy: 1))
            if !r.isNull && r.width > 4 && r.height > 4 {
                let path = NSBezierPath(roundedRect: r, xRadius: 5, yRadius: 5)
                path.lineWidth = 2
                path.setLineDash([5, 4], count: 2, phase: 0)
                NSColor.labelColor.withAlphaComponent(0.55).setStroke()
                path.stroke()
            }
        }
    }

    // MARK: Mouse

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        trackingAreas.forEach(removeTrackingArea)
        addTrackingArea(NSTrackingArea(rect: .zero,
                                       options: [.mouseMoved, .mouseEnteredAndExited, .activeAlways, .inVisibleRect],
                                       owner: self))
    }

    private func cell(for event: NSEvent) -> (col: Int, row: Int) {
        cell(at: convert(event.locationInWindow, from: nil))
    }

    override func mouseDown(with event: NSEvent) {
        let c = cell(for: event)
        dragStart = c
        selection = GridSelection(from: c, to: c)
        onSelectionChange?(selection)
    }

    override func mouseDragged(with event: NSEvent) {
        guard let start = dragStart else { return }
        let new = GridSelection(from: start, to: cell(for: event))
        if new != selection {
            selection = new
            onSelectionChange?(new)
        }
    }

    override func mouseUp(with event: NSEvent) {
        guard dragStart != nil, let sel = selection else { return }
        dragStart = nil
        onSelectionChange?(nil)
        onPick?(sel)
    }

    override func mouseMoved(with event: NSEvent) {
        hover = cell(for: event)
        needsDisplay = true
    }

    override func mouseExited(with event: NSEvent) {
        hover = nil
        needsDisplay = true
    }

    // MARK: Keyboard

    override func keyDown(with event: NSEvent) {
        if onKey?(event) != true { super.keyDown(with: event) }
    }
}
