import AppKit
import ApplicationServices

/// The window that had focus when Quadra was called.
struct TargetWindow {
    let app: NSRunningApplication
    let element: AXUIElement

    /// Current position and size (AppKit coordinates).
    var frame: CGRect? { WindowMover.axFrame(of: element).map { Geometry.flip($0) } }

    var title: String? { WindowMover.copy(element, kAXTitleAttribute) as? String }

    /// Windows in native full screen can't be moved or resized.
    var isFullScreen: Bool { (WindowMover.copy(element, "AXFullScreen") as? Bool) == true }
}

/// Reads and changes other apps' windows through the macOS Accessibility API.
enum WindowMover {
    /// Whether macOS has granted Quadra the Accessibility permission.
    /// With `prompt: true`, macOS shows the alert that leads to System Settings.
    static func isTrusted(prompt: Bool = false) -> Bool {
        AXIsProcessTrustedWithOptions(["AXTrustedCheckOptionPrompt": prompt] as CFDictionary)
    }

    static func focusedWindow() -> TargetWindow? {
        guard let app = NSWorkspace.shared.frontmostApplication,
              app.processIdentifier != ProcessInfo.processInfo.processIdentifier else { return nil }
        let axApp = AXUIElementCreateApplication(app.processIdentifier)
        for attribute in [kAXFocusedWindowAttribute, kAXMainWindowAttribute] {
            if let value = copy(axApp, attribute), CFGetTypeID(value) == AXUIElementGetTypeID() {
                return TargetWindow(app: app, element: unsafeBitCast(value, to: AXUIElement.self))
            }
        }
        return nil
    }

    static func copy(_ element: AXUIElement, _ attribute: String) -> CFTypeRef? {
        var value: CFTypeRef?
        let result = AXUIElementCopyAttributeValue(element, attribute as CFString, &value)
        return result == .success ? value : nil
    }

    /// Position and size in Accessibility coordinates (origin at the top left).
    static func axFrame(of window: AXUIElement) -> CGRect? {
        guard let pos = copy(window, kAXPositionAttribute), CFGetTypeID(pos) == AXValueGetTypeID(),
              let size = copy(window, kAXSizeAttribute), CFGetTypeID(size) == AXValueGetTypeID() else { return nil }
        var point = CGPoint.zero
        var dims = CGSize.zero
        AXValueGetValue(unsafeBitCast(pos, to: AXValue.self), .cgPoint, &point)
        AXValueGetValue(unsafeBitCast(size, to: AXValue.self), .cgSize, &dims)
        return CGRect(origin: point, size: dims)
    }

    /// Moves and resizes the window to `frame` (AppKit coordinates).
    /// `area` is the usable area of the destination display.
    static func move(_ target: TargetWindow, to frame: CGRect, within area: CGRect) {
        withEnhancedUIOff(target) {
            setSize(target, frame.size)
            setOrigin(target, frame)
            // Size again: some apps limit the size by the space left at the old position.
            setSize(target, frame.size)
        }
        // Some apps have a minimum or fixed size. If the window didn't take the requested size,
        // center it on the chosen area instead of leaving it stuck in a corner.
        if let actual = target.frame,
           abs(actual.width - frame.width) > 2 || abs(actual.height - frame.height) > 2 {
            setOrigin(target, Geometry.centered(actual.size, on: frame, within: area))
        }
    }

    /// Gives focus back to the window after the grid closes.
    static func focus(_ target: TargetWindow) {
        AXUIElementPerformAction(target.element, kAXRaiseAction as CFString)
        target.app.activate()
    }

    // MARK: Private

    private static func setSize(_ target: TargetWindow, _ size: CGSize) {
        var size = size
        guard let value = AXValueCreate(.cgSize, &size) else { return }
        AXUIElementSetAttributeValue(target.element, kAXSizeAttribute as CFString, value)
    }

    /// Places the window's top-left corner where `frame`'s top-left corner is.
    private static func setOrigin(_ target: TargetWindow, _ frame: CGRect) {
        var origin = Geometry.flip(frame).origin
        guard let value = AXValueCreate(.cgPoint, &origin) else { return }
        AXUIElementSetAttributeValue(target.element, kAXPositionAttribute as CFString, value)
    }

    /// Chrome, Electron and others animate and get the size wrong while this option is on,
    /// so it is turned off during the change and back on afterwards.
    private static func withEnhancedUIOff(_ target: TargetWindow, _ body: () -> Void) {
        let axApp = AXUIElementCreateApplication(target.app.processIdentifier)
        let attribute = "AXEnhancedUserInterface" as CFString
        let wasOn = (copy(axApp, attribute as String) as? Bool) == true
        if wasOn { AXUIElementSetAttributeValue(axApp, attribute, kCFBooleanFalse) }
        body()
        if wasOn { AXUIElementSetAttributeValue(axApp, attribute, kCFBooleanTrue) }
    }
}
