import AppKit

let app = NSApplication.shared

// Snapshot mode: draws parts of the app to PNG images and quits (opens no windows, asks for nothing).
// Usage: Quadra --snapshot path.png [dark]
//        Quadra --snapshot-settings path.png [dark]
// Add `-AppleLanguages "(en)"` or `"(pt-BR)"` to pick the language.
let args = CommandLine.arguments
if let i = args.firstIndex(of: "--snapshot"), i + 1 < args.count {
    Snapshot.render(to: args[i + 1], dark: args.contains("dark"))
    exit(0)
}
if let i = args.firstIndex(of: "--snapshot-settings"), i + 1 < args.count {
    Snapshot.renderSettings(to: args[i + 1], dark: args.contains("dark"))
    exit(0)
}

let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.accessory) // menu bar only, no Dock icon
app.run()
