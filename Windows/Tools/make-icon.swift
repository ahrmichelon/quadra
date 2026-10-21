import AppKit

// Draws the Quadra icon in every size Windows uses and saves one PNG per size.
// Usage: swift Tools/make-icon.swift <output folder>
// (Tools/make-icon.sh runs this file and then packs the PNGs into src/Quadra/Assets/Quadra.ico)
//
// The drawing is the same as the Mac app's (Branding/quadra-icon.svg): a blue rounded square with a 3 × 3 grid
// and the top-left 2 × 2 block in cyan. Small sizes use hand-picked whole-pixel measurements so they stay sharp
// next to the clock; large sizes follow the SVG's proportions.

let args = CommandLine.arguments
guard args.count == 2 else {
    print("Usage: swift make-icon.swift <output folder>")
    exit(1)
}
let output = URL(fileURLWithPath: args[1])
try? FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)

// Brand colors (the same as in Branding/README.md).
let blueTop = CGColor(srgbRed: 0x3B / 255, green: 0x62 / 255, blue: 0xF4 / 255, alpha: 1)    // #3B62F4
let blueBottom = CGColor(srgbRed: 0x28 / 255, green: 0x45 / 255, blue: 0xC9 / 255, alpha: 1) // #2845C9
let cyan = CGColor(srgbRed: 0x7F / 255, green: 0xE3 / 255, blue: 0xFF / 255, alpha: 1)       // #7FE3FF
let faint = CGColor(srgbRed: 1, green: 1, blue: 1, alpha: 0.28)                              // white 28%

/// Measurements in pixels: empty margin around the tile, grid inset inside the tile, cell size and gap.
struct Metrics {
    var margin: CGFloat, inset: CGFloat, cell: CGFloat, gap: CGFloat, radius: CGFloat, cellRadius: CGFloat
}

func metrics(for size: Int) -> Metrics {
    let s = CGFloat(size)
    switch size {
    case 16: return Metrics(margin: 0, inset: 1, cell: 4, gap: 1, radius: 3, cellRadius: 0.8)
    case 20: return Metrics(margin: 0, inset: 3, cell: 4, gap: 1, radius: 4, cellRadius: 0.8)
    case 24: return Metrics(margin: 0, inset: 4, cell: 4, gap: 2, radius: 5, cellRadius: 1)
    case 32: return Metrics(margin: 0, inset: 5, cell: 6, gap: 2, radius: 7, cellRadius: 1.2)
    default:
        // The SVG: an 824 tile with 185 corners; grid inset 137, cells 156, gaps 41.
        let margin = (s * 0.04).rounded()
        let tile = s - margin * 2
        let k = tile / 824
        return Metrics(margin: margin, inset: 137 * k, cell: 156 * k, gap: 41 * k, radius: 185 * k, cellRadius: 30 * k)
    }
}

func draw(size: Int) -> Data {
    let m = metrics(for: size)
    let s = CGFloat(size)
    let space = CGColorSpace(name: CGColorSpace.sRGB)!
    let ctx = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: 0, space: space,
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    // Work with the origin at the top left, like the SVG.
    ctx.translateBy(x: 0, y: s)
    ctx.scaleBy(x: 1, y: -1)

    // Tile with a vertical gradient.
    let tile = CGRect(x: m.margin, y: m.margin, width: s - m.margin * 2, height: s - m.margin * 2)
    ctx.saveGState()
    ctx.addPath(CGPath(roundedRect: tile, cornerWidth: m.radius, cornerHeight: m.radius, transform: nil))
    ctx.clip()
    let gradient = CGGradient(colorsSpace: space, colors: [blueTop, blueBottom] as CFArray, locations: [0, 1])!
    ctx.drawLinearGradient(gradient, start: CGPoint(x: 0, y: tile.minY), end: CGPoint(x: 0, y: tile.maxY), options: [])
    ctx.restoreGState()

    // 3 × 3 grid with the top-left 2 × 2 block highlighted.
    func cells(col: Int, row: Int, span: Int = 1) -> CGRect {
        let w = m.cell * CGFloat(span) + m.gap * CGFloat(span - 1)
        return CGRect(x: tile.minX + m.inset + CGFloat(col) * (m.cell + m.gap),
                      y: tile.minY + m.inset + CGFloat(row) * (m.cell + m.gap), width: w, height: w)
    }
    func fill(_ rect: CGRect, _ radius: CGFloat, _ color: CGColor) {
        ctx.addPath(CGPath(roundedRect: rect, cornerWidth: radius, cornerHeight: radius, transform: nil))
        ctx.setFillColor(color)
        ctx.fillPath()
    }
    for (col, row) in [(2, 0), (2, 1), (0, 2), (1, 2), (2, 2)] {
        fill(cells(col: col, row: row), m.cellRadius, faint)
    }
    fill(cells(col: 0, row: 0, span: 2), m.cellRadius * 1.2, cyan)

    let image = ctx.makeImage()!
    let rep = NSBitmapImageRep(cgImage: image)
    return rep.representation(using: .png, properties: [:])!
}

for size in [16, 20, 24, 32, 40, 48, 64, 96, 128, 256] {
    try draw(size: size).write(to: output.appendingPathComponent("icon-\(size).png"))
}
print("Wrote icons to \(output.path)")
