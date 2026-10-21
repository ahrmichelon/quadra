import AppKit
import SwiftUI

/// Draws parts of the app to PNG images, to check the look without opening windows on screen.
/// Used to produce the screenshots in the README.
enum Snapshot {
    /// A make-believe desktop with windows, to show the panel's transparency.
    private final class FakeDesktop: NSView {
        override func draw(_ dirtyRect: NSRect) {
            NSGradient(colors: [.systemIndigo, .systemTeal])?.draw(in: bounds, angle: 30)
            let windows: [(CGRect, NSColor)] = [
                (CGRect(x: 30, y: 40, width: 380, height: 240), .white),
                (CGRect(x: 330, y: 120, width: 420, height: 260), .systemOrange),
                (CGRect(x: 600, y: 20, width: 300, height: 200), .black),
            ]
            for (rect, color) in windows {
                color.setFill()
                NSBezierPath(roundedRect: rect, xRadius: 10, yRadius: 10).fill()
                NSColor.gray.setFill()
                NSBezierPath(rect: CGRect(x: rect.minX + 20, y: rect.midY, width: rect.width - 40, height: 8)).fill()
            }
        }
    }

    /// The grid panel over a make-believe desktop, with two displays in the picker.
    static func render(to path: String, dark: Bool) {
        let appearance = NSAppearance(named: dark ? .darkAqua : .aqua)!
        let content = GridContentView()
        let size = content.configure(title: "Safari — Start Page", cols: 6, rows: 6,
                                     gridSize: CGSize(width: Settings.defaultPanelSize,
                                                      height: Settings.defaultPanelSize * 3 / 4),
                                     current: CGRect(x: 0.12, y: 0.15, width: 0.55, height: 0.62),
                                     screenNames: ["1 · Studio Display", "2 · Built-in Display"], selectedScreen: 0)
        content.grid.selection = GridSelection(from: (0, 0), to: (2, 5))

        let margin: CGFloat = 30
        let desktop = FakeDesktop(frame: CGRect(x: 0, y: 0, width: size.width + margin * 2,
                                                height: size.height + margin * 2))
        desktop.appearance = appearance
        let background = PanelBackground(frame: CGRect(x: margin, y: margin, width: size.width, height: size.height))
        background.opacity = CGFloat(Settings.defaultPanelOpacity)
        background.addSubview(content)
        desktop.addSubview(background)
        save(desktop, appearance: appearance, to: path)
    }

    /// The Settings window with the default layouts (without reading or writing preferences).
    static func renderSettings(to path: String, dark: Bool) {
        let appearance = NSAppearance(named: dark ? .darkAqua : .aqua)!
        let host = NSHostingView(rootView: SettingsView(store: LayoutStore(persist: false))
            .background(Color(nsColor: .windowBackgroundColor)))
        let size = host.fittingSize
        let window = NSWindow(contentRect: CGRect(origin: .zero, size: size), styleMask: [.borderless],
                              backing: .buffered, defer: false)
        window.appearance = appearance
        window.contentView = host
        host.layoutSubtreeIfNeeded()
        RunLoop.main.run(until: Date().addingTimeInterval(0.5)) // let SwiftUI lay out the view
        save(host, appearance: appearance, to: path)
    }

    private static func save(_ view: NSView, appearance: NSAppearance, to path: String) {
        guard let rep = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { return }
        appearance.performAsCurrentDrawingAppearance {
            view.cacheDisplay(in: view.bounds, to: rep)
        }
        try? rep.representation(using: .png, properties: [:])?.write(to: URL(fileURLWithPath: path))
    }
}
