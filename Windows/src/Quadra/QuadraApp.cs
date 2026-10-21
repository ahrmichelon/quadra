using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace Quadra;

/// <summary>Connects everything: tray icon, shortcuts, grid panel, overlays and settings.</summary>
internal sealed class QuadraApp : IDisposable
{
    /// <summary>Ready-made grid sizes in the menu (columns × rows).</summary>
    private static readonly (int Cols, int Rows)[] Presets =
        [(2, 2), (3, 2), (3, 3), (4, 3), (4, 4), (6, 4), (6, 6), (8, 6), (12, 8)];

    /// <summary>True while Quadra quits (windows may close for real then).</summary>
    public static bool IsQuitting { get; private set; }

    private readonly Application app;
    private SettingsStore store = null!;
    private HotKeyCenter? hotKeys;
    private ForegroundWatcher? foreground;
    private TrayIcon? tray;
    private GridPanel panel = null!;
    /// <summary>Outline around the chosen window.</summary>
    private OverlayWindow targetHighlight = null!;
    /// <summary>Area painted on screen showing where the window will go.</summary>
    private OverlayWindow dropPreview = null!;
    private SettingsWindow? settingsWindow;

    /// <summary>The window the grid moves.</summary>
    private IntPtr target;
    /// <summary>The display the grid stands for (the window's display, or the one picked in the panel).</summary>
    private Display? targetDisplay;
    /// <summary>The last app window the user worked in. Clicking the tray icon takes the focus away from it.</summary>
    private IntPtr lastWindow;
    private DateTime panelShown = DateTime.MinValue;
    private string registeredHotKeys = "";
    private DispatcherTimer? focusTimer, checkTimer, updateTimer;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly HttpClient Downloads = new() { Timeout = TimeSpan.FromMinutes(10) };
    private bool downloading;
    /// <summary>The newer release found by the last check, if any.</summary>
    private Release? availableUpdate;
    /// <summary>The release the last notification was about (null for other notifications).</summary>
    private Release? notifiedUpdate;

    public QuadraApp(Application app) => this.app = app;

    private static int DoubleClickTime => WinForms.SystemInformation.DoubleClickTime;

    public void Start()
    {
        SettingsStore.Log = Log.Write;
        Keys.LayoutKeyName = HotKeyCenter.KeyName;
        store = new SettingsStore(Path.Combine(Log.Folder, "settings.json"));
        Log.Write($"Quadra {Links.Version} started. Windows {Environment.OSVersion.Version}, " +
                  $"{RuntimeInformation.ProcessArchitecture}, language {CultureInfo.CurrentUICulture.Name}");
        LogDisplays();

        panel = new GridPanel();
        targetHighlight = new OverlayWindow(OverlayWindow.Kind.Outline);
        dropPreview = new OverlayWindow(OverlayWindow.Kind.Fill);
        panel.Picked += ApplyFromPanel;
        panel.Canceled += () => WindowMover.Focus(target);
        panel.PreviewChanged += ShowDropPreview;
        panel.DisplayPicked += PickDisplay;
        panel.GearClicked += point => tray?.ShowMenuAt(point);
        panel.GridSizeChanged += (cols, rows) => store.Edit(s => { s.Columns = cols; s.Rows = rows; });
        panel.Hidden += () =>
        {
            targetHighlight.Hide();
            dropPreview.Hide();
        };

        tray = new TrayIcon();
        tray.Clicked += TrayClicked;
        tray.MenuOpening += () => FillMenu(tray.Menu);
        // The gear menu may have taken the focus from the panel; give it back so Esc works.
        tray.MenuClosed += () =>
        {
            if (panel.IsVisible) WindowMover.BringToFront(panel.Handle);
        };

        hotKeys = new HotKeyCenter();
        hotKeys.Updated += failed => store.SetUnavailable(failed);
        hotKeys.ShowSettingsRequested += OpenSettings;
        hotKeys.QuitRequested += Quit;
        HotKeyField.RecordingStarted = hotKeys.Suspend;
        HotKeyField.RecordingStopped = hotKeys.Resume;
        store.Changed += RegisterHotKeys;
        RegisterHotKeys();

        foreground = new ForegroundWatcher();
        foreground.Changed += ForegroundChanged;
        var current = WindowMover.Root(Native.GetForegroundWindow());
        if (WindowMover.IsManageable(current)) lastWindow = current;

        // A display was connected, disconnected or rearranged: the open grid no longer matches.
        SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;

        tray.BalloonClicked += () =>
        {
            if (notifiedUpdate is { } release) ShowUpdateDialog(release);
        };
        StartUpdateChecks();

        StartWithWindows.UpdatePathIfMoved();
        DeleteOldUpdateDownloads();
        Log.Write(Installer.IsRunningInstalled ? $"Running from {AppPaths.InstallFolder}" : $"Running from {Environment.ProcessPath}");

        var settings = store.Current;
        if (!settings.Welcomed || settings.LastVersion == null)
        {
            // New, or coming from 1.0/1.1, which didn't install themselves.
            var combo = settings.ShowGridHotKey ?? HotKeyCombo.ShowGridDefault;
            string text = Installer.IsRunningInstalled ? "Welcome_Text" : "Welcome_TextPortable";
            tray.ShowBalloon(Loc.Get("Welcome_Title"), Loc.Format(text, combo.Display));
        }
        else if (settings.LastVersion != Links.Version)
        {
            tray.ShowBalloon(Loc.Format("Updated_Title", Links.Version), Loc.Get("Updated_Text"));
        }
        store.Edit(s =>
        {
            s.Welcomed = true;
            s.LastVersion = Links.Version;
        });
    }

