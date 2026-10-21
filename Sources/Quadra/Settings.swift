import CoreGraphics
import Foundation

/// Shape of the grid in the panel.
enum PanelShape: String, CaseIterable, Identifiable {
    case square, fourThree, threeTwo, sixteenNine, screen

    var id: String { rawValue }

    var title: String {
        switch self {
        case .square: String(localized: "Square")
        case .fourThree: "4:3"
        case .threeTwo: "3:2"
        case .sixteenNine: "16:9"
        case .screen: String(localized: "Like the display")
        }
    }

    /// Height divided by width (nil = the display's own proportion).
    var ratio: CGFloat? {
        switch self {
        case .square: 1
        case .fourThree: 3.0 / 4.0
        case .threeTwo: 2.0 / 3.0
        case .sixteenNine: 9.0 / 16.0
        case .screen: nil
        }
    }
}

/// Preferences kept between launches.
enum Settings {
    private static var defaults: UserDefaults { .standard }

    /// Limits for columns and rows.
    static let sizeRange = 1...20

    static var columns: Int {
        get { clamp(defaults.object(forKey: "columns") as? Int ?? 6) }
        set { defaults.set(clamp(newValue), forKey: "columns") }
    }

    static var rows: Int {
        get { clamp(defaults.object(forKey: "rows") as? Int ?? 6) }
        set { defaults.set(clamp(newValue), forKey: "rows") }
    }

    /// Space in points between windows and around the screen edges.
    static var gap: Int {
        get { defaults.integer(forKey: "gap") }
        set { defaults.set(newValue, forKey: "gap") }
    }

    /// Opacity of the panel background (0.2 to 1). Lower shows more of the apps behind it.
    static let defaultPanelOpacity = 0.6
    static var panelOpacity: Double {
        get { defaults.object(forKey: "panelOpacity") as? Double ?? defaultPanelOpacity }
        set { defaults.set(newValue, forKey: "panelOpacity") }
    }

    /// Width of the grid in the panel, in points.
    static let defaultPanelSize = 340.0
    static let panelSizeRange = 240.0...720.0
    static var panelSize: Double {
        get {
            let v = defaults.object(forKey: "panelSize") as? Double ?? defaultPanelSize
            return min(max(v, panelSizeRange.lowerBound), panelSizeRange.upperBound)
        }
        set { defaults.set(newValue, forKey: "panelSize") }
    }

    static var panelShape: PanelShape {
        get { PanelShape(rawValue: defaults.string(forKey: "panelShape") ?? "") ?? .fourThree }
        set { defaults.set(newValue.rawValue, forKey: "panelShape") }
    }

    /// Size of the grid in the panel, from the chosen width and shape.
    /// `screen` is only used by the "like the display" shape.
    static func panelGridSize(screen: CGSize) -> CGSize {
        let width = CGFloat(panelSize)
        let ratio = panelShape.ratio ?? screen.height / max(screen.width, 1)
        let height = min(max(width * ratio, 120), 600)
        return CGSize(width: width.rounded(), height: height.rounded())
    }

    private static func clamp(_ v: Int) -> Int {
        min(max(v, sizeRange.lowerBound), sizeRange.upperBound)
    }
}
