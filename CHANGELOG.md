# Changelog

## 1.3.0

- **Workspaces**: save where the windows of several apps are and put them all back with one
  shortcut. Choose "Save Current Windows as Workspace…" in the menu. When you restore a
  workspace, closed apps and documents are opened again, hidden apps and minimized windows come
  back, and each window goes back to its place on its display.
- Settings now has three tabs: General, Layouts and Workspaces. In Workspaces you can give a
  workspace a shortcut, update it with the current windows, or leave a window out.

## 1.2.0

- A welcome window explains why Quadra needs the Accessibility permission, opens System Settings
  and notices by itself when the permission is granted.
- Quadra is now published by [Vulpini](https://vulpini.com.br). The project moved to
  [github.com/vulpini-br/quadra](https://github.com/vulpini-br/quadra), and Homebrew is now
  `brew install --cask vulpini-br/tap/quadra` (old links keep working).
- Pix donations now go to Vulpini Contabilidade.
- `./build.sh --appstore` builds the Mac App Store version: sandboxed, without the update check
  and the donation link. Store texts, screenshots and a privacy policy are in `docs/app-store`
  and `PRIVACY.md`.

## 1.1.0

- Quadra tells you when a new version is out. It checks GitHub once a day (you can turn this
  off in Settings), and "Check for Updates…" in the menu checks right away.
- Install and update with Homebrew: `brew install --cask vulpini-br/tap/quadra`.
- Releases are signed with the project's own certificate, so macOS keeps the Accessibility
  permission across updates. Coming from 1.0.x, allow Quadra in Accessibility one last time.

## 1.0.1

- "Support Quadra…" in the menu and a support link in Settings.
- Support section with Pix in the README, and a Sponsor button on GitHub.

## 1.0.0

First public version.

- Grid panel: drag across a grid of 1 to 20 columns and rows to move the focused window.
- See-through panel in the middle of the screen, with adjustable shape, width and opacity.
- Outline around the chosen window and a preview of the destination area while dragging.
- Click another window while the grid is open to move that one instead.
- Layouts with their own shortcuts, with six ready-made ones (halves, full screen, center).
- Multiple displays: pick the display in the panel, or move a window to the next or previous
  display with a shortcut.
- Apps with a minimum or fixed size are centered on the chosen area.
- Adjustable gap between windows.
- Open at login.
- English and Brazilian Portuguese.
