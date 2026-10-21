# Changelog: Quadra for Windows

## 1.2.1

- Installing now says so: after opening the downloaded Quadra.exe, a window tells you Quadra is
  installed, where its icon is, that it is in the Start menu and opens by itself with Windows, and
  that the downloaded file can be deleted. Opening a newer Quadra.exe says it was updated. Before,
  the only sign was a notification, which Windows can hide (Do not disturb), so it looked like
  nothing had been installed.

## 1.2.0

- Quadra installs itself: opening the downloaded Quadra.exe puts it in your user folder
  (`%LocalAppData%\Programs\Quadra`, no administrator rights), adds it to the Start menu and to
  Settings → Apps, and makes it open by itself when Windows starts. Before, it only ran from where
  it was downloaded and didn't come back after restarting Windows unless you turned on
  Start with Windows.
- Updating takes one click: Quadra downloads the new version, checks it against the file published
  on GitHub, then closes and opens again by itself. Opening a newer Quadra.exe also updates it.
- Uninstall from Settings → Apps removes Quadra, its shortcut and its settings.
- After an update, a notification says which version is now running.

## 1.1.0

- Quadra tells you when a new version is out, like Quadra for Mac 1.1. It asks GitHub once a day
  (you can turn this off in Settings), shows a notification and an item at the top of the menu,
  and opens the download of the right Quadra.exe for your PC.
- "Check for Updates…" in the menu and "Check Now" in Settings check right away.

## 1.0.0

First version for Windows, with the same features as Quadra for Mac 1.0.

- Grid panel: drag across a grid of 1 to 20 columns and rows to move the focused window.
- See-through panel in the middle of the display, with adjustable shape, width and opacity.
  Follows light and dark mode and the Windows accent color.
- Outline around the chosen window and a preview of the destination area while dragging.
- Click another window while the grid is open to move that one instead.
- Layouts with their own shortcuts, with six ready-made ones (halves, full screen, center).
- Multiple displays: pick the display in the panel, or move a window to the next or previous
  display with a shortcut. Works when each display has a different scaling.
- Windows line up exactly with the screen edges, without Windows' invisible borders.
- Maximized windows are restored before moving; windows with a minimum or fixed size are
  centered on the chosen area.
- Adjustable gap between windows.
- Start with Windows.
- English and Brazilian Portuguese.
- One Quadra.exe for x64 and one for ARM64, with .NET inside.
