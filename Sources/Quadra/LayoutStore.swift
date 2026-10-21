import AppKit
import Carbon

/// A saved screen area with its own shortcut (like Divvy's "shortcuts").
struct SavedLayout: Codable, Identifiable, Equatable {
    var id = UUID()
    var name: String
    /// The grid used to pick the area.
    var cols: Int
    var rows: Int
    /// The chosen cells (row 0 at the top).
    var c0: Int, r0: Int, c1: Int, r1: Int
    var hotKey: HotKeyCombo?

    var selection: GridSelection {
        get { GridSelection(from: (c0, r0), to: (c1, r1)) }
        set { c0 = newValue.c0; r0 = newValue.r0; c1 = newValue.c1; r1 = newValue.r1 }
    }

    /// The area as fractions of the screen (0 to 1, origin at the top left).
    var unitRect: CGRect { selection.unitRect(cols: cols, rows: rows) }

    /// Changes the grid size while keeping the area as close as possible.
    mutating func resize(cols newCols: Int, rows newRows: Int) {
        let u = unitRect
        // Round to the nearest cell; on a tie, pull the edges inwards so a centered area stays centered.
        func cells(_ start: CGFloat, _ end: CGFloat, _ count: Int) -> (Int, Int) {
            let a = min(max(Int((start * CGFloat(count) + 0.5).rounded(.down)), 0), count - 1)
            let b = min(max(Int((end * CGFloat(count) - 0.5).rounded(.up)) - 1, a), count - 1)
            return (a, b)
        }
        (c0, c1) = cells(u.minX, u.maxX, newCols)
        (r0, r1) = cells(u.minY, u.maxY, newRows)
        cols = newCols
        rows = newRows
    }

    private static func preset(_ name: String, grid: (Int, Int), from a: (Int, Int), to b: (Int, Int),
                               key: Int) -> SavedLayout {
        SavedLayout(name: name, cols: grid.0, rows: grid.1, c0: a.0, r0: a.1, c1: b.0, r1: b.1,
                    hotKey: HotKeyCombo(keyCode: key, modifiers: controlKey | optionKey))
    }

    /// The layouts the app starts with.
    static var defaults: [SavedLayout] {
        [
            preset(String(localized: "Left half"), grid: (2, 1), from: (0, 0), to: (0, 0), key: kVK_LeftArrow),
            preset(String(localized: "Right half"), grid: (2, 1), from: (1, 0), to: (1, 0), key: kVK_RightArrow),
            preset(String(localized: "Top half"), grid: (1, 2), from: (0, 0), to: (0, 0), key: kVK_UpArrow),
            preset(String(localized: "Bottom half"), grid: (1, 2), from: (0, 1), to: (0, 1), key: kVK_DownArrow),
            preset(String(localized: "Full screen"), grid: (1, 1), from: (0, 0), to: (0, 0), key: kVK_Return),
            preset(String(localized: "Center"), grid: (6, 6), from: (1, 1), to: (4, 4), key: kVK_ANSI_C),
        ]
    }
}

/// The layouts and the app's own shortcuts. Saves itself after every change.
final class LayoutStore: ObservableObject {
    @Published var layouts: [SavedLayout] = [] { didSet { changed() } }
    @Published var showGridHotKey: HotKeyCombo? = .showGridDefault { didSet { changed() } }
    @Published var nextScreenHotKey: HotKeyCombo? = .nextScreenDefault { didSet { changed() } }
    @Published var previousScreenHotKey: HotKeyCombo? = .previousScreenDefault { didSet { changed() } }
    /// Shortcuts macOS refused to register (usually because another app already uses them).
    @Published var unavailable: Set<HotKeyCombo> = []

    /// Called after any change to the layouts or shortcuts.
    var onChange: (() -> Void)?

    private let persist: Bool
    private static let key = "config"

    /// What gets saved in the preferences.
    struct Config: Codable, Equatable {
        var layouts: [SavedLayout]
        var showGridHotKey: HotKeyCombo?
        var nextScreenHotKey: HotKeyCombo?
        var previousScreenHotKey: HotKeyCombo?

        init(layouts: [SavedLayout], showGridHotKey: HotKeyCombo?, nextScreenHotKey: HotKeyCombo?,
             previousScreenHotKey: HotKeyCombo?) {
            self.layouts = layouts
            self.showGridHotKey = showGridHotKey
            self.nextScreenHotKey = nextScreenHotKey
            self.previousScreenHotKey = previousScreenHotKey
        }

        private enum CodingKeys: String, CodingKey {
            case layouts, showGridHotKey, nextScreenHotKey, previousScreenHotKey
        }

        // A shortcut missing from the file (saved by an older version) gets its default;
        // a shortcut saved as null was removed by the user and stays off.
        init(from decoder: Decoder) throws {
            let c = try decoder.container(keyedBy: CodingKeys.self)
            func hotKey(_ key: CodingKeys, _ fallback: HotKeyCombo) throws -> HotKeyCombo? {
                c.contains(key) ? try c.decodeIfPresent(HotKeyCombo.self, forKey: key) : fallback
            }
            layouts = try c.decode([SavedLayout].self, forKey: .layouts)
            showGridHotKey = try hotKey(.showGridHotKey, .showGridDefault)
            nextScreenHotKey = try hotKey(.nextScreenHotKey, .nextScreenDefault)
            previousScreenHotKey = try hotKey(.previousScreenHotKey, .previousScreenDefault)
        }

        func encode(to encoder: Encoder) throws {
            var c = encoder.container(keyedBy: CodingKeys.self)
            try c.encode(layouts, forKey: .layouts)
            try c.encode(showGridHotKey, forKey: .showGridHotKey)
            try c.encode(nextScreenHotKey, forKey: .nextScreenHotKey)
            try c.encode(previousScreenHotKey, forKey: .previousScreenHotKey)
        }
    }

    /// With `persist: false` it only uses the defaults and saves nothing (for tests and snapshots).
    init(persist: Bool = true) {
        self.persist = persist
        if persist, let data = UserDefaults.standard.data(forKey: Self.key),
           let config = try? JSONDecoder().decode(Config.self, from: data) {
            layouts = config.layouts
            showGridHotKey = config.showGridHotKey
            nextScreenHotKey = config.nextScreenHotKey
            previousScreenHotKey = config.previousScreenHotKey
        } else {
            layouts = SavedLayout.defaults
        }
    }

    var config: Config {
        Config(layouts: layouts, showGridHotKey: showGridHotKey, nextScreenHotKey: nextScreenHotKey,
               previousScreenHotKey: previousScreenHotKey)
    }

    func layout(id: UUID) -> SavedLayout? {
        layouts.first { $0.id == id }
    }

    /// Warning to show next to a shortcut, or nil when all is well.
    func warning(for combo: HotKeyCombo?) -> String? {
        guard let combo else { return nil }
        let all = [showGridHotKey, nextScreenHotKey, previousScreenHotKey] + layouts.map(\.hotKey)
        if all.filter({ $0 == combo }).count > 1 { return String(localized: "Duplicate shortcut") }
        if unavailable.contains(combo) { return String(localized: "Used by another app") }
        return nil
    }

    private func changed() {
        if persist, let data = try? JSONEncoder().encode(config) {
            UserDefaults.standard.set(data, forKey: Self.key)
        }
        onChange?()
    }
}
