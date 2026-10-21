<p align="center">
  <img src="../Branding/quadra-icon-1024.png" width="128" alt="Quadra icon">
</p>

<h1 align="center">Quadra for Windows</h1>

<p align="center">
  Arrange your windows on a grid. Free and open source.<br>
  <a href="README.pt-BR.md">Leia em português</a> · <a href="../README.md">Quadra for Mac</a>
</p>

Quadra is a tray app inspired by [Divvy](https://mizage.com/divvy/). Press a shortcut or click
the icon next to the clock, drag across the grid, and the window moves to that area of the
screen. It is the Windows version of [Quadra for Mac](../README.md), with the same grid, layouts
and shortcuts.

Windows has Snap Layouts, and Microsoft PowerToys has FancyZones. Quadra works differently: there
are no zones to set up first. You draw any area on a grid, on any display, whenever you need it,
and you can see where the window will land before letting go.

## Features

- **Drag on a grid** to pick any area, with 1 to 20 columns and rows.
- **See-through panel** in the middle of the display, with a preview of where the window will land.
- **Click another window** while the grid is open to move that one instead.
- **Layouts with their own shortcuts**: save areas such as "left half" or "center" and apply them
  with one keystroke. Six ready-made layouts are included.
- **Multiple displays**: pick the display in the panel, or send a window to the next display,
  even when each display has a different scaling.
- **Ultrawide friendly**, and works with portrait and laptop displays too.
- Windows line up exactly with the screen edges (Quadra leaves out Windows' invisible borders).
- Adjustable gaps between windows, grid shape, size and opacity. Follows light and dark mode and
  your accent color.
- In English and Brazilian Portuguese, following the Windows display language.
- One file, nothing to install, no administrator rights needed.

## Install

1. Download Quadra from the [releases page](https://github.com/ahrmichelon/quadra/releases?q=windows&expanded=true)
   (look for **Quadra for Windows**):
   - **Quadra-x64.exe** for most PCs (Intel and AMD).
   - **Quadra-arm64.exe** for PCs with an ARM processor (Snapdragon, Surface Pro X).
2. Open the downloaded file. Quadra isn't signed with a paid certificate yet, so Windows may show
   **"Windows protected your PC"**. Click **More info**, then **Run anyway**.
3. Quadra installs itself and opens. It goes in your user folder (`%LocalAppData%\Programs\Quadra`,
   no administrator rights needed), appears in the Start menu and in **Settings → Apps**, and opens
   by itself every time Windows starts (you can turn this off in its settings). You can delete the
   downloaded file afterwards.
4. A window says Quadra is installed. Its icon is next to the clock; if you don't see it,
   click the **^** arrow. To keep it always visible, drag it onto the taskbar.

Requires Windows 10 (version 1809 or later) or Windows 11, 64-bit. .NET is built into
Quadra.exe, so there is nothing else to install.

## Use

| Shortcut | What it does |
|---|---|
| **Ctrl+Alt+G** or click the icon next to the clock | Open the grid |
| Drag on the grid | Move the window to that area |
| **← → ↑ ↓** / **Enter** (grid open) | Halves / full screen |
| **Esc** | Close the grid |
| **Ctrl+Alt+← → ↑ ↓**, **Ctrl+Alt+Enter**, **Ctrl+Alt+C** | Default layouts: halves, full screen, center |
| **Ctrl+Alt+Shift+→** / **Ctrl+Alt+Shift+←** | Move to the next / previous display |

Right-click the icon, or click the gear on the panel, for the menu. Every shortcut can be
changed in **Settings and Layouts**; when another app already uses one, Quadra shows a warning
next to it.

### Updates

Once a day Quadra asks GitHub whether a new version is out, and tells you with a notification and
an item at the top of its menu (you can turn this off in Settings, where **Check Now** checks right
away). Click **Yes** to update: Quadra downloads the new version, checks it against the file
published on GitHub, closes and opens again by itself, keeping your settings and layouts. Opening a
newer Quadra.exe that you downloaded yourself updates it the same way. The check and the download
are the only times Quadra uses the internet, and they send nothing about you or your PC.

Good to know:

- Opening Quadra.exe again while it is running opens its settings.
- Windows of apps that run as administrator can only be moved when Quadra runs as administrator too.
- Settings are saved in `%AppData%\Quadra\settings.json`. The `quadra.log` file in the same
  folder helps when reporting a problem.
- To remove Quadra, open **Settings → Apps → Installed apps**, find **Quadra** and choose
  **Uninstall**. It removes Quadra, its shortcut and its settings.

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). The app runs only on Windows,
but it builds and its tests run on macOS and Linux too.

```bash
cd Windows
dotnet test tests/Quadra.Core.Tests/Quadra.Core.Tests.csproj
dotnet publish src/Quadra/Quadra.csproj -c Release -r win-x64 -o publish/x64
```

The second command makes `publish/x64/Quadra.exe` with .NET inside. Use `-r win-arm64` for ARM.
Opened normally, Quadra.exe installs itself; add `--portable` to run it from where it is while you
work on the code.
On Windows, `Quadra.exe --self-test results` checks the parts that talk to Windows (moving
windows, shortcuts, the panel, the settings window) and writes a report and screenshots to the
`results` folder. GitHub runs the tests and this self-test on every change.

| Folder | Contents |
|---|---|
| `src/Quadra.Core` | Geometry, layouts, shortcuts, settings and texts (plain .NET, tested anywhere) |
| `src/Quadra` | The Windows app (WPF) |
| `tests/Quadra.Core.Tests` | Tests for the geometry, layouts, settings and translations |
| `Tools` | Script that draws the icon (`Quadra.ico`) |

Every text shown to people is in `src/Quadra.Core/Strings.resx`, with the Portuguese translation
in `Strings.pt-BR.resx`; a test checks that both have the same texts.

### Releases

Update `CHANGELOG.md`, then push a tag such as `windows-v1.0.0`. GitHub runs the tests, builds
Quadra-x64.exe and Quadra-arm64.exe, runs the self-test and publishes the release. Windows
releases are never marked as the repository's "latest" release, because the Mac app's update
check reads that one.

## Support Quadra

Quadra is free. If it helps you, see [how to support it](../README.md#support-quadra).

## License

[MIT](../LICENSE)
