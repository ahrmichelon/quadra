import Carbon
import XCTest
@testable import Quadra

final class WorkspaceTests: XCTestCase {
    private func placement(_ bundleID: String, _ title: String, display: String = "Studio Display",
                           index: Int = 0) -> WindowPlacement {
        WindowPlacement(bundleID: bundleID, appName: bundleID, title: title, display: display,
                        displayIndex: index, unitRect: CGRect(x: 0, y: 0, width: 0.5, height: 1))
    }

    // MARK: Matching saved windows to open windows

    func testWindowsWithTheSameTitleArePairedFirst() {
        let saved = [placement("mail", "Inbox"), placement("mail", "Drafts")]
        let open = [OpenWindow(bundleID: "mail", title: "Drafts"), OpenWindow(bundleID: "mail", title: "Inbox")]
        let pairs = WorkspaceMatcher.match(saved, to: open)
        XCTAssertEqual(pairs.map(\.placement), [0, 1])
        XCTAssertEqual(pairs.map(\.window), [1, 0])
    }

    func testTitlesThatChangedFallBackToTheAppsWindowsInOrder() {
        // A browser's title follows the page, so it rarely matches: use its windows front to back.
        let saved = [placement("safari", "News"), placement("safari", "Mail")]
        let open = [OpenWindow(bundleID: "safari", title: "Weather"), OpenWindow(bundleID: "safari", title: "Maps")]
        let pairs = WorkspaceMatcher.match(saved, to: open)
        XCTAssertEqual(pairs.map(\.placement), [0, 1])
        XCTAssertEqual(pairs.map(\.window), [0, 1])
    }

    func testEachWindowIsUsedOnce() {
        let saved = [placement("notes", ""), placement("notes", ""), placement("notes", "")]
        let open = [OpenWindow(bundleID: "notes", title: "A"), OpenWindow(bundleID: "notes", title: "B")]
        let pairs = WorkspaceMatcher.match(saved, to: open)
        XCTAssertEqual(pairs.count, 2, "only two windows are open")
        XCTAssertEqual(Set(pairs.map(\.window)), [0, 1])
    }

    func testOtherAppsWindowsAreLeftAlone() {
        let saved = [placement("pages", "Report")]
        let open = [OpenWindow(bundleID: "safari", title: "Report"), OpenWindow(bundleID: "pages", title: "Letter")]
        let pairs = WorkspaceMatcher.match(saved, to: open)
        XCTAssertEqual(pairs.map(\.window), [1])
    }

    func testAnExactTitleWinsOverAnEarlierWindow() {
        // The second saved window has its exact match; the first takes what is left.
        let saved = [placement("mail", "Old"), placement("mail", "Inbox")]
        let open = [OpenWindow(bundleID: "mail", title: "Inbox"), OpenWindow(bundleID: "mail", title: "Other")]
        let pairs = WorkspaceMatcher.match(saved, to: open)
        XCTAssertEqual(pairs.map(\.window), [1, 0])
    }

    func testTheSameDocumentWinsOverTheTitle() {
        var report = placement("pages", "Report"); report.document = "file:///Users/me/Report.pages"
        let open = [OpenWindow(bundleID: "pages", title: "Report", document: "file:///Users/me/Old/Report.pages"),
                    OpenWindow(bundleID: "pages", title: "Report — Edited", document: "file:///Users/me/Report.pages")]
        XCTAssertEqual(WorkspaceMatcher.match([report], to: open).map(\.window), [1])
    }

    func testADocumentPlacementDoesntTakeAnotherWindow() {
        // The app opened without the file and shows an Open panel: leave it alone.
        var letter = placement("textedit", "Letter.txt"); letter.document = "file:///Users/me/Letter.txt"
        let open = [OpenWindow(bundleID: "textedit", title: "Open")]
        XCTAssertTrue(WorkspaceMatcher.match([letter], to: open).isEmpty)
    }