    /// <summary>Registers the app's shortcuts and each layout's shortcut (only when they changed).</summary>
    private void RegisterHotKeys()
    {
        if (hotKeys == null) return;
        var s = store.Current;
        var list = new List<(HotKeyCombo, Action)>();
        var signature = new StringBuilder();
        void Add(string name, HotKeyCombo? combo, Action action)
        {
            if (combo is not { } c) return;
            list.Add((c, action));
            signature.Append($"{name}={c.Key}/{c.Modifiers};");
        }
        Add("grid", s.ShowGridHotKey, () => ToggleGrid(fromTray: false));
        Add("next", s.NextScreenHotKey, () => MoveToAdjacentDisplay(1, fromTray: false));
        Add("previous", s.PreviousScreenHotKey, () => MoveToAdjacentDisplay(-1, fromTray: false));
        foreach (var layout in s.Layouts)
        {
            var id = layout.Id;
            Add(id.ToString(), layout.HotKey, () => ApplyLayout(id, fromTray: false));
        }
        if (signature.ToString() == registeredHotKeys) return;
        registeredHotKeys = signature.ToString();
        hotKeys.Set(list);
    }

    // Tray icon

    private void TrayClicked()
    {
        // A double click (the usual way to open tray apps) must not open and close the grid at once.
        if (panel.IsVisible && (DateTime.Now - panelShown).TotalMilliseconds < DoubleClickTime) return;
        // If this same click just closed the panel, don't reopen it.
        if (!panel.IsVisible && (DateTime.Now - panel.LastHidden).TotalMilliseconds < 300) return;
        ToggleGrid(fromTray: true);
    }

    // Grid

    /// <summary>The focused app window, or nothing (the desktop, the taskbar, Quadra itself).</summary>
    private static IntPtr FocusedWindow()
    {
        var hwnd = WindowMover.Root(Native.GetForegroundWindow());
        return WindowMover.IsManageable(hwnd) ? hwnd : IntPtr.Zero;
    }

    /// <summary>The window the user last worked in (used from the tray icon, which takes the focus).</summary>
    private IntPtr RememberedWindow() => WindowMover.IsManageable(lastWindow) ? lastWindow : FocusedWindow();

