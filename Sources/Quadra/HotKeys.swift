import AppKit
import Carbon

/// A key combination: key code plus modifiers (in the Carbon API format).
struct HotKeyCombo: Codable, Hashable {
    var keyCode: Int
    var modifiers: Int

    static let showGridDefault = HotKeyCombo(keyCode: kVK_ANSI_G, modifiers: controlKey | optionKey)
    static let nextScreenDefault = HotKeyCombo(keyCode: kVK_RightArrow, modifiers: controlKey | optionKey | cmdKey)
    static let previousScreenDefault = HotKeyCombo(keyCode: kVK_LeftArrow, modifiers: controlKey | optionKey | cmdKey)

    init(keyCode: Int, modifiers: Int) {
        self.keyCode = keyCode
        self.modifiers = modifiers
    }

    init(event: NSEvent) {
        keyCode = Int(event.keyCode)
        let flags = event.modifierFlags
        var m = 0
        if flags.contains(.control) { m |= controlKey }
        if flags.contains(.option) { m |= optionKey }
        if flags.contains(.shift) { m |= shiftKey }
        if flags.contains(.command) { m |= cmdKey }
        modifiers = m
    }

    /// Global shortcuts need ⌃, ⌥ or ⌘ (or an F key), otherwise they get in the way of typing.
    var isUsable: Bool {
        modifiers & (controlKey | optionKey | cmdKey) != 0 || Self.functionKeys[keyCode] != nil
    }

    /// Text such as "⌃⌥←".
    var display: String {
        var s = ""
        if modifiers & controlKey != 0 { s += "⌃" }
        if modifiers & optionKey != 0 { s += "⌥" }
        if modifiers & shiftKey != 0 { s += "⇧" }
        if modifiers & cmdKey != 0 { s += "⌘" }
        return s + Self.keyName(keyCode)
    }

    // MARK: Showing the shortcut in menu items

    var menuModifierMask: NSEvent.ModifierFlags {
        var f: NSEvent.ModifierFlags = []
        if modifiers & controlKey != 0 { f.insert(.control) }
        if modifiers & optionKey != 0 { f.insert(.option) }
        if modifiers & shiftKey != 0 { f.insert(.shift) }
        if modifiers & cmdKey != 0 { f.insert(.command) }
        return f
    }

    var menuKeyEquivalent: String? {
        func scalar(_ v: Int) -> String? { UnicodeScalar(UInt32(v)).map { String(Character($0)) } }
        switch keyCode {
        case kVK_LeftArrow: return scalar(NSLeftArrowFunctionKey)
        case kVK_RightArrow: return scalar(NSRightArrowFunctionKey)
        case kVK_UpArrow: return scalar(NSUpArrowFunctionKey)
        case kVK_DownArrow: return scalar(NSDownArrowFunctionKey)
        case kVK_Return: return "\r"
        case kVK_Space: return " "
        case kVK_Tab: return "\t"
        default:
            if let n = Self.functionKeys[keyCode] { return scalar(NSF1FunctionKey + n - 1) }
            let name = Self.keyName(keyCode)
            return name.count == 1 ? name.lowercased() : nil
        }
    }

    // MARK: Key names

    static let functionKeys: [Int: Int] = [
        kVK_F1: 1, kVK_F2: 2, kVK_F3: 3, kVK_F4: 4, kVK_F5: 5, kVK_F6: 6, kVK_F7: 7, kVK_F8: 8,
        kVK_F9: 9, kVK_F10: 10, kVK_F11: 11, kVK_F12: 12, kVK_F13: 13, kVK_F14: 14, kVK_F15: 15,
        kVK_F16: 16, kVK_F17: 17, kVK_F18: 18, kVK_F19: 19, kVK_F20: 20,
    ]

    private static let specialNames: [Int: String] = [
        kVK_LeftArrow: "←", kVK_RightArrow: "→", kVK_UpArrow: "↑", kVK_DownArrow: "↓",
        kVK_Return: "↩", kVK_ANSI_KeypadEnter: "⌤", kVK_Tab: "⇥", kVK_Space: String(localized: "Space"),
        kVK_Delete: "⌫", kVK_ForwardDelete: "⌦", kVK_Escape: "esc",
        kVK_Home: "↖", kVK_End: "↘", kVK_PageUp: "⇞", kVK_PageDown: "⇟",
    ]

