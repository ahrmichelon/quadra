import AppKit
import ServiceManagement
import SwiftUI

final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    private var statusItem: NSStatusItem!
    private let settingsMenu = NSMenu()
    private let store = LayoutStore()
    private lazy var panel = GridPanel()
    /// Outline around the chosen window.
    private lazy var targetHighlight = OverlayWindow(style: .outline)
    /// Area painted on screen showing where the window will go.
    private lazy var dropPreview = OverlayWindow(style: .fill)
    private var settingsWindow: NSWindow?
    private var target: TargetWindow?
    /// The display the grid stands for (the window's display, or the one picked in the panel).
    private var targetScreen: NSScreen?
    private var menuIsOpen = false

    /// Ready-made grid sizes in the menu (columns × rows).
    private let presets: [(cols: Int, rows: Int)] = [
        (2, 2), (3, 2), (3, 3), (4, 3), (4, 4), (6, 4), (6, 6), (8, 6), (12, 8),
    ]

    func applicationDidFinishLaunching(_ notification: Notification) {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        if let button = statusItem.button {
            button.image = BrandIcon.menuBar()
            button.toolTip = String(localized: "Click for the grid · right-click for the menu")
            button.target = self
            button.action = #selector(statusItemClicked)
            button.sendAction(on: [.leftMouseUp, .rightMouseUp])
        }
        settingsMenu.delegate = self

        panel.onPick = { [weak self] unit in self?.applyFromPanel(unit) }
        panel.onCancel = { [weak self] in
            if let t = self?.target { WindowMover.focus(t) }
        }
        panel.content.onGear = { [weak self] button in self?.showSettingsMenu(from: button) }
        panel.content.onScreenChange = { [weak self] index in self?.pickScreen(at: index) }
        panel.onResignKey = { [weak self] in
            // Wait for macOS to switch the frontmost app before checking which window was clicked.
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) { self?.panelLostFocus() }
        }
        panel.onPreview = { [weak self] unit in self?.showDropPreview(unit) }
        panel.onHide = { [weak self] in
            self?.targetHighlight.orderOut(nil)
            self?.dropPreview.orderOut(nil)
        }

        // A display was connected, disconnected or rearranged: the open grid no longer matches.
        NotificationCenter.default.addObserver(forName: NSApplication.didChangeScreenParametersNotification,
                                               object: nil, queue: .main) { [weak self] _ in
            self?.panel.orderOut(nil)
        }

        HotKeyCenter.shared.onUpdate = { [weak self] failed in self?.store.unavailable = failed }
        store.onChange = { [weak self] in self?.registerHotKeys() }
        registerHotKeys()

        // The first time, macOS shows the alert that leads to System Settings.
        if !WindowMover.isTrusted() { _ = WindowMover.isTrusted(prompt: true) }
    }

    /// Registers the app's shortcuts and each layout's shortcut.
    private func registerHotKeys() {
        var list: [(HotKeyCombo, () -> Void)] = []
        if let combo = store.showGridHotKey {
            list.append((combo, { [weak self] in self?.toggleGrid(fromStatusItem: false) }))
        }
        if let combo = store.nextScreenHotKey {
            list.append((combo, { [weak self] in self?.moveToAdjacentScreen(1) }))
        }
        if let combo = store.previousScreenHotKey {
            list.append((combo, { [weak self] in self?.moveToAdjacentScreen(-1) }))
        }
        for layout in store.layouts {
            guard let combo = layout.hotKey else { continue }
            let id = layout.id
            list.append((combo, { [weak self] in self?.applyLayout(id: id) }))
        }
        HotKeyCenter.shared.set(list)
    }

    // MARK: Menu bar icon

    @objc private func statusItemClicked() {
        let event = NSApp.currentEvent
        let wantsMenu = event?.type == .rightMouseUp
            || event?.modifierFlags.contains(.control) == true
            || event?.modifierFlags.contains(.option) == true
        if wantsMenu {
            if panel.isVisible { panel.orderOut(nil) }
            showMenuFromStatusItem()
            return
        }
        // If this same click just closed the panel, don't reopen it.
        if Date().timeIntervalSince(panel.lastHidden) < 0.3 { return }
        toggleGrid(fromStatusItem: true)
    }

    private func showMenuFromStatusItem() {
        // The menu is attached to the icon only for this click; a normal click opens the grid.
        statusItem.menu = settingsMenu
        statusItem.button?.performClick(nil)
        statusItem.menu = nil
    }

    private func showSettingsMenu(from button: NSButton) {
        let y = button.isFlipped ? button.bounds.maxY + 4 : -4
        settingsMenu.popUp(positioning: nil, at: NSPoint(x: 0, y: y), in: button)
    }

    // MARK: Grid

    private func toggleGrid(fromStatusItem: Bool) {
        if panel.isVisible { panel.orderOut(nil); return }
        guard WindowMover.isTrusted() else { openAccessibilitySettings(); return }
        guard let window = WindowMover.focusedWindow(), presentGrid(for: window) else {
            // No focused window: from the icon, show the menu; from the shortcut, just beep.
            if fromStatusItem { showMenuFromStatusItem() } else { NSSound.beep() }
            return
        }
    }

    /// Opens (or updates) the grid to move `window`. With `screen`, the grid stands for that display
    /// instead of the window's own, and the panel stays where it is. False if the window can't be read.
    @discardableResult
    private func presentGrid(for window: TargetWindow, on screen: NSScreen? = nil) -> Bool {
        guard let frame = window.frame, let windowScreen = Geometry.screen(containing: frame) else { return false }
        let gridScreen = screen ?? windowScreen
        target = window
        targetScreen = gridScreen

        // The dashed outline only makes sense when the grid stands for the window's own display.
        let v = gridScreen.visibleFrame
        let current: CGRect? = gridScreen == windowScreen
            ? CGRect(x: (frame.minX - v.minX) / v.width, y: (v.maxY - frame.maxY) / v.height,
                     width: frame.width / v.width, height: frame.height / v.height)
            : nil

        let screens = Geometry.orderedScreens
        let names = screens.count > 1
            ? screens.enumerated().map { "\($0.offset + 1) · \($0.element.localizedName)" }
            : []
        let selected = screens.firstIndex(of: gridScreen) ?? 0

        let center: CGPoint
        if screen != nil, panel.isVisible {
            center = CGPoint(x: panel.frame.midX, y: panel.frame.midY)
        } else {
            center = CGPoint(x: windowScreen.visibleFrame.midX, y: windowScreen.visibleFrame.midY)
        }

        let appName = window.app.localizedName ?? "App"
        let title = (window.title?.isEmpty == false) ? "\(appName) — \(window.title!)" : appName
        dropPreview.orderOut(nil)
        targetHighlight.show(frame: frame.insetBy(dx: -4, dy: -4))
        panel.show(title: title, current: current, gridSize: Settings.panelGridSize(screen: v.size),
                   screenNames: names, selectedScreen: selected, center: center)
        return true
    }

    /// The user picked another display in the panel.
    private func pickScreen(at index: Int) {
        let screens = Geometry.orderedScreens
        guard let window = target, screens.indices.contains(index) else { return }
        presentGrid(for: window, on: screens[index])
    }

    /// The panel lost focus. If the user clicked another window, that window becomes the one to move;
    /// if they clicked outside any window (the desktop, for example), the grid closes.
    private func panelLostFocus() {
        guard panel.isVisible, !panel.isKeyWindow, !menuIsOpen else { return }
        if let window = WindowMover.focusedWindow(), presentGrid(for: window) { return }
        panel.orderOut(nil)
    }

    /// Paints the area where the window will go (nil clears it).
    private func showDropPreview(_ unit: CGRect?) {
        guard let unit, let screen = targetScreen else {
            dropPreview.orderOut(nil)
            return
        }
        dropPreview.show(frame: Geometry.frame(for: unit, in: screen.visibleFrame, gap: CGFloat(Settings.gap)))
    }

    private func applyFromPanel(_ unit: CGRect) {
        panel.orderOut(nil)
        guard let window = target, let screen = targetScreen else { return }
        move(window, to: unit, on: screen)
    }

    // MARK: Layouts and displays

    /// The window to act on: the grid's window when the panel is open, otherwise the focused window.
    private func windowForAction() -> TargetWindow? {
        if panel.isVisible {
            panel.orderOut(nil)
            return target
        }
        guard WindowMover.isTrusted() else { openAccessibilitySettings(); return nil }
        return WindowMover.focusedWindow()
    }

    /// Moves the window to the layout's area on the display it is on.
    private func applyLayout(id: UUID) {
        guard let layout = store.layout(id: id) else { return }
        guard let window = windowForAction(), let frame = window.frame,
              let screen = Geometry.screen(containing: frame) else {
            NSSound.beep()
            return
        }
        move(window, to: layout.unitRect, on: screen)
    }

    /// Moves the window to the next (`step` = 1) or previous (`step` = -1) display,
    /// keeping its relative position and size.
    private func moveToAdjacentScreen(_ step: Int) {
        let screens = Geometry.orderedScreens
        guard screens.count > 1, let window = windowForAction(), !window.isFullScreen,
              let frame = window.frame, let current = Geometry.screen(containing: frame),
              let i = screens.firstIndex(of: current) else {
            NSSound.beep()
            return
        }
        let next = screens[(i + step + screens.count) % screens.count]
        let newFrame = Geometry.map(frame, from: current.visibleFrame, to: next.visibleFrame)
        WindowMover.move(window, to: newFrame, within: next.visibleFrame)
        WindowMover.focus(window)
    }

    private func move(_ window: TargetWindow, to unit: CGRect, on screen: NSScreen) {
        guard !window.isFullScreen else { NSSound.beep(); return }
        let frame = Geometry.frame(for: unit, in: screen.visibleFrame, gap: CGFloat(Settings.gap))
        WindowMover.move(window, to: frame, within: screen.visibleFrame)
        WindowMover.focus(window)
    }

    // MARK: Menu

    func menuWillOpen(_ menu: NSMenu) { menuIsOpen = true }

    func menuDidClose(_ menu: NSMenu) {
        menuIsOpen = false
        // The gear menu may have taken focus from the panel; give it back so esc works.
        if panel.isVisible && !panel.isKeyWindow { panel.makeKey() }
    }

    func menuNeedsUpdate(_ menu: NSMenu) {
        menu.removeAllItems()

        if !panel.isVisible {
            menu.addItem(item(String(localized: "Show Grid"), #selector(showGridFromMenu), shortcut: store.showGridHotKey))
            if let warning = store.warning(for: store.showGridHotKey) {
                menu.addItem(info(String(localized: "⚠︎ Grid shortcut: \(warning.lowercased())")))
            }
            menu.addItem(.separator())
        }

        if !store.layouts.isEmpty {
            menu.addItem(.sectionHeader(title: String(localized: "Layouts")))
            for layout in store.layouts {
                let title = layout.name.isEmpty ? String(localized: "Untitled") : layout.name
                let it = item(title, #selector(applyLayoutFromMenu(_:)), shortcut: layout.hotKey)
                it.representedObject = layout.id
                menu.addItem(it)
            }
            menu.addItem(.separator())
        }

        if NSScreen.screens.count > 1 {
            menu.addItem(.sectionHeader(title: String(localized: "Displays")))
            menu.addItem(item(String(localized: "Move to Next Display"), #selector(moveToNextScreenFromMenu),
                              shortcut: store.nextScreenHotKey))
            menu.addItem(item(String(localized: "Move to Previous Display"), #selector(moveToPreviousScreenFromMenu),
                              shortcut: store.previousScreenHotKey))
            menu.addItem(.separator())
        }

        let sizes = NSMenu()
        sizes.addItem(info(String(localized: "Columns × rows")))
        for (i, p) in presets.enumerated() {
            let it = item("\(p.cols) × \(p.rows)", #selector(setPreset(_:)))
            it.tag = i
            it.state = (Settings.columns, Settings.rows) == p ? .on : .off
            sizes.addItem(it)
        }
        if !presets.contains(where: { $0 == (Settings.columns, Settings.rows) }) {
            let custom = info(String(localized: "\(Settings.columns) × \(Settings.rows) (custom)"))
            custom.state = .on
            sizes.addItem(custom)
        }
        sizes.addItem(.separator())
        sizes.addItem(info(String(localized: "Fine-tune with the controls on the grid")))
        menu.addItem(submenu(String(localized: "Grid Size"), sizes))

        let gaps = NSMenu()
        for g in [0, 4, 8, 16] {
            let it = item(g == 0 ? String(localized: "None") : String(localized: "\(g) points"), #selector(setGap(_:)))
            it.tag = g
            it.state = Settings.gap == g ? .on : .off
            gaps.addItem(it)
        }
        menu.addItem(submenu(String(localized: "Window Spacing"), gaps))
        menu.addItem(.separator())

        if WindowMover.isTrusted() {
            menu.addItem(info(String(localized: "✓ Accessibility allowed")))
        } else {
            menu.addItem(item(String(localized: "Allow Accessibility…"), #selector(openAccessibilitySettings)))
        }
        let login = item(String(localized: "Open at Login"), #selector(toggleLaunchAtLogin))
        login.state = SMAppService.mainApp.status == .enabled ? .on : .off
        menu.addItem(login)
        let settings = item(String(localized: "Settings & Layouts…"), #selector(openSettings))
        settings.keyEquivalent = ","
        menu.addItem(settings)
        menu.addItem(.separator())

        menu.addItem(item(String(localized: "Support Quadra…"), #selector(openSupport)))
        menu.addItem(item(String(localized: "About Quadra"), #selector(showAbout)))
        menu.addItem(NSMenuItem(title: String(localized: "Quit Quadra"),
                                action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q"))
    }

    private func item(_ title: String, _ action: Selector, shortcut: HotKeyCombo? = nil) -> NSMenuItem {
        let it = NSMenuItem(title: title, action: action, keyEquivalent: "")
        it.target = self
        if let shortcut, let key = shortcut.menuKeyEquivalent {
            it.keyEquivalent = key
            it.keyEquivalentModifierMask = shortcut.menuModifierMask
        }
        return it
    }

    private func info(_ title: String) -> NSMenuItem {
        let it = NSMenuItem(title: title, action: nil, keyEquivalent: "")
        it.isEnabled = false
        return it
    }

    private func submenu(_ title: String, _ menu: NSMenu) -> NSMenuItem {
        let it = NSMenuItem(title: title, action: nil, keyEquivalent: "")
        it.submenu = menu
        return it
    }

    /// Menu actions run after the menu closes, so focus is back on the window's app.
    private func afterMenuCloses(_ action: @escaping () -> Void) {
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.15, execute: action)
    }

    @objc private func showGridFromMenu() {
        afterMenuCloses { [weak self] in self?.toggleGrid(fromStatusItem: true) }
    }

    @objc private func applyLayoutFromMenu(_ sender: NSMenuItem) {
        guard let id = sender.representedObject as? UUID else { return }
        afterMenuCloses { [weak self] in self?.applyLayout(id: id) }
    }

    @objc private func moveToNextScreenFromMenu() {
        afterMenuCloses { [weak self] in self?.moveToAdjacentScreen(1) }
    }

    @objc private func moveToPreviousScreenFromMenu() {
        afterMenuCloses { [weak self] in self?.moveToAdjacentScreen(-1) }
    }

    @objc private func setPreset(_ sender: NSMenuItem) {
        let p = presets[sender.tag]
        Settings.columns = p.cols
        Settings.rows = p.rows
        if panel.isVisible { panel.content.setGridSize(cols: p.cols, rows: p.rows) }
    }

    @objc private func setGap(_ sender: NSMenuItem) { Settings.gap = sender.tag }

    @objc private func openAccessibilitySettings() {
        _ = WindowMover.isTrusted(prompt: true)
        let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!
        NSWorkspace.shared.open(url)
    }

    @objc private func openSettings() {
        if panel.isVisible { panel.orderOut(nil) }
        if settingsWindow == nil {
            let window = NSWindow(contentViewController: NSHostingController(rootView: SettingsView(store: store)))
            window.title = String(localized: "Quadra Settings")
            window.styleMask = [.titled, .closable, .miniaturizable]
            window.isReleasedWhenClosed = false
            window.center()
            settingsWindow = window
        }
        NSApp.activate()
        settingsWindow?.makeKeyAndOrderFront(nil)
    }

    @objc private func toggleLaunchAtLogin() {
        do {
            if SMAppService.mainApp.status == .enabled {
                try SMAppService.mainApp.unregister()
            } else {
                try SMAppService.mainApp.register()
            }
        } catch {
            let alert = NSAlert()
            alert.messageText = String(localized: "Couldn't change Open at Login")
            alert.informativeText = error.localizedDescription + "\n\n"
                + String(localized: "Tip: move Quadra.app to the Applications folder and try again.")
            NSApp.activate()
            alert.runModal()
        }
    }

    @objc private func openSupport() {
        if panel.isVisible { panel.orderOut(nil) }
        NSWorkspace.shared.open(Links.support)
    }

    @objc private func showAbout() {
        if panel.isVisible { panel.orderOut(nil) }
        NSApp.activate()
        NSApp.orderFrontStandardAboutPanel(options: [
            .credits: NSAttributedString(string: String(localized: "Arrange your Mac's windows on a grid."),
                                         attributes: [.font: NSFont.systemFont(ofSize: 11),
                                                      .foregroundColor: NSColor.secondaryLabelColor]),
        ])
    }
}