    private void ToggleGrid(bool fromTray)
    {
        if (panel.IsVisible)
        {
            panel.Cancel();
            return;
        }
        var hwnd = fromTray ? RememberedWindow() : FocusedWindow();
        if (hwnd != IntPtr.Zero && PresentGrid(hwnd, null)) return;
        // No window to move: from the icon, show the menu; from the shortcut, just beep.
        if (fromTray) tray?.ShowMenuAtIcon();
        else WindowMover.Beep();
    }

    /// <summary>
    /// Opens (or updates) the grid to move <paramref name="hwnd"/>. With <paramref name="gridDisplay"/>, the grid
    /// stands for that display instead of the window's own, and the panel stays where it is.
    /// False if the window can't be read.
    /// </summary>
    private bool PresentGrid(IntPtr hwnd, Display? gridDisplay)
    {
        if (WindowMover.VisibleFrame(hwnd) is not { } frame) return false;
        var displays = Displays.All();
        if (displays.Count == 0) return false;
        var windowDisplay = Displays.ForRect(frame, displays);
        var grid = gridDisplay ?? windowDisplay;
        target = hwnd;
        targetDisplay = grid;

        // The dashed outline only makes sense when the grid stands for the window's own display.
        RectD? current = grid.SameAs(windowDisplay) ? WindowGeometry.UnitRect(frame, grid.WorkArea) : null;
        var names = displays.Count > 1
            ? displays.Select((d, i) => $"{i + 1} · {d.Name}").ToList()
            : new List<string>();
        int selected = Math.Max(0, displays.FindIndex(d => d.SameAs(grid)));

        Point center;
        Display panelDisplay;
        if (gridDisplay != null && panel.IsVisible)
        {
            center = panel.ScreenCenter;
            panelDisplay = Displays.ForPoint(center.X, center.Y, displays);
        }
        else
        {
            center = new Point(windowDisplay.WorkArea.CenterX, windowDisplay.WorkArea.CenterY);
            panelDisplay = windowDisplay;
        }

        dropPreview.Hide();
        if (!Native.IsZoomed(hwnd))
            targetHighlight.ShowAt(frame.Inflate(WindowGeometry.Scale(4, windowDisplay.Dpi)), Native.HWND_TOPMOST);
        else
            targetHighlight.Hide(); // a maximized window fills the screen; its outline would only hide the edges

        var s = store.Current;
        panel.Present(WindowMover.Title(hwnd), s.Columns, s.Rows, s.PanelGridSize(grid.WorkArea.Width, grid.WorkArea.Height),
                      current, names, selected, s.PanelOpacity, center, panelDisplay);
        targetHighlight.PlaceBelow(panel.Handle);
        panelShown = DateTime.Now;
        return true;
    }

    /// <summary>The user picked another display in the panel.</summary>
    private void PickDisplay(int index)
    {
        var displays = Displays.All();
        if (index < 0 || index >= displays.Count || !Native.IsWindow(target)) return;
        PresentGrid(target, displays[index]);
    }

