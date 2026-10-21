import Carbon
import XCTest
@testable import Quadra

final class LayoutTests: XCTestCase {
    let visible = CGRect(x: 0, y: 0, width: 1500, height: 900)

    func testDefaultLayoutsCoverTheExpectedAreas() {
        let frames = SavedLayout.defaults.map { Geometry.frame(for: $0.unitRect, in: visible, gap: 0) }
        XCTAssertEqual(frames, [
            CGRect(x: 0, y: 0, width: 750, height: 900),      // left half
            CGRect(x: 750, y: 0, width: 750, height: 900),    // right half
            CGRect(x: 0, y: 450, width: 1500, height: 450),   // top half
            CGRect(x: 0, y: 0, width: 1500, height: 450),     // bottom half
            CGRect(x: 0, y: 0, width: 1500, height: 900),     // full screen
            CGRect(x: 250, y: 150, width: 1000, height: 600), // center
        ])
    }

    func testDefaultLayoutsHaveDifferentShortcuts() {
        let combos = SavedLayout.defaults.compactMap(\.hotKey)
        XCTAssertEqual(Set(combos).count, combos.count)
    }

    func testResizingKeepsTheArea() {
        var left = SavedLayout.defaults[0]
        left.resize(cols: 4, rows: 2)
        XCTAssertEqual([left.c0, left.c1, left.r0, left.r1], [0, 1, 0, 1])

        var full = SavedLayout.defaults[4]
        full.resize(cols: 20, rows: 20)
        XCTAssertEqual([full.c0, full.c1, full.r0, full.r1], [0, 19, 0, 19])
    }

    func testResizingKeepsACenteredAreaCentered() {
        var center = SavedLayout.defaults[5]
        center.resize(cols: 3, rows: 3)
        XCTAssertEqual([center.c0, center.c1, center.r0, center.r1], [1, 1, 1, 1])
    }

    // MARK: Shortcuts

    func testShortcutText() {
        XCTAssertEqual(HotKeyCombo.showGridDefault.display, "⌃⌥G")
        XCTAssertEqual(HotKeyCombo.nextScreenDefault.display, "⌃⌥⌘→")
    }

    func testShortcutsNeedAModifierOrAnFKey() {
        XCTAssertTrue(HotKeyCombo(keyCode: kVK_F5, modifiers: 0).isUsable)
        XCTAssertFalse(HotKeyCombo(keyCode: kVK_ANSI_A, modifiers: shiftKey).isUsable)
        XCTAssertFalse(HotKeyCombo(keyCode: kVK_ANSI_A, modifiers: 0).isUsable)
    }

    func testDuplicateShortcutsAreFlagged() {
        let store = LayoutStore(persist: false)
        XCTAssertNil(store.warning(for: store.layouts[4].hotKey))
        store.layouts[1].hotKey = store.layouts[0].hotKey
        XCTAssertNotNil(store.warning(for: store.layouts[0].hotKey))
        // The grid shortcut counts too.
        store.layouts[2].hotKey = store.showGridHotKey
        XCTAssertNotNil(store.warning(for: store.showGridHotKey))
    }

    // MARK: Saved preferences

    func testSavedConfigRoundTrip() throws {
        let store = LayoutStore(persist: false)
        store.nextScreenHotKey = nil // the user removed this shortcut
        let data = try JSONEncoder().encode(store.config)
        let decoded = try JSONDecoder().decode(LayoutStore.Config.self, from: data)
        XCTAssertEqual(decoded, store.config)
        XCTAssertNil(decoded.nextScreenHotKey, "a removed shortcut must stay removed")
    }

    func testConfigFromAnOlderVersionGetsTheNewDefaults() throws {
        // Saved before the display shortcuts existed: only layouts and the grid shortcut.
        let old = """
        {"layouts": [], "showGridHotKey": {"keyCode": 5, "modifiers": 6144}}
        """
        let decoded = try JSONDecoder().decode(LayoutStore.Config.self, from: Data(old.utf8))
        XCTAssertEqual(decoded.showGridHotKey, .showGridDefault)
        XCTAssertEqual(decoded.nextScreenHotKey, .nextScreenDefault)
        XCTAssertEqual(decoded.previousScreenHotKey, .previousScreenDefault)
    }
}
