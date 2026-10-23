import AppKit

// Composes the Mac App Store screenshots (2880 × 1800): a caption on top and a picture of the app.
// Usage: swift Tools/store-screenshots.swift <folder with panel-*, settings-*, workspaces-*, welcome-* PNGs> <output folder>
// (Tools/make-store-screenshots.sh renders those pictures and runs this file.)

let args = CommandLine.arguments
guard args.count == 3 else {
    print("Usage: swift store-screenshots.swift <pictures folder> <output folder>")
    exit(1)
}
let pictures = URL(fileURLWithPath: args[1]), output = URL(fileURLWithPath: args[2])
try? FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)

struct Shot {
    let picture: String
    let title: [String: String]
    let subtitle: [String: String]
}

let shots = [
    Shot(picture: "panel",
         title: ["en": "Drag on a grid. The window goes there.", "pt-BR": "Arraste na grade. A janela vai para lá."],
         subtitle: ["en": "Any area, any grid: 1 to 20 columns and rows.",
                    "pt-BR": "Qualquer área, qualquer grade: de 1 a 20 colunas e linhas."]),
    Shot(picture: "settings",
         title: ["en": "Layouts with their own shortcuts.", "pt-BR": "Layouts com atalhos próprios."],
         subtitle: ["en": "Halves, full screen, center, or your own.",
                    "pt-BR": "Metades, tela cheia, centro, ou os seus."]),
    Shot(picture: "workspaces",
         title: ["en": "Your apps, back in place.", "pt-BR": "Seus apps, de volta no lugar."],
         subtitle: ["en": "Save a workspace and restore it with one shortcut.",
                    "pt-BR": "Salve um espaço de trabalho e restaure com um atalho."]),
    Shot(picture: "welcome",
         title: ["en": "Private by design.", "pt-BR": "Feito para ser privado."],
         subtitle: ["en": "No data collected. No internet access.",
                    "pt-BR": "Nenhum dado coletado. Nenhum acesso à internet."]),
]

func color(_ hex: Int, _ alpha: CGFloat = 1) -> NSColor {
    NSColor(srgbRed: CGFloat((hex >> 16) & 0xFF) / 255, green: CGFloat((hex >> 8) & 0xFF) / 255,
            blue: CGFloat(hex & 0xFF) / 255, alpha: alpha)
}

func centered(_ text: String, size: CGFloat, weight: NSFont.Weight, color: NSColor, top: CGFloat, width: CGFloat) {
    let style = NSMutableParagraphStyle()
    style.alignment = .center
    let attributed = NSAttributedString(string: text, attributes: [
        .font: NSFont.systemFont(ofSize: size, weight: weight), .foregroundColor: color, .paragraphStyle: style,
    ])
    let height = attributed.boundingRect(with: CGSize(width: width, height: 1000), options: .usesLineFragmentOrigin).height
    attributed.draw(in: CGRect(x: 0, y: top - height, width: width, height: height))
}

for (index, shot) in shots.enumerated() {
    for language in ["en", "pt-BR"] {
        let width = 2880, height = 1800
        let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: width, pixelsHigh: height, bitsPerSample: 8,
                                   samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                                   colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
        NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
        let bounds = CGRect(x: 0, y: 0, width: width, height: height)
        NSGradient(starting: color(0x1B2A6B), ending: color(0x0E1533))!.draw(in: bounds, angle: -60)

        centered(shot.title[language]!, size: 118, weight: .bold, color: .white, top: 1680, width: CGFloat(width))
        centered(shot.subtitle[language]!, size: 56, weight: .medium, color: color(0x7FE3FF), top: 1500, width: CGFloat(width))

        let file = pictures.appendingPathComponent("\(shot.picture)-\(language).png")
        guard let picture = NSImage(contentsOf: file),
              let pixels = picture.representations.first else { fatalError("missing \(file.path)") }
        // Fit the picture under the captions, at most at its own pixel size.
        let natural = CGSize(width: pixels.pixelsWide, height: pixels.pixelsHigh)
        let box = CGSize(width: 2500, height: 1240)
        let scale = min(box.width / natural.width, box.height / natural.height, 1)
        let size = CGSize(width: natural.width * scale, height: natural.height * scale)
        let frame = CGRect(x: (CGFloat(width) - size.width) / 2, y: 80 + (box.height - size.height) / 2,
                           width: size.width, height: size.height)
        NSGraphicsContext.saveGraphicsState()
        let shadow = NSShadow()
        shadow.shadowColor = NSColor.black.withAlphaComponent(0.45)
        shadow.shadowBlurRadius = 60
        shadow.shadowOffset = NSSize(width: 0, height: -20)
        shadow.set()
        let clip = NSBezierPath(roundedRect: frame, xRadius: 28, yRadius: 28)
        NSColor.black.setFill()
        clip.fill()
        NSGraphicsContext.restoreGraphicsState()
        NSGraphicsContext.saveGraphicsState()
        clip.addClip()
        picture.draw(in: frame)
        NSGraphicsContext.restoreGraphicsState()

        let name = "\(index + 1)-\(shot.picture)-\(language).png"
        try! rep.representation(using: .png, properties: [:])!.write(to: output.appendingPathComponent(name))
        print("  \(name)")
    }
}