    /// Name of the key on the current keyboard layout (e.g. "G", "←", "F5").
    static func keyName(_ keyCode: Int) -> String {
        if let s = specialNames[keyCode] { return s }
        if let n = functionKeys[keyCode] { return "F\(n)" }
        guard let source = TISCopyCurrentASCIICapableKeyboardLayoutInputSource()?.takeRetainedValue(),
              let ptr = TISGetInputSourceProperty(source, kTISPropertyUnicodeKeyLayoutData) else { return "#\(keyCode)" }
        let data = Unmanaged<CFData>.fromOpaque(ptr).takeUnretainedValue() as Data
        var deadKeys: UInt32 = 0
        var chars = [UniChar](repeating: 0, count: 4)
        var length = 0
        let status = data.withUnsafeBytes { raw -> OSStatus in
            guard let layout = raw.bindMemory(to: UCKeyboardLayout.self).baseAddress else { return -1 }
            return UCKeyTranslate(layout, UInt16(keyCode), UInt16(kUCKeyActionDisplay), 0,
                                  UInt32(LMGetKbdType()), OptionBits(kUCKeyTranslateNoDeadKeysBit),
                                  &deadKeys, chars.count, &length, &chars)
        }
        guard status == noErr, length > 0 else { return "#\(keyCode)" }
        return String(utf16CodeUnits: chars, count: length).uppercased()
    }
}

/// Registers the app's global shortcuts. Uses the Carbon API, which needs no Accessibility permission.
final class HotKeyCenter {
    static let shared = HotKeyCenter()

    /// Called after every registration with the shortcuts that could not be used.
    var onUpdate: ((Set<HotKeyCombo>) -> Void)?

    private var bindings: [UInt32: (combo: HotKeyCombo, action: () -> Void)] = [:]
    private var refs: [EventHotKeyRef] = []
    private var suspended = false

    private init() {
        var spec = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        InstallEventHandler(GetApplicationEventTarget(), { _, event, _ in
            var hotKeyID = EventHotKeyID()
            let status = GetEventParameter(event, EventParamName(kEventParamDirectObject),
                                           EventParamType(typeEventHotKeyID), nil,
                                           MemoryLayout<EventHotKeyID>.size, nil, &hotKeyID)
            if status == noErr {
                let id = hotKeyID.id
                DispatchQueue.main.async { HotKeyCenter.shared.fire(id) }
            }
            return noErr
        }, 1, &spec, nil, nil)
    }

    /// Replaces all shortcuts at once.
    func set(_ list: [(HotKeyCombo, () -> Void)]) {
        unregisterAll()
        bindings = [:]
        for (i, entry) in list.enumerated() {
            bindings[UInt32(i + 1)] = (entry.0, entry.1)
        }
        if !suspended { registerAll() }
    }

    /// Turns the shortcuts off while the user records a new one (otherwise the old one would fire).
    func suspend() {
        suspended = true
        unregisterAll()
    }

    func resume() {
        guard suspended else { return }
        suspended = false
        registerAll()
    }

    private func registerAll() {
        unregisterAll()
        var failed = Set<HotKeyCombo>()
        for (id, entry) in bindings.sorted(by: { $0.key < $1.key }) {
            var ref: EventHotKeyRef?
            let hotKeyID = EventHotKeyID(signature: 0x5144_5241, id: id) // "QDRA"
            let status = RegisterEventHotKey(UInt32(entry.combo.keyCode), UInt32(entry.combo.modifiers),
                                             hotKeyID, GetApplicationEventTarget(), 0, &ref)
            if status == noErr, let ref { refs.append(ref) } else { failed.insert(entry.combo) }
        }
        onUpdate?(failed)
    }

    private func unregisterAll() {
        refs.forEach { UnregisterEventHotKey($0) }
        refs = []
    }

    private func fire(_ id: UInt32) {
        bindings[id]?.action()
    }
}
