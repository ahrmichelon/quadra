import AppKit
import SwiftUI

/// Whether the Accessibility permission has been granted, watched while the welcome window is open.
final class WelcomeModel: ObservableObject {
    @Published var isTrusted = WindowMover.isTrusted()
    var shortcut = HotKeyCombo.showGridDefault.display
}

/// First-run window: explains why Quadra needs the Accessibility permission, opens System Settings,
/// and notices by itself when the permission is granted.
final class WelcomeWindowController {
    private let model = WelcomeModel()
    private var window: NSWindow?
    private var timer: Timer?
    /// Opens System Settings at Accessibility.
    var onOpenSettings: (() -> Void)?

    func show(shortcut: String?) {
        model.shortcut = shortcut ?? HotKeyCombo.showGridDefault.display
        model.isTrusted = WindowMover.isTrusted()
        if window == nil {
            let view = WelcomeView(model: model,
                                   onOpenSettings: { [weak self] in self?.onOpenSettings?() },
                                   onClose: { [weak self] in self?.close() })
            let window = NSWindow(contentViewController: NSHostingController(rootView: view))
            window.title = String(localized: "Welcome to Quadra")
            window.styleMask = [.titled, .closable]
            window.isReleasedWhenClosed = false
            window.center()
            self.window = window
        }
        NSApp.activate()
        window?.makeKeyAndOrderFront(nil)

        // Check once a second whether the permission arrived, so the user doesn't have to come back.
        timer?.invalidate()
        timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] timer in
            guard let self, self.window?.isVisible == true else { return timer.invalidate() }
            let trusted = WindowMover.isTrusted()
            guard trusted != self.model.isTrusted else { return }
            self.model.isTrusted = trusted
            if trusted {
                NSApp.activate()
                self.window?.makeKeyAndOrderFront(nil)
            }
        }
    }

    func close() {
        timer?.invalidate()
        window?.close()
    }
}

struct WelcomeView: View {
    @ObservedObject var model: WelcomeModel
    let onOpenSettings: () -> Void
    let onClose: () -> Void

    var body: some View {
        VStack(spacing: 14) {
            Image(nsImage: NSApp.applicationIconImage)
                .resizable()
                .frame(width: 72, height: 72)
            if model.isTrusted {
                Text("Quadra is ready")
                    .font(.title2.weight(.semibold))
                Text("Press \(model.shortcut) or click the grid icon in the menu bar to arrange the focused window.")
                    .multilineTextAlignment(.center)
                    .fixedSize(horizontal: false, vertical: true)
                Button("Done", action: onClose)
                    .keyboardShortcut(.defaultAction)
                    .padding(.top, 4)
            } else {
                Text("Allow Quadra to move windows")
                    .font(.title2.weight(.semibold))
                Text("To move and resize other apps' windows, macOS asks you to turn Quadra on in Accessibility. Quadra only reads the position, size and title of the focused window, and never sends them anywhere.")
                    .multilineTextAlignment(.center)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
                VStack(alignment: .leading, spacing: 8) {
                    Label("Click Open System Settings", systemImage: "1.circle.fill")
                    Label("Turn Quadra on in the list", systemImage: "2.circle.fill")
                    Label("That's it: Quadra notices by itself", systemImage: "3.circle.fill")
                }
                .padding(.vertical, 4)
                HStack(spacing: 12) {
                    Button("Not Now", action: onClose)
                    Button("Open System Settings", action: onOpenSettings)
                        .keyboardShortcut(.defaultAction)
                }
            }
        }
        .padding(28)
        .frame(width: 460)
    }
}
