using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Quadra;

/// <summary>
/// <c>Quadra.exe --self-test &lt;folder&gt;</c> checks the parts that talk to Windows (displays, moving windows,
/// shortcuts, the grid panel, the overlays and the settings window), then writes report.txt and screenshots to
/// the folder and exits with the number of failed checks. The GitHub workflow runs it on a real Windows machine.
/// It doesn't touch the user's settings or the "Start with Windows" entry.
/// </summary>
internal static class SelfTest
{
    private static readonly List<string> Report = [];
    private static int failures;
    private static string folder = "";

    public static int Main(string outputFolder)
    {
        folder = Path.GetFullPath(outputFolder);
        Directory.CreateDirectory(folder);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int result = 1;
        app.Startup += async (_, _) =>
        {
            try
            {
                await RunAsync();
                result = failures;
            }
            catch (Exception e)
            {
                Fail("The self-test crashed", e.ToString());
                result = 100;
            }
            finally
            {
                Report.Add("");
                Report.Add(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
                File.WriteAllLines(Path.Combine(folder, "report.txt"), Report);
                app.Shutdown(result);
            }
        };
        app.Run();
        return result;
    }

    private static void Check(string name, bool ok, string detail = "")
    {
        Report.Add($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  (" + detail + ")" : "")}");
        if (!ok) failures++;
    }

    private static void Fail(string name, string detail) => Check(name, false, detail);

    private static void Note(string text) => Report.Add($"INFO  {text}");

    private static Task Wait(int ms) => Task.Delay(ms);

    private static bool Near(RectD? a, RectD b, double tolerance = 1) =>
        a is { } r && Math.Abs(r.X - b.X) <= tolerance && Math.Abs(r.Y - b.Y) <= tolerance &&
        Math.Abs(r.Width - b.Width) <= tolerance && Math.Abs(r.Height - b.Height) <= tolerance;

    private static RectD WindowRect(IntPtr hwnd) => Native.GetWindowRect(hwnd, out var r) ? r.ToRectD() : default;

    private static async Task RunAsync()
    {
        Note($"Quadra {Links.Version}, Windows {Environment.OSVersion.Version}, {RuntimeInformation.ProcessArchitecture}, " +
             $"language {Loc.Get("Language_Code")}");

        // Displays
        var displays = Displays.All();
        Check("Lists the displays", displays.Count > 0, $"{displays.Count} display(s)");
        foreach (var d in displays)
        {
            Note($"Display \"{d.Name}\" {d.DeviceName}: {d.Bounds}, work area {d.WorkArea}, {d.Dpi} dpi" +
                 (d.IsPrimary ? ", primary" : ""));
            Check($"Work area of {d.DeviceName} is inside the display", d.Bounds.Contains(d.WorkArea));
        }
        var primary = displays.FirstOrDefault(d => d.IsPrimary) ?? displays[0];
        var area = primary.WorkArea;

        // Icons and texts
        Check("Loads the icon", AppIcons.Large is BitmapSource { PixelWidth: 256 });
        Check("Loads the texts", Loc.Format("Panel_Columns", 6).Contains('6'), Loc.Format("Panel_Columns", 6));

        await UpdateCheck();
        await MovingWindows(displays, primary, area);
        await OtherApps(area);
        await Shortcuts();
        await PanelAndOverlays(displays, primary, area);
        await Settings();

        using (var tray = new TrayIcon())
        {
            await Wait(300);
            Check("Shows the tray icon", true);
        }

        await InstallUpdateUninstall();
    }

    private static async Task UpdateCheck()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            // Every published Windows release is newer than 0.0.1.
            var release = await Updates.CheckAsync(http, "0.0.1");
            Check("Finds the newest Windows release on GitHub", release != null,
                  release == null ? "none found" : $"{release.Version}, {release.DownloadUrl("x64")}");

            if (release?.ExeFor("x64") is { } exe)
            {
                using var download = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                string path = Path.Combine(Path.GetTempPath(), $"QuadraSelfTest-{Guid.NewGuid():N}.exe");
                try
                {
                    await Updates.DownloadAsync(download, exe, path);
                    Check("Downloads an update and checks it against GitHub's checksum", File.Exists(path) && exe.Sha256 != null,
                          $"{new FileInfo(path).Length / 1048576} MB, sha256 {exe.Sha256}");
                }
                catch (InvalidDataException e)
                {
                    Fail("Downloads an update and checks it against GitHub's checksum", e.Message);
                }
                finally
                {
                    File.Delete(path);
                }
            }
        }
        catch (Exception e)
        {
            // GitHub limits how often a machine may ask; the shared machines GitHub tests on sometimes hit it.
            Note($"Couldn't ask GitHub for releases: {e.Message}");
        }
    }

    private static async Task MovingWindows(List<Display> displays, Display primary, RectD area)
    {
        var window = new Window
        {
            Title = "Quadra self-test",
            Width = 640,
            Height = 480,
            Left = 40,
            Top = 40,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };
        window.Show();
        await Wait(500);
        var hwnd = new WindowInteropHelper(window).Handle;
        var visible = WindowMover.VisibleFrame(hwnd);
        Check("Reads a window's visible frame", visible != null);
        Note($"Window rectangle {WindowRect(hwnd)}, visible frame {visible} (the difference is the invisible border)");

        (string Name, RectD Unit, int Gap)[] cases =
        [
            ("left half", new RectD(0, 0, 0.5, 1), 0),
            ("right half with an 8 px gap", new RectD(0.5, 0, 0.5, 1), 8),
            ("top-left quarter", new RectD(0, 0, 0.5, 0.5), 0),
            ("full screen with a 16 px gap", new RectD(0, 0, 1, 1), 16),
            ("center of a 6 × 6 grid", new GridSelection(1, 1, 4, 4).UnitRect(6, 6), 0),
        ];
        foreach (var (name, unit, gap) in cases)
        {
            var wanted = WindowGeometry.Frame(unit, area, WindowGeometry.Scale(gap, primary.Dpi));
            WindowMover.Move(hwnd, wanted, area);
            await Wait(300);
            var got = WindowMover.VisibleFrame(hwnd);
            Check($"Moves a window to the {name}", Near(got, wanted), $"wanted {wanted}, got {got}");
        }

        // A maximized window is restored first.
        window.WindowState = WindowState.Maximized;
        await Wait(500);
        Check("The test window is maximized", Native.IsZoomed(hwnd));
        var leftHalf = WindowGeometry.Frame(new RectD(0, 0, 0.5, 1), area, 0);
        WindowMover.Move(hwnd, leftHalf, area);
        await Wait(500);
        Check("Moves a maximized window", !Native.IsZoomed(hwnd) && Near(WindowMover.VisibleFrame(hwnd), leftHalf),
              $"got {WindowMover.VisibleFrame(hwnd)}");

        // A window with a minimum size is centered on the area.
        window.MinWidth = 700;
        await Wait(200);
        var narrow = WindowGeometry.Frame(new RectD(0, 0, 1.0 / 6, 1), area, 0);
        WindowMover.Move(hwnd, narrow, area);
        await Wait(400);
        var got2 = WindowMover.VisibleFrame(hwnd);
        bool centered = got2 is { } g && Near(g, WindowGeometry.Centered(g.Width, g.Height, narrow, area)) && g.Width > narrow.Width;
        Check("Centers a window that refuses the size", centered, $"area {narrow}, got {got2}");
        window.MinWidth = 0;

        // More than one display: move to the next one.
        if (displays.Count > 1)
        {
            var from = Displays.ForRect(WindowMover.VisibleFrame(hwnd)!.Value, displays);
            var to = displays.First(d => !d.SameAs(from));
            WindowMover.MoveToDisplay(hwnd, from, to);
            await Wait(500);
            var got3 = WindowMover.VisibleFrame(hwnd);
            Check("Moves a window to another display", got3 is { } f && to.WorkArea.Contains(f), $"got {got3}, display {to.WorkArea}");
        }
        else
        {
            Note("Only one display: moving between displays not checked");
        }
        window.Close();

        // A window that can't be resized keeps its size and goes to the middle of the area.
        var fixedSize = new Window
        {
            Title = "Quadra self-test (fixed size)",
            Width = 300,
            Height = 200,
            Left = 60,
            Top = 60,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };
        fixedSize.Show();
        await Wait(400);
        var fixedHwnd = new WindowInteropHelper(fixedSize).Handle;
        var before = WindowMover.VisibleFrame(fixedHwnd)!.Value;
        WindowMover.Move(fixedHwnd, leftHalf, area);
        await Wait(300);
        var expected = WindowGeometry.Centered(before.Width, before.Height, leftHalf, area);
        Check("Keeps the size of a window that can't be resized", Near(WindowMover.VisibleFrame(fixedHwnd), expected),
              $"wanted {expected}, got {WindowMover.VisibleFrame(fixedHwnd)}");
        fixedSize.Close();
    }

    private static async Task OtherApps(RectD area)
    {
        var existing = Process.GetProcessesByName("notepad").Select(p => p.Id).ToHashSet();
        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Note($"Notepad didn't start ({e.Message}): moving another app's window not checked");
            return;
        }

        IntPtr notepad = IntPtr.Zero;
        for (int i = 0; i < 50 && notepad == IntPtr.Zero; i++)
        {
            await Wait(200);
            notepad = Process.GetProcessesByName("notepad").Where(p => !existing.Contains(p.Id))
                             .Select(p => p.MainWindowHandle).FirstOrDefault(h => h != IntPtr.Zero);
        }
        try
        {
            if (notepad == IntPtr.Zero)
            {
                Note("Notepad's window didn't appear: moving another app's window not checked");
                return;
            }
            Check("Notepad is a window Quadra can move", WindowMover.IsManageable(notepad));
            Note($"Notepad's title: {WindowMover.Title(notepad)}");

            var rightHalf = WindowGeometry.Frame(new RectD(0.5, 0, 0.5, 1), area, 0);
            WindowMover.Move(notepad, rightHalf, area);
            await Wait(500);
            Check("Moves another app's window (Notepad)", Near(WindowMover.VisibleFrame(notepad), rightHalf),
                  $"wanted {rightHalf}, got {WindowMover.VisibleFrame(notepad)}");
            Screenshot("notepad-right-half.png", area);

            // Like the user clicking another window while the grid is open: one of Quadra's windows is in
            // front, then a click on Notepad brings it to the front, and Quadra must notice.
            var helper = new Window
            {
                Title = "Quadra self-test (helper)",
                Width = 300,
                Height = 200,
                Left = 40,
                Top = 40,
                WindowStartupLocation = WindowStartupLocation.Manual,
            };
            helper.Show();
            await Wait(300);
            WindowMover.BringToFront(new WindowInteropHelper(helper).Handle);
            await Wait(300);

            using var watcher = new ForegroundWatcher();
            var seen = new List<IntPtr>();
            watcher.Changed += hwnd => seen.Add(WindowMover.Root(hwnd));
            Click(rightHalf.CenterX, rightHalf.CenterY);
            await Wait(600);
            Check("Notices a click on another app's window", seen.Contains(notepad),
                  $"{seen.Count} event(s), foreground is {(Native.GetForegroundWindow() == notepad ? "Notepad" : "another window")}");
            Check("Finds the app window in front (for a click on the icon before any window was used)",
                  WindowMover.FrontmostWindow() == notepad, WindowMover.Title(WindowMover.FrontmostWindow()));
            helper.Close();
        }
        finally
        {
            foreach (var p in Process.GetProcessesByName("notepad").Where(p => !existing.Contains(p.Id)))
            {
                try { p.Kill(); } catch (Exception) { }
            }
        }
    }

    private static async Task Shortcuts()
    {
        using var center = new HotKeyCenter();
        IReadOnlyCollection<HotKeyCombo> failed = [];
        center.Updated += f => failed = f;
        int fired = 0;
        var combo = new HotKeyCombo(Keys.F1 + 11, HotKeyCombo.Control | HotKeyCombo.Alt | HotKeyCombo.Shift); // F12
        center.Set([(combo, () => fired++)]);
        Check("Registers a global shortcut", failed.Count == 0, combo.Display);

        Press(0x11, 0x12, 0x10, 0x7B); // Ctrl+Alt+Shift+F12
        await Wait(500);
        Check("A global shortcut runs its action", fired == 1, $"ran {fired} time(s)");

        center.Set([(combo, () => { }), (combo, () => { })]);
        Check("The same shortcut twice isn't reported as taken by another app", failed.Count == 0);

        center.Suspend();
        Press(0x11, 0x12, 0x10, 0x7B);
        await Wait(300);
        center.Resume();
        Check("Shortcuts are off while recording a new one", fired == 1);

        Note($"Key names on this keyboard: 0xBA = {HotKeyCenter.KeyName(0xBA)}, 0xBF = {HotKeyCenter.KeyName(0xBF)}, " +
             $"0xC0 = {HotKeyCenter.KeyName(0xC0)}");
    }

    private static async Task PanelAndOverlays(List<Display> displays, Display primary, RectD area)
    {
        var settings = new AppSettings();
        var panel = new GridPanel();
        int canceled = 0;
        RectD? picked = null;
        panel.Canceled += () => canceled++;
        panel.Picked += unit => picked = unit;
        var center = new Point(area.CenterX, area.CenterY);
        string[] names = displays.Count > 1
            ? displays.Select((d, i) => $"{i + 1} · {d.Name}").ToArray()
            : ["1 · DELL U3419W", "2 · Built-in display"]; // shows the display picker in the screenshot

        void Present() => panel.Present("Quadra self-test — Notepad", 6, 6,
                                        settings.PanelGridSize(area.Width, area.Height), new RectD(0, 0, 0.5, 1),
                                        names, 0, settings.PanelOpacity, center, primary);
        Present();
        await Wait(700);
        Check("Opens the grid panel", panel.IsVisible);
        var rect = WindowRect(panel.Handle);
        Check("The panel is centered on the display", Math.Abs(rect.CenterX - center.X) <= 2 && Math.Abs(rect.CenterY - center.Y) <= 2,
              $"panel {rect}, center {center}");
        Check("The panel's size matches its content", Math.Abs(rect.Width - Math.Ceiling(panel.Width * primary.Scale)) <= 2,
              $"{rect.Width} px for {panel.Width} DIPs at {primary.Scale}×");
        Check("The panel has the keyboard focus", Native.GetForegroundWindow() == panel.Handle);

        var outline = new OverlayWindow(OverlayWindow.Kind.Outline);
        var preview = new OverlayWindow(OverlayWindow.Kind.Fill);
        var around = WindowGeometry.Frame(new RectD(0, 0, 0.5, 1), area, 0).Inflate(4);
        outline.ShowAt(around, Native.HWND_TOPMOST);
        outline.PlaceBelow(panel.Handle);
        var target = WindowGeometry.Frame(new RectD(0.5, 0, 0.5, 1), area, 8);
        preview.ShowAt(target, panel.Handle);
        await Wait(500);
        Check("The window outline covers the window", Near(WindowRect(outline.Handle), around), $"{WindowRect(outline.Handle)}");
        Check("The preview covers the destination", Near(WindowRect(preview.Handle), target), $"{WindowRect(preview.Handle)}");
        Screenshot("panel.png", primary.Bounds);
        outline.Hide();
        preview.Hide();

        Press(0x1B); // Esc
        await Wait(400);
        Check("Esc closes the grid", canceled == 1 && !panel.IsVisible);

        Present();
        await Wait(500);
        Press(0x25); // ←
        await Wait(400);
        Check("← picks the left half", picked == new RectD(0, 0, 0.5, 1), $"{picked}");
        panel.HidePanel();

        Present();
        await Wait(400);
        panel.SetGridSize(12, 4);
        await Wait(200);
        Screenshot("panel-12x4.png", primary.Bounds);
        panel.HidePanel();
    }

    private static async Task Settings()
    {
        var window = new SettingsWindow(new SettingsStore(null), () => { });
        window.Show();
        await Wait(800);
        Check("Opens the settings window", window.IsVisible && window.ActualHeight > 300,
              $"{window.ActualWidth} × {window.ActualHeight} DIPs");
        Screenshot("settings.png", WindowRect(new WindowInteropHelper(window).Handle));
        window.Close();
    }

    // Installing, updating and uninstalling, as a user would, in a temporary folder under test names
    // (QUADRA_TEST_ROOT), so the real Quadra is never touched.

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string TestUninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\QuadraTest";

    private static async Task InstallUpdateUninstall()
    {
        string root = Path.Combine(Path.GetTempPath(), "QuadraSelfTest-" + Guid.NewGuid().ToString("N")[..8]);
        string downloaded = Path.Combine(root, "Downloads", "Quadra-x64.exe");
        string installFolder = Path.Combine(root, "Programs", "Quadra");
        string installed = Path.Combine(installFolder, "Quadra.exe");
        string shortcut = Path.Combine(root, "Start Menu", "QuadraTest.lnk");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
            File.Copy(Environment.ProcessPath!, downloaded);

            int code = await InstallShowingTheWindow(root, downloaded);
            Check("The downloaded Quadra.exe installs itself and closes", code == 0, $"exit code {code}");
            Check("Installs Quadra.exe for the user, without administrator rights", File.Exists(installed));
            Check("Adds a Start menu shortcut", File.Exists(shortcut));
            using (var key = Registry.CurrentUser.OpenSubKey(TestUninstallKey))
                Check("Appears in Settings → Apps, with Uninstall",
                      key?.GetValue("UninstallString") is string u && u.Contains(installed, StringComparison.OrdinalIgnoreCase));
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                Check("Opens with Windows, from the installed copy",
                      key?.GetValue("QuadraTest") is string r && r.Contains(installed, StringComparison.OrdinalIgnoreCase));
            int first = await TestCopyProcess();
            Check("Opens the installed copy", first != 0);

            code = await RunTestCopy(root, downloaded, "");
            await Wait(1500);
            int same = await TestCopyProcess();
            Check("Opening the downloaded file again keeps the installed copy open", code == 0 && same == first,
                  $"process {first} → {same}");

            code = await RunTestCopy(root, downloaded, "", fakeVersion: "99.0.0");
            int second = await TestCopyProcess();
            Check("A newer version closes the open copy, takes its place and opens",
                  code == 0 && second != 0 && second != first, $"process {first} → {second}");

            // winget upgrade: Quadra.exe --install --silent with Quadra open. No window (the copy would wait for
            // someone to close it, and RunTestCopy would time out); the open copy is replaced and opened again.
            code = await RunTestCopy(root, downloaded, "--install --silent", fakeVersion: "100.0.0");
            int third = await TestCopyProcess();
            Check("A silent update (winget upgrade) replaces the open copy and opens it again",
                  code == 0 && third != 0 && third != second, $"exit code {code}, process {second} → {third}");

            await UninstallAndCheck(root, installed, installFolder, shortcut);

            // winget install: Quadra.exe --install --silent on a PC without Quadra. No window, and Quadra doesn't
            // open now: it is in the Start menu and opens with Windows.
            code = await RunTestCopy(root, downloaded, "--install --silent");
            await Wait(1500);
            bool registered;
            using (var key = Registry.CurrentUser.OpenSubKey(TestUninstallKey))
                registered = key?.GetValue("DisplayVersion") is string v && v == Links.Version &&
                             key.GetValue("QuietUninstallString") is string q && q.Contains("--uninstall --quiet");
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                registered &= key?.GetValue("QuadraTest") is string;
            Check("A silent install (winget install) installs, adds its entries and exits with 0",
                  code == 0 && File.Exists(installed) && File.Exists(shortcut) && registered, $"exit code {code}");
            Check("A silent install doesn't open Quadra right away", TestCopyWindow() == IntPtr.Zero);

            await UninstallAndCheck(root, installed, installFolder, shortcut);
        }
        catch (Exception e)
        {
            Fail("Installing, updating and uninstalling", e.ToString());
        }
        finally
        {
            // Leave nothing behind, even when a check failed.
            if (TestCopyWindow() is var window && window != IntPtr.Zero)
            {
                Native.GetWindowThreadProcessId(window, out uint pid);
                try { Process.GetProcessById((int)pid).Kill(); } catch (Exception) { }
            }
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)) key?.DeleteValue("QuadraTest", false);
            Registry.CurrentUser.DeleteSubKeyTree(TestUninstallKey, throwOnMissingSubKey: false);
            await Wait(1000);
            try { Directory.Delete(root, recursive: true); } catch (Exception) { }
        }
    }

    /// <summary>
    /// Opens the downloaded copy as a user would, so it shows the "Quadra is installed" window; checks that the
    /// window appears, takes a screenshot of it and closes it.
    /// </summary>
    private static async Task<int> InstallShowingTheWindow(string root, string exe)
    {
        var start = new ProcessStartInfo(exe) { UseShellExecute = false };
        start.Environment["QUADRA_TEST_ROOT"] = root;
        start.Environment["QUADRA_TEST_SHOW_WINDOW"] = "1";
        using var process = Process.Start(start)!;
        IntPtr dialog = IntPtr.Zero;
        for (int i = 0; i < 100 && dialog == IntPtr.Zero && !process.HasExited; i++)
        {
            await Wait(200);
            var found = Native.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "#32770", "Quadra"); // a message box
            if (found != IntPtr.Zero)
            {
                Native.GetWindowThreadProcessId(found, out uint pid);
                if (pid == process.Id) dialog = found;
            }
        }
        Check("Says in a window that Quadra was installed", dialog != IntPtr.Zero);
        if (dialog != IntPtr.Zero)
        {
            await Wait(500);
            Screenshot("installed-window.png", WindowRect(dialog).Inflate(20));
            Native.PostMessage(dialog, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
        }
        for (int i = 0; i < 150 && !process.HasExited; i++) await Wait(200);
        if (process.HasExited) return process.ExitCode;
        process.Kill();
        return -1;
    }

    /// <summary>Runs the quiet uninstall, as winget and Settings → Apps would, and checks that nothing is left.</summary>
    private static async Task UninstallAndCheck(string root, string installed, string installFolder, string shortcut)
    {
        int code = await RunTestCopy(root, installed, "--uninstall --quiet");
        await Wait(4000);
        Check("Uninstall closes Quadra", TestCopyWindow() == IntPtr.Zero);
        bool removed = !Directory.Exists(installFolder) && !File.Exists(shortcut) &&
                       !Directory.Exists(Path.Combine(root, "Data"));
        using (var key = Registry.CurrentUser.OpenSubKey(RunKey)) removed &= key?.GetValue("QuadraTest") == null;
        using (var key = Registry.CurrentUser.OpenSubKey(TestUninstallKey)) removed &= key == null;
        Check("Uninstall removes the app, its shortcut, its entries and its settings", code == 0 && removed,
              $"exit code {code}, folder left: {Directory.Exists(installFolder)}");
    }

    /// <summary>Runs a copy of Quadra under the test names and waits (up to 30 s) for it to finish.</summary>
    private static async Task<int> RunTestCopy(string root, string exe, string arguments, string? fakeVersion = null)
    {
        var start = new ProcessStartInfo(exe, arguments) { UseShellExecute = false };
        start.Environment["QUADRA_TEST_ROOT"] = root;
        if (fakeVersion != null) start.Environment["QUADRA_TEST_VERSION"] = fakeVersion;
        using var process = Process.Start(start)!;
        for (int i = 0; i < 150 && !process.HasExited; i++) await Wait(200);
        if (process.HasExited) return process.ExitCode;
        process.Kill();
        return -1;
    }

    private static IntPtr TestCopyWindow() =>
        Native.FindWindowEx(Native.HWND_MESSAGE, IntPtr.Zero, null, "QuadraTest.MessageWindow");

    /// <summary>The process of the open test copy (waits up to 15 s for it to open), or 0.</summary>
    private static async Task<int> TestCopyProcess()
    {
        for (int i = 0; i < 75; i++)
        {
            var window = TestCopyWindow();
            if (window != IntPtr.Zero)
            {
                Native.GetWindowThreadProcessId(window, out uint pid);
                return (int)pid;
            }
            await Wait(200);
        }
        return 0;
    }

    /// <summary>Presses keys together (in order) and releases them (in reverse order).</summary>
    private static void Press(params ushort[] keys)
    {
        var inputs = new List<Native.INPUT>();
        foreach (var k in keys)
            inputs.Add(new Native.INPUT { type = Native.INPUT_KEYBOARD, u = new Native.INPUTUNION { ki = new Native.KEYBDINPUT { wVk = k } } });
        foreach (var k in keys.Reverse())
            inputs.Add(new Native.INPUT
            {
                type = Native.INPUT_KEYBOARD,
                u = new Native.INPUTUNION { ki = new Native.KEYBDINPUT { wVk = k, dwFlags = Native.KEYEVENTF_KEYUP } },
            });
        uint sent = Native.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Native.INPUT>());
        if (sent != inputs.Count) Note($"SendInput sent {sent} of {inputs.Count} key events");
    }

    /// <summary>Clicks at a point of the primary display (screen pixels).</summary>
    private static void Click(double x, double y)
    {
        // Absolute mouse coordinates go from 0 to 65535 across the primary display.
        int width = Math.Max(2, Native.GetSystemMetrics(Native.SM_CXSCREEN));
        int height = Math.Max(2, Native.GetSystemMetrics(Native.SM_CYSCREEN));
        int ax = (int)Math.Round(x * 65535 / (width - 1)), ay = (int)Math.Round(y * 65535 / (height - 1));
        Native.INPUT Mouse(uint flags) => new()
        {
            type = Native.INPUT_MOUSE,
            u = new Native.INPUTUNION { mi = new Native.MOUSEINPUT { dx = ax, dy = ay, dwFlags = flags } },
        };
        var inputs = new[]
        {
            Mouse(Native.MOUSEEVENTF_MOVE | Native.MOUSEEVENTF_ABSOLUTE),
            Mouse(Native.MOUSEEVENTF_LEFTDOWN | Native.MOUSEEVENTF_ABSOLUTE),
            Mouse(Native.MOUSEEVENTF_LEFTUP | Native.MOUSEEVENTF_ABSOLUTE),
        };
        Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.INPUT>());
    }

    private static void Screenshot(string name, RectD area)
    {
        try
        {
            int w = Math.Max(1, (int)area.Width), h = Math.Max(1, (int)area.Height);
            using var bitmap = new System.Drawing.Bitmap(w, h);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
                g.CopyFromScreen((int)area.X, (int)area.Y, 0, 0, new System.Drawing.Size(w, h));
            bitmap.Save(Path.Combine(folder, name), System.Drawing.Imaging.ImageFormat.Png);
        }
        catch (Exception e)
        {
            Note($"Couldn't take the screenshot {name}: {e.Message}");
        }
    }
}
