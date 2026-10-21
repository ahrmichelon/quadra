import AppKit

/// Quadra brand drawings used inside the app.
enum BrandIcon {
    /// Menu bar icon: the same 3 × 3 grid as the app icon, with the 2 × 2 block filled.
    /// It is a template image, so macOS paints it black or white to match the menu bar.
    static func menuBar() -> NSImage {
        let size: CGFloat = 18
        let image = NSImage(size: NSSize(width: size, height: size), flipped: true) { _ in
            let inset: CGFloat = 2, gap: CGFloat = 1.5
            let grid = size - inset * 2
            let cell = (grid - gap * 2) / 3
            func cells(col: Int, row: Int, span: CGFloat = 1) -> CGRect {
                let w = cell * span + gap * (span - 1)
                return CGRect(x: inset + CGFloat(col) * (cell + gap), y: inset + CGFloat(row) * (cell + gap),
                              width: w, height: w)
            }
            NSColor.black.withAlphaComponent(0.4).setFill()
            for (col, row) in [(2, 0), (2, 1), (0, 2), (1, 2), (2, 2)] {
                NSBezierPath(roundedRect: cells(col: col, row: row), xRadius: 1, yRadius: 1).fill()
            }
            NSColor.black.setFill()
            NSBezierPath(roundedRect: cells(col: 0, row: 0, span: 2), xRadius: 1.6, yRadius: 1.6).fill()
            return true
        }
        image.isTemplate = true
        image.accessibilityDescription = "Quadra"
        return image
    }
}
