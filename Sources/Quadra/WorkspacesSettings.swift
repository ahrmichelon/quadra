import AppKit
import SwiftUI

/// The Workspaces tab of Settings: the list of workspaces and the editor of the selected one.
struct WorkspacesTab: View {
    @ObservedObject var store: LayoutStore
    @State private var selectedID: UUID?
    @State private var showNoWindows = false

    init(store: LayoutStore) {
        self.store = store
        _selectedID = State(initialValue: store.workspaces.last?.id)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("Each workspace remembers where the windows of several apps are and puts them all back with one shortcut.")
                .font(.callout)
                .foregroundStyle(.secondary)
            HStack(alignment: .top, spacing: 20) {
                list
                editor
            }
        }
        .padding(16)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        // A workspace saved from the menu while Settings is open: show it.
        .onChange(of: store.workspaces.count) { old, new in
            if new > old { selectedID = store.workspaces.last?.id }
        }
        .alert("No windows to save", isPresented: $showNoWindows) {
            Button("OK") {}
        } message: {
            if WindowMover.isTrusted() {
                Text("Open the windows you want in the workspace and try again.")
            } else {
                Text("Quadra needs the Accessibility permission to see the windows. Choose Allow Accessibility… in the Quadra menu.")
            }
        }
    }

    // MARK: List

    private var list: some View {
        VStack(alignment: .leading, spacing: 0) {
            List(selection: $selectedID) {
                ForEach(store.workspaces) { workspace in
                    WorkspaceRow(workspace: workspace, hasWarning: store.warning(for: workspace.hotKey) != nil)
                        .tag(workspace.id)
                }
                .onMove { store.workspaces.move(fromOffsets: $0, toOffset: $1) }
            }
            .listStyle(.bordered(alternatesRowBackgrounds: false))

            HStack(spacing: 0) {
                Button(action: saveCurrentWindows) {
                    Image(systemName: "plus").frame(width: 26, height: 22)
                }
                .help("Save Current Windows")
                Button(action: removeWorkspace) {
                    Image(systemName: "minus").frame(width: 26, height: 22)
                }
                .help("Delete the selected workspace")
                .disabled(selectedID == nil)
                Spacer()
            }
            .buttonStyle(.borderless)
            .padding(.top, 4)
        }
        .frame(width: 250)
    }

    private func saveCurrentWindows() {
        let windows = WorkspaceArranger.capture()
        guard !windows.isEmpty else { showNoWindows = true; return }
        let new = Workspace(name: store.newWorkspaceName(), windows: windows)
        store.workspaces.append(new)
        selectedID = new.id
    }

    private func removeWorkspace() {
        guard let id = selectedID, let i = store.workspaces.firstIndex(where: { $0.id == id }) else { return }
        store.workspaces.remove(at: i)
        selectedID = store.workspaces.indices.contains(i) ? store.workspaces[i].id : store.workspaces.last?.id
    }

    // MARK: Editor

    @ViewBuilder private var editor: some View {
        if let id = selectedID, let workspace = binding(for: id) {
            WorkspaceEditor(workspace: workspace,
                            warning: store.warning(for: workspace.wrappedValue.hotKey),
                            onUpdate: { update(id) },
                            onRestore: { restore(id) })
                .id(id)
        } else {
            VStack(spacing: 12) {
                Text("Arrange the windows the way you like, then click + to save them as a workspace.")
                    .multilineTextAlignment(.center)
                    .foregroundStyle(.secondary)
                Button("Save Current Windows", action: saveCurrentWindows)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
    }

    private func update(_ id: UUID) {
        let windows = WorkspaceArranger.capture()
        guard !windows.isEmpty else { showNoWindows = true; return }
        if let i = store.workspaces.firstIndex(where: { $0.id == id }) { store.workspaces[i].windows = windows }
    }

    private func restore(_ id: UUID) {
        guard let workspace = store.workspace(id: id), WindowMover.isTrusted() else { NSSound.beep(); return }
        WorkspaceArranger.restore(workspace) { moved in
            if moved == 0 { NSSound.beep() }
        }
    }

    /// Access to the workspace by id (safe even if it is deleted while on screen).
    private func binding(for id: UUID) -> Binding<Workspace>? {
        guard let current = store.workspace(id: id) else { return nil }
        return Binding(
            get: { store.workspace(id: id) ?? current },
            set: { new in
                if let i = store.workspaces.firstIndex(where: { $0.id == id }) { store.workspaces[i] = new }
            })
    }
}

/// A row of the list: name, apps and shortcut.
private struct WorkspaceRow: View {
    let workspace: Workspace
    let hasWarning: Bool

    var body: some View {
        HStack(spacing: 8) {
            Image(systemName: "rectangle.3.group")
                .foregroundStyle(Color.accentColor)
            VStack(alignment: .leading, spacing: 1) {
                Group {
                    if workspace.name.isEmpty { Text("Untitled") } else { Text(verbatim: workspace.name) }
                }
                .lineLimit(1)
                Text(verbatim: workspace.appNames.joined(separator: ", "))
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
            Spacer(minLength: 4)
            if hasWarning {
                Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.orange)
            }
            Text(verbatim: workspace.hotKey?.display ?? "—")
                .foregroundStyle(.secondary)
        }
        .padding(.vertical, 2)
    }
}

/// Name, shortcut and windows of a workspace.
private struct WorkspaceEditor: View {
    @Binding var workspace: Workspace
    let warning: String?
    let onUpdate: () -> Void
    let onRestore: () -> Void

    private let width: CGFloat = 370

    var body: some View {
        Grid(alignment: .leadingFirstTextBaseline, horizontalSpacing: 10, verticalSpacing: 14) {
            GridRow {
                Text("Name").gridColumnAlignment(.trailing)
                TextField("Workspace name", text: $workspace.name)
                    .textFieldStyle(.roundedBorder)
                    .frame(width: width)
            }
            GridRow {
                Text("Shortcut")
                HStack(spacing: 10) {
                    HotKeyField(combo: $workspace.hotKey)
                    WarningLabel(text: warning)
                }
            }
            GridRow(alignment: .top) {
                Text("Windows")
                VStack(alignment: .leading, spacing: 10) {
                    WorkspacePreview(windows: workspace.windows)
                        .frame(width: width, height: 120)
                    List {
                        ForEach(Array(workspace.windows.enumerated()), id: \.offset) { i, window in
                            HStack(spacing: 6) {
                                Text(verbatim: window.appName).fontWeight(.medium)
                                if !window.title.isEmpty {
                                    Text(verbatim: window.title)
                                        .foregroundStyle(.secondary)
                                        .truncationMode(.middle)
                                }
                                Spacer(minLength: 4)
                                Button { workspace.windows.remove(at: i) } label: {
                                    Image(systemName: "minus.circle")
                                }
                                .buttonStyle(.borderless)
                                .help("Leave this window out")
                            }
                            .lineLimit(1)
                        }
                    }
                    .listStyle(.bordered(alternatesRowBackgrounds: true))
                    .frame(width: width, height: 132)
                    HStack(spacing: 10) {
                        Button("Update with Current Windows", action: onUpdate)
                        Button("Restore Now", action: onRestore)
                    }
                    Text("Apps and documents that are closed are opened again when you restore.")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                }
            }
        }
    }
}

/// Small drawing of the displays with the workspace's windows on them.
private struct WorkspacePreview: View {
    let windows: [WindowPlacement]

    private var displays: [Int] { Array(Set(windows.map(\.displayIndex))).sorted() }

    /// Width / height of a display, taken from the connected one in that position when there is one.
    private func aspect(_ index: Int) -> CGFloat {
        let screens = Geometry.orderedScreens
        guard screens.indices.contains(index) else { return 16 / 10 }
        let size = screens[index].visibleFrame.size
        return min(max(size.width / max(size.height, 1), 0.5), 3.5)
    }

    var body: some View {
        HStack(alignment: .bottom, spacing: 12) {
            ForEach(displays, id: \.self) { index in
                let onDisplay = windows.filter { $0.displayIndex == index }
                VStack(spacing: 4) {
                    GeometryReader { geo in
                        let w = geo.size.width - 6, h = geo.size.height - 6
                        ZStack(alignment: .topLeading) {
                            RoundedRectangle(cornerRadius: 4)
                                .strokeBorder(Color.secondary.opacity(0.6), lineWidth: 1)
                            // The app's front window last, so it is drawn on top.
                            ForEach(Array(onDisplay.enumerated().reversed()), id: \.offset) { _, window in
                                let r = window.unitRect
                                RoundedRectangle(cornerRadius: 3)
                                    .fill(Color.accentColor.opacity(0.22))
                                    .overlay(RoundedRectangle(cornerRadius: 3).strokeBorder(Color.accentColor, lineWidth: 1))
                                    .overlay(alignment: .topLeading) {
                                        Text(verbatim: window.appName)
                                            .font(.system(size: 9, weight: .medium))
                                            .lineLimit(1)
                                            .padding(3)
                                    }
                                    .frame(width: max(4, w * r.width), height: max(4, h * r.height))
                                    .clipped()
                                    .offset(x: 3 + w * r.minX, y: 3 + h * r.minY)
                            }
                        }
                    }
                    .aspectRatio(aspect(index), contentMode: .fit)
                    if displays.count > 1 {
                        Text(verbatim: onDisplay.first?.display ?? "")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                            .lineLimit(1)
                    }
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}
