import AppKit
import ApplicationServices

/// Where one window goes when a workspace is restored.
struct WindowPlacement: Codable, Equatable {
    var bundleID: String
    var appName: String
    /// The window's title when it was saved (may be empty). Used to tell an app's windows apart.
    var title: String
    /// The display it was on, by name and by position in `Geometry.orderedScreens`.
    var display: String
    var displayIndex: Int
    /// The window's area as fractions of the display's usable area (0 to 1, origin at the top left).
    var unitRect: CGRect
    /// The file open in the window (a file URL), when the app says which one. Lets a document
    /// that was closed be opened again. Missing in workspaces saved before 1.3.0.
    var document: String? = nil
}

/// A saved arrangement of several apps' windows, restored with one shortcut.
struct Workspace: Codable, Identifiable, Equatable {
    var id = UUID()
    var name: String
    var hotKey: HotKeyCombo?
    var windows: [WindowPlacement]

    /// The apps in the workspace, in order, without repeats.
    var appNames: [String] {
        var seen = Set<String>()
        return windows.compactMap { seen.insert($0.bundleID).inserted ? $0.appName : nil }
    }
}

/// An open window, as seen when matching a workspace (the pure part, easy to test).
struct OpenWindow: Equatable {
    var bundleID: String
    var title: String
    var document: String? = nil
}

enum WorkspaceMatcher {
    /// Pairs saved placements with open windows of the same app: first the window with the same
    /// document, then the one with the same title, then the app's remaining windows front to back.
    /// A placement with a document only takes a window that matches it (never, say, an Open
    /// panel). Returns (placement index, window index) pairs; each window is used at most once.
    static func match(_ placements: [WindowPlacement], to windows: [OpenWindow]) -> [(placement: Int, window: Int)] {
        var used = Set<Int>()
        var pairs: [(placement: Int, window: Int)] = []
        func take(_ p: Int, _ fits: (OpenWindow) -> Bool) -> Bool {
            guard let w = windows.indices.first(where: {
                !used.contains($0) && windows[$0].bundleID == placements[p].bundleID && fits(windows[$0])
            }) else { return false }
            used.insert(w)
            pairs.append((p, w))
            return true
        }
        var rest = placements.indices.filter { p in
            guard let document = placements[p].document else { return true }
            return !take(p) { $0.document == document }
        }
        rest = rest.filter { p in
            let title = placements[p].title
            return title.isEmpty || !take(p) { $0.title == title }
        }
        for p in rest where placements[p].document == nil {
            _ = take(p) { _ in true }
        }
        return pairs.sorted { $0.placement < $1.placement }
    }

    /// The display to use for a placement: the one with the same name, otherwise the one in the
    /// same position, otherwise the first.
    static func displayIndex(for placement: WindowPlacement, among names: [String]) -> Int {
        if let i = names.firstIndex(of: placement.display) { return i }
        if names.indices.contains(placement.displayIndex) { return placement.displayIndex }
        return 0
    }
}

/// Reads all open windows and puts them back where a workspace says.
enum WorkspaceArranger {
    /// Standard windows of the other apps that have a Dock icon, front to back per app.
    /// Minimized windows are left out unless `includingMinimized`.
    static func openWindows(includingMinimized: Bool = false) -> [TargetWindow] {
        let me = ProcessInfo.processInfo.processIdentifier
        return NSWorkspace.shared.runningApplications
            .filter { $0.activationPolicy == .regular && $0.processIdentifier != me && !$0.isHidden }
            .flatMap { app -> [TargetWindow] in
                let axApp = AXUIElementCreateApplication(app.processIdentifier)
                // An app that doesn't answer must not freeze Quadra.
                AXUIElementSetMessagingTimeout(axApp, 1)
                guard let value = WindowMover.copy(axApp, kAXWindowsAttribute),
                      CFGetTypeID(value) == CFArrayGetTypeID(),
                      let elements = value as? [AXUIElement] else { return [] }
                return elements.compactMap { element in
                    // A minimized window says it is a dialog, so it is recognized by being minimized.
                    let minimized = isMinimized(element)
                    guard minimized ? includingMinimized
                                    : (WindowMover.copy(element, kAXSubroleAttribute) as? String) == kAXStandardWindowSubrole
                    else { return nil }
                    let window = TargetWindow(app: app, element: element)
                    return window.isFullScreen ? nil : window
                }
            }
    }

    private static func isMinimized(_ element: AXUIElement) -> Bool {
        (WindowMover.copy(element, kAXMinimizedAttribute) as? Bool) == true
    }

    /// The file open in a window, as a file URL (nil for web pages and windows without a file).
    private static func document(of window: TargetWindow) -> String? {
        guard let url = WindowMover.copy(window.element, kAXDocumentAttribute) as? String,
              url.hasPrefix("file://") else { return nil }
        return url
    }

    /// The saved files that still exist, as URLs.
    private static func existingFiles(_ documents: [String?]) -> [URL] {
        var seen = Set<String>()
        return documents.compactMap { $0 }.filter { seen.insert($0).inserted }.compactMap { URL(string: $0) }
            .filter { FileManager.default.fileExists(atPath: $0.path) }
    }

