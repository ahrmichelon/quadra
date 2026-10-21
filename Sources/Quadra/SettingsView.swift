import AppKit
import SwiftUI

/// Settings window: the app's shortcuts, the panel's look and the list of layouts.
struct SettingsView: View {
    @ObservedObject var store: LayoutStore
    @State private var selectedID: UUID?
    @AppStorage("panelOpacity") private var panelOpacity = Settings.defaultPanelOpacity
    @AppStorage("panelSize") private var panelSize = Settings.defaultPanelSize
    @AppStorage("panelShape") private var panelShape = PanelShape.fourThree

    init(store: LayoutStore) {
        self.store = store
        _selectedID = State(initialValue: store.layouts.first?.id)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 12) {
                Image(nsImage: NSApp.applicationIconImage)
                    .resizable()
                    .frame(width: 48, height: 48)
                VStack(alignment: .leading, spacing: 2) {
                    Text(verbatim: "Quadra").font(.title2.weight(.semibold))
                    Text("Arrange your Mac's windows on a grid · version \(appVersion)")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                }
                Spacer()
                Link(destination: Links.support) {
                    Label("Support Quadra", systemImage: "heart")
                }
            }

            Divider()

            Grid(alignment: .leading, horizontalSpacing: 10, verticalSpacing: 10) {
                shortcutRow("Show the grid", $store.showGridHotKey)
                shortcutRow("Move to the next display", $store.nextScreenHotKey)
                shortcutRow("Move to the previous display", $store.previousScreenHotKey)
                GridRow {
                    Text("Grid shape")
                    Picker("Grid shape", selection: $panelShape) {
                        ForEach(PanelShape.allCases) { Text(verbatim: $0.title).tag($0) }
                    }
                    .pickerStyle(.segmented)
                    .labelsHidden()
                    .fixedSize()
                }
                GridRow {
                    Text("Grid width")
                    HStack(spacing: 10) {
                        Slider(value: $panelSize, in: Settings.panelSizeRange, step: 10)
                            .frame(width: 200)
                        Text("\(Int(panelSize)) points")
                            .monospacedDigit()
                            .foregroundStyle(.secondary)
                    }
                }
                GridRow {
                    Text("Grid opacity")
                    HStack(spacing: 10) {
                        Slider(value: $panelOpacity, in: 0.2...1.0)
                            .frame(width: 200)
                        Text(verbatim: "\(Int((panelOpacity * 100).rounded()))%")
                            .monospacedDigit()
                            .foregroundStyle(.secondary)
                            .frame(width: 44, alignment: .leading)
                        Text("Lower lets you see the apps behind it.")
                            .font(.callout)
                            .foregroundStyle(.secondary)
                    }
                }
            }

            Divider()

            VStack(alignment: .leading, spacing: 3) {
                Text("Layouts").font(.headline)
                Text("Each layout moves the focused window to an area of the screen, with its own shortcut.")
                    .font(.callout)
                    .foregroundStyle(.secondary)
            }

            HStack(alignment: .top, spacing: 20) {
                layoutList
                editor
            }
        }
        .padding(20)
        .frame(width: 790, height: 760)
    }

    private func shortcutRow(_ title: LocalizedStringKey, _ combo: Binding<HotKeyCombo?>) -> some View {
        GridRow {
            Text(title).gridColumnAlignment(.trailing)
            HStack(spacing: 10) {
                HotKeyField(combo: combo)
                WarningLabel(text: store.warning(for: combo.wrappedValue))
            }
        }
    }

    private var appVersion: String {
        Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "1.0"
    }

    // MARK: List

    private var layoutList: some View {
        VStack(alignment: .leading, spacing: 0) {
            List(selection: $selectedID) {
                ForEach(store.layouts) { layout in
                    LayoutRow(layout: layout, hasWarning: store.warning(for: layout.hotKey) != nil)
                        .tag(layout.id)
                }
                .onMove { store.layouts.move(fromOffsets: $0, toOffset: $1) }
            }
            .listStyle(.bordered(alternatesRowBackgrounds: false))

            HStack(spacing: 0) {
                Button(action: addLayout) {
                    Image(systemName: "plus").frame(width: 26, height: 22)
                }
                .help("New layout")
                Button(action: removeLayout) {
                    Image(systemName: "minus").frame(width: 26, height: 22)
                }
                .help("Delete the selected layout")
                .disabled(selectedID == nil)
                Spacer()
            }
            .buttonStyle(.borderless)
            .padding(.top, 4)
        }
        .frame(width: 250)
    }

    private func addLayout() {
        let new = SavedLayout(name: String(localized: "New layout"), cols: 6, rows: 6,
                              c0: 0, r0: 0, c1: 2, r1: 5, hotKey: nil)
        store.layouts.append(new)
        selectedID = new.id
    }

    private func removeLayout() {
        guard let id = selectedID, let i = store.layouts.firstIndex(where: { $0.id == id }) else { return }
        store.layouts.remove(at: i)
        selectedID = store.layouts.indices.contains(i) ? store.layouts[i].id : store.layouts.last?.id
    }

    // MARK: Editor

    @ViewBuilder private var editor: some View {
        if let id = selectedID, let layout = binding(for: id) {
            LayoutEditor(layout: layout, warning: store.warning(for: layout.wrappedValue.hotKey))
                .id(id)
        } else {
            Text("Choose a layout from the list or click + to create one.")
                .foregroundStyle(.secondary)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
    }

    /// Access to the layout by id (safe even if it is deleted while on screen).
    private func binding(for id: UUID) -> Binding<SavedLayout>? {
        guard let current = store.layout(id: id) else { return nil }
        return Binding(
            get: { store.layout(id: id) ?? current },
            set: { new in
                if let i = store.layouts.firstIndex(where: { $0.id == id }) { store.layouts[i] = new }
            })
    }
}