    func testOldWorkspacesWithoutDocumentsStillLoad() throws {
        let old = """
        {"bundleID": "a", "appName": "A", "title": "T", "display": "D", "displayIndex": 0,
         "unitRect": [[0, 0], [0.5, 1]]}
        """
        let decoded = try JSONDecoder().decode(WindowPlacement.self, from: Data(old.utf8))
        XCTAssertNil(decoded.document)
        XCTAssertEqual(decoded.title, "T")
    }

    // MARK: Displays

    func testTheDisplayIsFoundByNameThenByPosition() {
        let names = ["Built-in Display", "Studio Display"]
        XCTAssertEqual(WorkspaceMatcher.displayIndex(for: placement("a", "", display: "Studio Display", index: 0),
                                                     among: names), 1, "same name, even if it moved")
        XCTAssertEqual(WorkspaceMatcher.displayIndex(for: placement("a", "", display: "Projector", index: 1),
                                                     among: names), 1, "unknown name: same position")
        XCTAssertEqual(WorkspaceMatcher.displayIndex(for: placement("a", "", display: "Projector", index: 3),
                                                     among: names), 0, "display gone: the first one")
    }

    // MARK: Positions

    func testAWindowsAreaRoundTrips() {
        let visible = CGRect(x: -1920, y: 25, width: 1920, height: 1055)
        let frame = CGRect(x: -1440, y: 300, width: 960, height: 600)
        let unit = Geometry.unit(of: frame, in: visible)
        XCTAssertEqual(Geometry.frame(for: unit, in: visible, gap: 0), frame)
    }

    func testAnAreaOutsideTheDisplayIsKeptInside() {
        let visible = CGRect(x: 0, y: 0, width: 1000, height: 800)
        let unit = Geometry.unit(of: CGRect(x: 900, y: -100, width: 400, height: 1000), in: visible)
        XCTAssertGreaterThanOrEqual(unit.minX, 0)
        XCTAssertLessThanOrEqual(unit.maxX, 1)
        XCTAssertLessThanOrEqual(unit.maxY, 1)
        XCTAssertGreaterThanOrEqual(unit.minY, 0)
    }

    // MARK: Saving

    func testAppNamesAreListedOnce() {
        var a = placement("mail", "Inbox"); a.appName = "Mail"
        var b = placement("mail", "Drafts"); b.appName = "Mail"
        var c = placement("safari", ""); c.appName = "Safari"
        XCTAssertEqual(Workspace(name: "W", windows: [a, b, c]).appNames, ["Mail", "Safari"])
    }

    func testWorkspacesAreSaved() throws {
        let store = LayoutStore(persist: false)
        store.workspaces = [Workspace(name: "Writing", hotKey: HotKeyCombo(keyCode: kVK_ANSI_1, modifiers: controlKey | optionKey),
                                      windows: [placement("pages", "Report")])]
        let data = try JSONEncoder().encode(store.config)
        let decoded = try JSONDecoder().decode(LayoutStore.Config.self, from: data)
        XCTAssertEqual(decoded.workspaces, store.workspaces)
    }

    func testConfigFromBeforeWorkspacesHasNone() throws {
        let old = """
        {"layouts": [], "showGridHotKey": null, "nextScreenHotKey": null, "previousScreenHotKey": null}
        """
        let decoded = try JSONDecoder().decode(LayoutStore.Config.self, from: Data(old.utf8))
        XCTAssertEqual(decoded.workspaces, [])
    }

    func testNewNamesDontRepeat() {
        let store = LayoutStore(persist: false)
        let first = store.newWorkspaceName()
        store.workspaces = [Workspace(name: first, windows: [])]
        XCTAssertNotEqual(store.newWorkspaceName(), first)
    }

    func testAWorkspaceShortcutCanBeADuplicate() {
        let store = LayoutStore(persist: false)
        store.workspaces = [Workspace(name: "W", hotKey: store.showGridHotKey, windows: [])]
        XCTAssertNotNil(store.warning(for: store.showGridHotKey))
    }
}
