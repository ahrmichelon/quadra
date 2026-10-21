import AppKit

// Draws the Quadra icon in every size macOS uses.
// Usage: swift Tools/make-icon.swift <.iconset folder> <1024 png>
// (Tools/make-icon.sh runs this file and then builds AppIcon.icns)

let args = CommandLine.arguments
guard args.count == 3 else {
    print("Usage: swift make-icon.swift <.iconset folder> <1024 png>")
    exit(1)
}
let iconset = URL(fileURLWithPath: args[1])
let masterPNG = URL(fileURLWithPath: args[2])

// Brand colors (the same as in Branding/README.md).
let blueTop = NSColor(srgbRed: 0x3B / 255, green: 0x62 / 255, blue: 0xF4 / 255, alpha: 1)    // #3B62F4
let blueBottom = NSColor(srgbRed: 0x28 / 255, green: 0x45 / 255, blue: 0xC9 / 255, alpha: 1) // #2845C9
let cyan = NSColor(srgbRed: 0x7F / 255, green: 0xE3 / 255, blue: 0xFF / 255, alpha: 1)       // #7FE3FF

extension CGRect {
    func scaled(_ s: CGFloat) -> CGRect { CGRect(x: minX * s, y: minY * s, width: width * s, height: height * s) }
}

/// Draws the icon in a square of `pixels` × `pixels`.
/// Measurements follow the macOS icon grid: an 824 background inside 1024, with 185 corners.
func drawIcon(pixels: Int) -> NSBitmapImageRep {
    let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: pixels, pixelsHigh: pixels,
                               bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                               colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
    rep.size = NSSize(width: pixels, height: pixels)
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
    let s = CGFloat(pixels) / 1024

    // Rounded background with a soft shadow, like Apple's icons.
    let tile = CGRect(x: 100, y: 100, width: 824, height: 824).scaled(s)
    let tilePath = NSBezierPath(roundedRect: tile, xRadius: 185 * s, yRadius: 185 * s)
    NSGraphicsContext.saveGraphicsState()
    let shadow = NSShadow()
    shadow.shadowColor = NSColor.black.withAlphaComponent(0.28)
    shadow.shadowBlurRadius = 24 * s
    shadow.shadowOffset = NSSize(width: 0, height: -10 * s)
    shadow.set()
    blueBottom.setFill()
    tilePath.fill()
    NSGraphicsContext.restoreGraphicsState()
    NSGradient(starting: blueTop, ending: blueBottom)!.draw(in: tilePath, angle: -90)

    // 3 × 3 grid with the top-left 2 × 2 block highlighted.
    let inset: CGFloat = 137, gap: CGFloat = 41
    let gridSize = 824 - inset * 2
    let cell = (gridSize - gap * 2) / 3
    let gridLeft = 100 + inset, gridTop = 100 + inset + gridSize
    func cells(col: Int, row: Int, span: Int = 1) -> CGRect {
        let w = cell * CGFloat(span) + gap * CGFloat(span - 1)
        let x = gridLeft + CGFloat(col) * (cell + gap)
        let top = gridTop - CGFloat(row) * (cell + gap)
        return CGRect(x: x, y: top - w, width: w, height: w).scaled(s)
    }
    NSColor.white.withAlphaComponent(0.28).setFill()
    for (col, row) in [(2, 0), (2, 1), (0, 2), (1, 2), (2, 2)] {
        NSBezierPath(roundedRect: cells(col: col, row: row), xRadius: 30 * s, yRadius: 30 * s).fill()
    }
    cyan.setFill()
    NSBezierPath(roundedRect: cells(col: 0, row: 0, span: 2), xRadius: 36 * s, yRadius: 36 * s).fill()

    NSGraphicsContext.restoreGraphicsState()
    return rep
}

func save(_ rep: NSBitmapImageRep, to url: URL) {
    guard let data = rep.representation(using: .png, properties: [:]) else { return }
    try? data.write(to: url)
}

try? FileManager.default.createDirectory(at: iconset, withIntermediateDirectories: true)
for base in [16, 32, 128, 256, 512] {
    save(drawIcon(pixels: base), to: iconset.appendingPathComponent("icon_\(base)x\(base).png"))
    save(drawIcon(pixels: base * 2), to: iconset.appendingPathComponent("icon_\(base)x\(base)@2x.png"))
}
save(drawIcon(pixels: 1024), to: masterPNG)
print("Icons drawn in \(iconset.path)")