/// A row of the list: thumbnail of the area, name and shortcut.
private struct LayoutRow: View {
    let layout: SavedLayout
    let hasWarning: Bool

    var body: some View {
        HStack(spacing: 8) {
            AreaPreview(unit: layout.unitRect)
                .frame(width: 30, height: 19)
            Group {
                if layout.name.isEmpty { Text("Untitled") } else { Text(verbatim: layout.name) }
            }
            .lineLimit(1)
            Spacer(minLength: 4)
            if hasWarning {
                Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.orange)
            }
            Text(verbatim: layout.hotKey?.display ?? "—")
                .foregroundStyle(.secondary)
        }
        .padding(.vertical, 2)
    }
}

/// Small drawing of the screen with the layout's area painted.
private struct AreaPreview: View {
    let unit: CGRect

    var body: some View {
        GeometryReader { geo in
            let w = geo.size.width, h = geo.size.height
            ZStack(alignment: .topLeading) {
                RoundedRectangle(cornerRadius: 3)
                    .strokeBorder(Color.secondary.opacity(0.6), lineWidth: 1)
                RoundedRectangle(cornerRadius: 2)
                    .fill(Color.accentColor)
                    .frame(width: max(2, (w - 4) * unit.width), height: max(2, (h - 4) * unit.height))
                    .offset(x: 2 + (w - 4) * unit.minX, y: 2 + (h - 4) * unit.minY)
            }
        }
    }
}

/// Name, shortcut and area of a layout.
private struct LayoutEditor: View {
    @Binding var layout: SavedLayout
    let warning: String?

    /// Size of the editing grid, with the main display's proportion
    /// (a bit taller on ultrawide displays, so it is easy to drag).
    private var gridSize: CGSize {
        let screen = NSScreen.main?.visibleFrame.size ?? CGSize(width: 16, height: 10)
        let ratio = max(screen.height / max(screen.width, 1), 0.4)
        let width = min(360, 270 / ratio)
        return CGSize(width: width.rounded(), height: (width * ratio).rounded())
    }

    var body: some View {
        Grid(alignment: .leadingFirstTextBaseline, horizontalSpacing: 10, verticalSpacing: 14) {
            GridRow {
                Text("Name").gridColumnAlignment(.trailing)
                TextField("Layout name", text: $layout.name)
                    .textFieldStyle(.roundedBorder)
                    .frame(width: gridSize.width)
            }
            GridRow {
                Text("Shortcut")
                HStack(spacing: 10) {
                    HotKeyField(combo: $layout.hotKey)
                    WarningLabel(text: warning)
                }
            }
            GridRow(alignment: .top) {
                Text("Area")
                VStack(alignment: .leading, spacing: 10) {
                    GridEditor(layout: $layout)
                        .frame(width: gridSize.width, height: gridSize.height)
                    HStack(spacing: 22) {
                        Stepper("Columns: \(layout.cols)", value: Binding(
                            get: { layout.cols },
                            set: { layout.resize(cols: $0, rows: layout.rows) }), in: Settings.sizeRange)
                        Stepper("Rows: \(layout.rows)", value: Binding(
                            get: { layout.rows },
                            set: { layout.resize(cols: layout.cols, rows: $0) }), in: Settings.sizeRange)
                    }
                    .monospacedDigit()
                    Text("Drag on the grid to choose the area.")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                }
            }
        }
    }
}

/// The same grid as the panel, used to pick a layout's area.
private struct GridEditor: NSViewRepresentable {
    @Binding var layout: SavedLayout

    func makeNSView(context: Context) -> GridView { GridView() }

    func updateNSView(_ view: GridView, context: Context) {
        view.cols = layout.cols
        view.rows = layout.rows
        view.selection = layout.selection
        view.onPick = { sel in layout.selection = sel }
    }
}

/// Button that records a shortcut: click it and press the keys. esc cancels.
struct HotKeyField: View {
    @Binding var combo: HotKeyCombo?
    @State private var recording = false
    @State private var monitor: Any?

    private var label: String {
        if recording { return String(localized: "Press the shortcut…") }
        return combo?.display ?? String(localized: "Record shortcut")
    }

    var body: some View {
        HStack(spacing: 4) {
            Button(action: { recording ? stop() : start() }) {
                Text(verbatim: label)
                    .frame(minWidth: 120)
                    .foregroundStyle(recording ? Color.accentColor : Color.primary)
            }
            if combo != nil && !recording {
                Button { combo = nil } label: {
                    Image(systemName: "xmark.circle.fill").foregroundStyle(.secondary)
                }
                .buttonStyle(.borderless)
                .help("Remove shortcut")
            }
        }
        .onDisappear(perform: stop)
    }

    private func start() {
        recording = true
        HotKeyCenter.shared.suspend()
        monitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { event in
            if event.keyCode == 53 { // esc cancels
                stop()
                return nil
            }
            let new = HotKeyCombo(event: event)
            guard new.isUsable else {
                NSSound.beep() // needs ⌃, ⌥ or ⌘
                return nil
            }
            combo = new
            stop()
            return nil
        }
    }

    private func stop() {
        if let monitor { NSEvent.removeMonitor(monitor) }
        monitor = nil
        if recording {
            recording = false
            HotKeyCenter.shared.resume()
        }
    }
}

private struct WarningLabel: View {
    let text: String?

    var body: some View {
        if let text {
            Label(text, systemImage: "exclamationmark.triangle.fill")
                .font(.callout)
                .foregroundStyle(.orange)
        }
    }
}
