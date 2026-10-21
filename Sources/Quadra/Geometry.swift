import AppKit

/// A rectangle of grid cells. Row 0 is at the top.
struct GridSelection: Equatable {
    var c0: Int, r0: Int, c1: Int, r1: Int

    init(from a: (col: Int, row: Int), to b: (col: Int, row: Int)) {
        c0 = min(a.col, b.col); c1 = max(a.col, b.col)
        r0 = min(a.row, b.row); r1 = max(a.row, b.row)
    }

    func contains(col: Int, row: Int) -> Bool {
        col >= c0 && col <= c1 && row >= r0 && row <= r1
    }

    /// The selection as fractions of the screen (0 to 1, origin at the top left).
    func unitRect(cols: Int, rows: Int) -> CGRect {
        CGRect(x: CGFloat(c0) / CGFloat(cols), y: CGFloat(r0) / CGFloat(rows),
               width: CGFloat(c1 - c0 + 1) / CGFloat(cols), height: CGFloat(r1 - r0 + 1) / CGFloat(rows))
    }
}

enum Geometry {
    /// AppKit measures from the bottom-left corner of the primary display; the Accessibility API
    /// measures from its top-left corner. Both use the primary display (the one with the menu bar).
    static var primaryHeight: CGFloat { NSScreen.screens.first?.frame.height ?? 0 }

    /// Converts a rectangle between the two coordinate systems (the formula is the same both ways).
    static func flip(_ r: CGRect, primaryHeight: CGFloat = Geometry.primaryHeight) -> CGRect {
        CGRect(x: r.minX, y: primaryHeight - r.maxY, width: r.width, height: r.height)
    }

    // MARK: Displays

    /// Displays from left to right (top to bottom when stacked), the order shown in the panel.
    static var orderedScreens: [NSScreen] {
        NSScreen.screens.sorted { isOrderedBefore($0.frame, $1.frame) }
    }

    static func isOrderedBefore(_ a: CGRect, _ b: CGRect) -> Bool {
        a.minX != b.minX ? a.minX < b.minX : a.maxY > b.maxY
    }

    /// Index of the frame that holds most of `rect`, or nil when it touches none.
    static func indexOfLargestOverlap(_ rect: CGRect, in frames: [CGRect]) -> Int? {
        var best: Int?
        var bestArea: CGFloat = 0
        for (i, frame) in frames.enumerated() {
            let overlap = frame.intersection(rect)
            let area = overlap.isNull ? 0 : overlap.width * overlap.height
            if area > bestArea { best = i; bestArea = area }
        }
        return best
    }

    /// The display that holds most of the rectangle (AppKit coordinates).
    static func screen(containing rect: CGRect) -> NSScreen? {
        let screens = NSScreen.screens
        if let i = indexOfLargestOverlap(rect, in: screens.map(\.frame)) { return screens[i] }
        return NSScreen.main ?? screens.first
    }

    // MARK: Frames

    /// The screen area (AppKit coordinates) for an area given as fractions of the screen
    /// (0 to 1, origin at the top left). `visible` is the usable area, without menu bar and Dock.
    static func frame(for unit: CGRect, in visible: CGRect, gap: CGFloat) -> CGRect {
        var left = visible.minX + visible.width * unit.minX
        var right = visible.minX + visible.width * unit.maxX
        var top = visible.maxY - visible.height * unit.minY
        var bottom = visible.maxY - visible.height * unit.maxY
        if gap > 0 {
            // Full gap at the screen edges, half a gap between neighbouring windows.
            let atEdge = { (v: CGFloat) in abs(v) < 0.001 }
            left += atEdge(unit.minX) ? gap : gap / 2
            right -= atEdge(unit.maxX - 1) ? gap : gap / 2
            top -= atEdge(unit.minY) ? gap : gap / 2
            bottom += atEdge(unit.maxY - 1) ? gap : gap / 2
        }
        left = left.rounded(); right = right.rounded()
        top = top.rounded(); bottom = bottom.rounded()
        return CGRect(x: left, y: bottom, width: right - left, height: top - bottom)
    }

    /// Moves a window frame from one display to another, keeping its relative position and size
    /// (a window on the left half stays on the left half).
    static func map(_ frame: CGRect, from source: CGRect, to dest: CGRect) -> CGRect {
        let sx = dest.width / max(source.width, 1)
        let sy = dest.height / max(source.height, 1)
        let width = min(frame.width * sx, dest.width)
        let height = min(frame.height * sy, dest.height)
        var x = dest.minX + (frame.minX - source.minX) * sx
        var y = dest.minY + (frame.minY - source.minY) * sy
        x = min(max(x, dest.minX), dest.maxX - width)
        y = min(max(y, dest.minY), dest.maxY - height)
        return CGRect(x: x.rounded(), y: y.rounded(), width: width.rounded(), height: height.rounded())
    }

    /// Centers a window of `size` on `target`, keeping it inside `area` when it fits.
    /// Used when an app refuses the requested size (minimum or fixed size).
    static func centered(_ size: CGSize, on target: CGRect, within area: CGRect) -> CGRect {
        var x = target.midX - size.width / 2
        var y = target.midY - size.height / 2
        x = max(min(x, area.maxX - size.width), area.minX)
        // Too tall for the screen: keep the title bar visible at the top.
        y = size.height > area.height ? area.maxY - size.height : max(min(y, area.maxY - size.height), area.minY)
        return CGRect(x: x.rounded(), y: y.rounded(), width: size.width, height: size.height)
    }
}