    private void ForegroundChanged(IntPtr hwnd)
    {
        var root = WindowMover.Root(hwnd);
        if (WindowMover.IsManageable(root)) lastWindow = root;
        if (!panel.IsVisible) return;
        // Wait for the click to finish before checking which window got the focus.
        focusTimer?.Stop();
        focusTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Normal, (_, _) =>
        {
            focusTimer?.Stop();
            PanelLostFocus();
        }, app.Dispatcher);
    }

    /// <summary>
    /// The panel lost the focus. If the user clicked another window, that window becomes the one to move;
    /// if they clicked outside any window (the desktop, the taskbar), the grid closes.
    /// </summary>
    private void PanelLostFocus()
    {
        if (!panel.IsVisible || tray?.MenuIsOpen == true) return;
        var hwnd = WindowMover.Root(Native.GetForegroundWindow());
        if (hwnd == IntPtr.Zero || WindowMover.IsOwn(hwnd)) return;
        if (WindowMover.IsManageable(hwnd))
        {
            if (PresentGrid(hwnd, null)) return;
        }
        else if ((DateTime.Now - panelShown).TotalMilliseconds < DoubleClickTime)
        {
            // Just opened from the tray icon (for example with a double click): keep it open.
            WindowMover.BringToFront(panel.Handle);
            return;
        }
        panel.HidePanel();
    }

    /// <summary>Paints the area where the window will go (null clears it).</summary>
    private void ShowDropPreview(RectD? unit)
    {
        if (unit is not { } u || targetDisplay is not { } display)
        {
            dropPreview.Hide();
            return;
        }
        double gap = WindowGeometry.Scale(store.Current.Gap, display.Dpi);
        dropPreview.ShowAt(WindowGeometry.Frame(u, display.WorkArea, gap), panel.Handle);
    }

    private void ApplyFromPanel(RectD unit)
    {
        panel.HidePanel();
        if (targetDisplay is { } display && Native.IsWindow(target)) MoveTo(target, unit, display);
    }

    // Layouts and displays

    /// <summary>The window to act on: the grid's window when the panel is open, otherwise the focused one.</summary>
    private IntPtr WindowForAction(bool fromTray)
    {
        if (panel.IsVisible)
        {
            panel.HidePanel();
            return target;
        }
        return fromTray ? RememberedWindow() : FocusedWindow();
    }

    /// <summary>Moves the window to the layout's area on the display it is on.</summary>
    private void ApplyLayout(Guid id, bool fromTray)
    {
        if (store.Layout(id) is not { } layout) return;
        var hwnd = WindowForAction(fromTray);
        if (hwnd == IntPtr.Zero || WindowMover.VisibleFrame(hwnd) is not { } frame)
        {
            WindowMover.Beep();
            return;
        }
        MoveTo(hwnd, layout.UnitRect, Displays.ForRect(frame, Displays.All()));
    }

    /// <summary>
    /// Moves the window to the next (<paramref name="step"/> = 1) or previous (-1) display,
    /// keeping its relative position and size.
    /// </summary>
    private void MoveToAdjacentDisplay(int step, bool fromTray)
    {
        var displays = Displays.All();
        var hwnd = WindowForAction(fromTray);
        if (displays.Count < 2 || hwnd == IntPtr.Zero || WindowMover.VisibleFrame(hwnd) is not { } frame)
        {
            WindowMover.Beep();
            return;
        }
        var current = Displays.ForRect(frame, displays);
        int i = displays.FindIndex(d => d.SameAs(current));
        var next = displays[(i + step + displays.Count) % displays.Count];
        checkTimer?.Stop();
        if (!WindowMover.MoveToDisplay(hwnd, current, next))
        {
            WindowMover.Beep();
            return;
        }
        WindowMover.Focus(hwnd);
    }

    private void MoveTo(IntPtr hwnd, RectD unit, Display display)
    {
        double gap = WindowGeometry.Scale(store.Current.Gap, display.Dpi);
        var frame = WindowGeometry.Frame(unit, display.WorkArea, gap);
        checkTimer?.Stop();
        if (WindowMover.Move(hwnd, frame, display.WorkArea) is not { } placed)
        {
            WindowMover.Beep();
            return;
        }
        WindowMover.Focus(hwnd);
        CheckLater(hwnd, frame, display.WorkArea, placed);
    }

    /// <summary>
    /// Some apps adjust themselves a moment after being moved to a display with another scaling.
    /// Looks again shortly after, and puts the window back in place once if it moved.
    /// </summary>
    private void CheckLater(IntPtr hwnd, RectD frame, RectD area, RectD placed)
    {
        checkTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Normal, (sender, _) =>
        {
            ((DispatcherTimer)sender!).Stop();
            if (WindowMover.VisibleFrame(hwnd) is not { } now) return;
            bool moved = Math.Abs(now.X - placed.X) > 2 || Math.Abs(now.Y - placed.Y) > 2 ||
                         Math.Abs(now.Width - placed.Width) > 2 || Math.Abs(now.Height - placed.Height) > 2;
            if (!moved) return;
            Log.Write($"The window changed after being placed ({now} instead of {placed}); placing it again");
            WindowMover.Move(hwnd, frame, area);
        }, app.Dispatcher);
    }

    private void OnDisplaysChanged(object? sender, EventArgs e) =>
        app.Dispatcher.BeginInvoke(() =>
        {
            panel.HidePanel();
            LogDisplays();
        });

    private static void LogDisplays()
    {
        foreach (var d in Displays.All())
            Log.Write($"Display \"{d.Name}\" {d.DeviceName}: {d.Bounds}, work area {d.WorkArea}, " +
                      $"{d.Dpi} dpi{(d.IsPrimary ? ", primary" : "")}");
    }

    // Menu (tray icon and the gear on the panel)

    private void FillMenu(WinForms.ContextMenuStrip menu)
    {
        menu.Items.Clear();
        var s = store.Current;

        if (availableUpdate is { } update)
        {
            var item = Item(Loc.Format("Menu_UpdateAvailable", update.Version), () => ShowUpdateDialog(update));
            item.Font = new System.Drawing.Font(item.Font, System.Drawing.FontStyle.Bold);
            menu.Items.Add(item);
            menu.Items.Add(new WinForms.ToolStripSeparator());
        }

        if (!panel.IsVisible)
        {
            menu.Items.Add(Item(Loc.Get("Menu_ShowGrid"), () => ToggleGrid(fromTray: true), s.ShowGridHotKey));
            if (store.Warning(s.ShowGridHotKey) is { } warning)
                menu.Items.Add(Info(Loc.Format("Menu_GridShortcutWarning", warning.ToLower(CultureInfo.CurrentCulture))));
            menu.Items.Add(new WinForms.ToolStripSeparator());
        }

        if (s.Layouts.Count > 0)
        {
            menu.Items.Add(Info(Loc.Get("Menu_Layouts")));
            foreach (var layout in s.Layouts)
            {
                var id = layout.Id;
                string name = layout.Name.Length > 0 ? layout.Name : Loc.Get("Menu_Untitled");
                menu.Items.Add(Item(name, () => ApplyLayout(id, fromTray: true), layout.HotKey));
            }
            menu.Items.Add(new WinForms.ToolStripSeparator());
        }

        if (Displays.All().Count > 1)
        {
            menu.Items.Add(Info(Loc.Get("Menu_Displays")));
            menu.Items.Add(Item(Loc.Get("Menu_NextDisplay"), () => MoveToAdjacentDisplay(1, fromTray: true), s.NextScreenHotKey));
            menu.Items.Add(Item(Loc.Get("Menu_PreviousDisplay"), () => MoveToAdjacentDisplay(-1, fromTray: true),
                                s.PreviousScreenHotKey));
            menu.Items.Add(new WinForms.ToolStripSeparator());
        }

        var sizes = new WinForms.ToolStripMenuItem(Loc.Get("Menu_GridSize"));
        sizes.DropDownItems.Add(Info(Loc.Get("Menu_ColumnsRows")));
        foreach (var (cols, rows) in Presets)
        {
            var item = Item($"{cols} × {rows}", () => SetGridSize(cols, rows));
            item.Checked = s.Columns == cols && s.Rows == rows;
            sizes.DropDownItems.Add(item);
        }
        if (!Presets.Contains((s.Columns, s.Rows)))
        {
            var custom = Info(Loc.Format("Menu_CustomSize", s.Columns, s.Rows));
            custom.Checked = true;
            sizes.DropDownItems.Add(custom);
        }
        sizes.DropDownItems.Add(new WinForms.ToolStripSeparator());
        sizes.DropDownItems.Add(Info(Loc.Get("Menu_FineTune")));
        menu.Items.Add(sizes);

        var gaps = new WinForms.ToolStripMenuItem(Loc.Get("Menu_Spacing"));
        foreach (int gap in AppSettings.GapChoices)
        {
            string title = gap == 0 ? Loc.Get("Menu_SpacingNone") : Loc.Format("Menu_SpacingPixels", gap);
            var item = Item(title, () => store.Edit(x => x.Gap = gap));
            item.Checked = s.Gap == gap;
            gaps.DropDownItems.Add(item);
        }
        menu.Items.Add(gaps);
        menu.Items.Add(new WinForms.ToolStripSeparator());

        bool startsWithWindows = StartWithWindows.IsEnabled;
        var login = Item(Loc.Get("Menu_StartWithWindows"), () => SetStartWithWindows(!startsWithWindows));
        login.Checked = startsWithWindows;
        menu.Items.Add(login);
        menu.Items.Add(Item(Loc.Get("Menu_Settings"), OpenSettings));
        menu.Items.Add(new WinForms.ToolStripSeparator());

        menu.Items.Add(Item(Loc.Get("Menu_CheckForUpdates"), CheckForUpdatesNow));
        menu.Items.Add(Item(Loc.Get("Menu_Support"), () =>
        {
            panel.HidePanel();
            Links.Open(Links.Support);
        }));
        menu.Items.Add(Item(Loc.Get("Menu_About"), ShowAbout));
        menu.Items.Add(Item(Loc.Get("Menu_Quit"), Quit));
    }

    private WinForms.ToolStripMenuItem Item(string title, Action action, HotKeyCombo? shortcut = null)
    {
        var item = new WinForms.ToolStripMenuItem(title.Replace("&", "&&"))
        {
            ShortcutKeyDisplayString = shortcut?.Display,
        };
        // Menu actions run after the menu closes, so the focus is back where it was.
        item.Click += (_, _) => AfterMenuCloses(action);
        return item;
    }

    private static WinForms.ToolStripMenuItem Info(string title) => new(title.Replace("&", "&&")) { Enabled = false };

    private void AfterMenuCloses(Action action)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
    }

    private void SetGridSize(int cols, int rows)
    {
        store.Edit(s => { s.Columns = cols; s.Rows = rows; });
        if (panel.IsVisible) panel.SetGridSize(cols, rows);
    }

    public static void SetStartWithWindows(bool on)
    {
        try
        {
            StartWithWindows.Set(on);
        }
        catch (Exception e)
        {
            Log.Write($"Couldn't change Start with Windows: {e}");
            MessageBox.Show(Loc.Format("Error_StartWithWindows", e.Message), "Quadra", MessageBoxButton.OK,
                            MessageBoxImage.Warning);
        }
    }

    private void OpenSettings()
    {
        panel.HidePanel();
        if (settingsWindow == null)
        {
            settingsWindow = new SettingsWindow(store, CheckForUpdatesNow);
            settingsWindow.Closed += (_, _) => settingsWindow = null;
        }
        settingsWindow.Show();
        if (settingsWindow.WindowState == WindowState.Minimized) settingsWindow.WindowState = WindowState.Normal;
        WindowMover.BringToFront(new WindowInteropHelper(settingsWindow).Handle);
        settingsWindow.Activate();
    }

    // Updates

    private void StartUpdateChecks()
    {
        // First look shortly after launch, then every hour see whether a day has passed.
        new DispatcherTimer(TimeSpan.FromSeconds(15), DispatcherPriority.Background, (sender, _) =>
        {
            ((DispatcherTimer)sender!).Stop();
            CheckIfDue();
        }, app.Dispatcher);
        updateTimer = new DispatcherTimer(TimeSpan.FromHours(1), DispatcherPriority.Background, (_, _) => CheckIfDue(),
                                          app.Dispatcher);
    }

    /// <summary>The automatic check: at most once a day, and each new version is announced only once.</summary>
    private async void CheckIfDue()
    {
        if (!store.Current.CheckForUpdates || !Updates.IsDue(store.Current.LastUpdateCheck, DateTimeOffset.Now)) return;
        try
        {
            if (await AskGitHub() is { } release && store.Current.AnnouncedVersion != release.Version)
            {
                store.Edit(s => s.AnnouncedVersion = release.Version);
                notifiedUpdate = release;
                tray?.ShowBalloon(Loc.Format("Update_Available", release.Version), Loc.Get("Update_BalloonText"));
            }
        }
        catch (Exception e)
        {
            Log.Write($"The update check failed: {e.Message}");
        }
    }

    /// <summary>"Check for Updates…" in the menu and "Check Now" in Settings.</summary>
    private async void CheckForUpdatesNow()
    {
        try
        {
            if (await AskGitHub() is { } release)
            {
                store.Edit(s => s.AnnouncedVersion = release.Version);
                ShowUpdateDialog(release);
            }
            else
            {
                Message(Loc.Get("Update_UpToDate"), Loc.Format("Update_UpToDateText", Links.Version), MessageBoxImage.Information);
            }
        }
        catch (Exception e)
        {
            Log.Write($"The update check failed: {e.Message}");
            Message(Loc.Get("Update_Error"), e.Message, MessageBoxImage.Warning);
        }
    }

    private async Task<Release?> AskGitHub()
    {
        var release = await Updates.CheckAsync(Http, Links.Version);
        store.Edit(s => s.LastUpdateCheck = DateTimeOffset.Now);
        availableUpdate = release;
        Log.Write(release != null ? $"Quadra {release.Version} is available" : "Quadra is up to date");
        return release;
    }

    /// <summary>Tells the user about a new version and offers to download it.</summary>
    private void ShowUpdateDialog(Release release)
    {
        panel.HidePanel();
        var answer = MessageBox.Show(
            Loc.Format("Update_Available", release.Version) + "\n\n" + Loc.Format("Update_Instructions", Links.Version),
            "Quadra", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (answer == MessageBoxResult.Yes) DownloadAndInstall(release);
    }

    /// <summary>
    /// Downloads the new Quadra.exe (checked against GitHub's checksum) and opens it. The new version installs
    /// itself: it closes this copy, takes its place and opens. If anything goes wrong, the download page opens.
    /// </summary>
    private async void DownloadAndInstall(Release release)
    {
        if (downloading) return;
        string architecture = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        if (release.ExeFor(architecture) is not { } exe)
        {
            Links.Open(release.PageUrl);
            return;
        }
        downloading = true;
        notifiedUpdate = null;
        tray?.ShowBalloon(Loc.Format("Update_Downloading", release.Version), Loc.Get("Update_DownloadingText"));
        string path = Path.Combine(Path.GetTempPath(), $"Quadra-update-{release.Version}.exe");
        try
        {
            await Updates.DownloadAsync(Downloads, exe, path);
            Log.Write($"Downloaded Quadra {release.Version} to {path}; opening it to install");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = false });
        }
        catch (Exception e)
        {
            Log.Write($"Couldn't download Quadra {release.Version}: {e}");
            Message(Loc.Get("Update_DownloadFailed"), e.Message + "\n\n" + Loc.Get("Update_OpenPage"), MessageBoxImage.Warning);
            Links.Open(release.PageUrl);
        }
        finally
        {
            downloading = false;
        }
    }

    /// <summary>Updates downloaded earlier stay in Temp after installing themselves; remove them.</summary>
    private static void DeleteOldUpdateDownloads()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(Path.GetTempPath(), "Quadra-update-*.exe"))
            {
                if (string.Equals(file, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(file); } catch (Exception) { } // still open; next time
            }
        }
        catch (Exception e)
        {
            Log.Write($"Couldn't clean up old update downloads: {e.Message}");
        }
    }

    private static void Message(string title, string text, MessageBoxImage image) =>
        MessageBox.Show(title + "\n\n" + text, "Quadra", MessageBoxButton.OK, image);

    private void ShowAbout()
    {
        panel.HidePanel();
        MessageBox.Show(Loc.Format("About_Text", Links.Version, Links.Repository), "Quadra", MessageBoxButton.OK,
                        MessageBoxImage.Information);
    }

    private void Quit()
    {
        IsQuitting = true;
        Dispose();
        app.Shutdown();
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;
        updateTimer?.Stop();
        tray?.Dispose();
        tray = null;
        hotKeys?.Dispose();
        hotKeys = null;
        foreground?.Dispose();
        foreground = null;
    }
}