    /// Opens files with an app (launching it if needed), or just the app when there are none.
    private static func open(_ files: [URL], with bundleID: String) {
        guard let appURL = NSWorkspace.shared.urlForApplication(withBundleIdentifier: bundleID) else { return }
        let configuration = NSWorkspace.OpenConfiguration()
        configuration.activates = false
        guard !files.isEmpty else {
            NSWorkspace.shared.openApplication(at: appURL, configuration: configuration)
            return
        }
        NSWorkspace.shared.open(files, withApplicationAt: appURL, configuration: configuration) { _, error in
            // Not allowed to pass the files (the App Store version can't): open the app alone.
            if error != nil {
                DispatchQueue.main.async { NSWorkspace.shared.openApplication(at: appURL, configuration: configuration) }
            }
        }
    }

    /// Where the open windows are now.
    static func capture() -> [WindowPlacement] {
        let screens = Geometry.orderedScreens
        return openWindows().compactMap { window in
            guard let bundleID = window.app.bundleIdentifier, let frame = window.frame,
                  let screen = Geometry.screen(containing: frame) else { return nil }
            return WindowPlacement(bundleID: bundleID, appName: window.app.localizedName ?? bundleID,
                                   title: window.title ?? "", display: screen.localizedName,
                                   displayIndex: screens.firstIndex(of: screen) ?? 0,
                                   unitRect: Geometry.unit(of: frame, in: screen.visibleFrame),
                                   document: document(of: window))
        }
    }

    /// Opens the workspace's apps that aren't running, with their documents (and shows the hidden
    /// ones), then puts each window in its place and brings them to the front. Apps that were just
    /// opened and windows coming back from the Dock take a moment, so it tries again a few times.
    /// `completion` gets how many windows were moved.
    static func restore(_ workspace: Workspace, completion: @escaping (Int) -> Void) {
        var waiting = false
        /// Apps opened or shown here: their windows are on the way, so their documents aren't reopened.
        var launched = Set<String>()
        for bundleID in Set(workspace.windows.map(\.bundleID)) {
            let running = NSRunningApplication.runningApplications(withBundleIdentifier: bundleID)
            if running.isEmpty {
                open(existingFiles(workspace.windows.filter { $0.bundleID == bundleID }.map(\.document)), with: bundleID)
                launched.insert(bundleID)
                waiting = true
            }
            for app in running where app.isHidden {
                app.unhide()
                launched.insert(bundleID)
                waiting = true
            }
        }

        var pending = Array(workspace.windows.indices)
        var found = Set<Int>() // placements whose window exists (even if still minimized)
        var placed: [TargetWindow] = []
        func attempt() {
            let windows = openWindows(includingMinimized: true)
                .filter { window in !placed.contains { CFEqual($0.element, window.element) } }
            let current = windows.map {
                OpenWindow(bundleID: $0.app.bundleIdentifier ?? "", title: $0.title ?? "", document: document(of: $0))
            }
            let screens = Geometry.orderedScreens
            guard !screens.isEmpty else { return }
            let names = screens.map(\.localizedName)
            let placements = pending.map { workspace.windows[$0] }
            var done = Set<Int>()
            for pair in WorkspaceMatcher.match(placements, to: current) {
                let window = windows[pair.window]
                found.insert(pending[pair.placement])
                if isMinimized(window.element) {
                    // It comes back from the Dock first, and is moved on the next try.
                    AXUIElementSetAttributeValue(window.element, kAXMinimizedAttribute as CFString, kCFBooleanFalse)
                    waiting = true
                    continue
                }
                let placement = placements[pair.placement]
                let screen = screens[WorkspaceMatcher.displayIndex(for: placement, among: names)]
                let frame = Geometry.frame(for: placement.unitRect, in: screen.visibleFrame, gap: 0)
                WindowMover.move(window, to: frame, within: screen.visibleFrame)
                placed.append(window)
                done.insert(pending[pair.placement])
            }
            pending.removeAll { done.contains($0) }
        }

        func finish() {
            // Bring the workspace's apps to the front, the first saved window on top.
            for window in placed.reversed() {
                AXUIElementPerformAction(window.element, kAXRaiseAction as CFString)
            }
            var activated = Set<pid_t>()
            for window in placed.reversed() where activated.insert(window.app.processIdentifier).inserted {
                window.app.activate()
            }
            if let first = placed.first { WindowMover.focus(first) }
            completion(placed.count)
        }

        attempt()
        // Documents closed in apps that are running: open them again.
        let closed = pending.filter { !found.contains($0) }.map { workspace.windows[$0] }
            .filter { $0.document != nil && !launched.contains($0.bundleID) }
        for (bundleID, placements) in Dictionary(grouping: closed, by: \.bundleID) {
            let files = existingFiles(placements.map(\.document))
            if !files.isEmpty { open(files, with: bundleID); waiting = true }
        }
        guard waiting, !pending.isEmpty else { return finish() }
        // Try again for about 10 seconds, stopping as soon as every window is in place.
        var gaps: [TimeInterval] = [0.7, 0.8, 1.5, 3, 4]
        func retry() {
            DispatchQueue.main.asyncAfter(deadline: .now() + gaps.removeFirst()) {
                attempt()
                if pending.isEmpty || gaps.isEmpty { finish() } else { retry() }
            }
        }
        retry()
    }
}
