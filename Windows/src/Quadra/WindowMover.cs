using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Quadra;

/// <summary>Reads and changes other apps' windows. Windows needs no special permission for this.</summary>
internal static class WindowMover
{
    /// <summary>Parts of Windows itself that look like windows but must never be moved.</summary>
    private static readonly HashSet<string> ShellClasses =
    [
        "Progman", "WorkerW",                                           // the desktop
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd",                      // the taskbars
        "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland", // hidden icons (^)
        "Windows.UI.Core.CoreWindow",                                   // Start, search, notifications
        "XamlExplorerHostIslandWindow", "MultitaskingViewFrame", "ForegroundStaging", // Alt+Tab, Task view
        "TaskListThumbnailWnd", "Shell_InputSwitchTopLevelWindow", "#32768", // previews, language switch, menus
    ];

    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;

    /// <summary>The top-level window that contains <paramref name="hwnd"/>.</summary>
    public static IntPtr Root(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return IntPtr.Zero;
        var root = Native.GetAncestor(hwnd, Native.GA_ROOT);
        return root != IntPtr.Zero ? root : hwnd;
    }

    public static bool IsOwn(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == OwnProcessId;
    }

    /// <summary>Whether the window is an app window Quadra can move (not the desktop, the taskbar or Quadra itself).</summary>
    public static bool IsManageable(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd) || !Native.IsWindowVisible(hwnd) || IsOwn(hwnd)) return false;
        if (ShellClasses.Contains(ClassName(hwnd))) return false;
        long style = Native.GetWindowLongPtr(hwnd, Native.GWL_STYLE).ToInt64();
        long exStyle = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
        if ((style & Native.WS_CHILD) != 0) return false;
        // Tool windows without a title bar are palettes and popups, not app windows.
        if ((exStyle & Native.WS_EX_TOOLWINDOW) != 0 && (style & Native.WS_CAPTION) != Native.WS_CAPTION) return false;
        // Windows on another virtual desktop, or suspended Store apps, are "cloaked": present but invisible.
        if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;
        return VisibleFrame(hwnd) is { Width: >= 20, Height: >= 20 };
    }

    /// <summary>
    /// The app window nearest the front of the screen (not minimized), or nothing when no app window is open.
    /// Used when the tray icon is clicked before any window was used since Quadra opened.
    /// </summary>
    public static IntPtr FrontmostWindow()
    {
        IntPtr found = IntPtr.Zero;
        Native.EnumWindows((hwnd, _) =>
        {
            if (Native.IsIconic(hwnd) || !IsManageable(hwnd)) return true;
            found = hwnd;
            return false; // stop: windows come from the front to the back
        }, IntPtr.Zero);
        return found;
    }

    /// <summary>
    /// The window as people see it. Windows 10 and 11 add invisible borders of a few pixels around most
    /// windows; the frame here leaves them out, so windows line up exactly with the screen edges.
    /// </summary>
    public static RectD? VisibleFrame(IntPtr hwnd)
    {
        if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out Native.RECT r,
                                         Marshal.SizeOf<Native.RECT>()) == 0)
            return r.ToRectD();
        return Native.GetWindowRect(hwnd, out r) ? r.ToRectD() : null;
    }

    public static bool IsResizable(IntPtr hwnd) =>
        (Native.GetWindowLongPtr(hwnd, Native.GWL_STYLE).ToInt64() & Native.WS_THICKFRAME) != 0;

    /// <summary>The window title, or the app's name when the window has none.</summary>
    public static string Title(IntPtr hwnd)
    {
        var text = new StringBuilder(512);
        Native.GetWindowText(hwnd, text, text.Capacity);
        if (text.Length > 0) return text.ToString();
        return AppName(hwnd) ?? Loc.Get("Panel_Window");
    }

    private static string? AppName(IntPtr hwnd)
    {
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        var process = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return null;
        try
        {
            var path = new StringBuilder(1024);
            uint size = (uint)path.Capacity;
            if (!Native.QueryFullProcessImageName(process, 0, path, ref size)) return null;
            var info = FileVersionInfo.GetVersionInfo(path.ToString());
            return string.IsNullOrWhiteSpace(info.FileDescription)
                ? System.IO.Path.GetFileNameWithoutExtension(path.ToString())
                : info.FileDescription;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            Native.CloseHandle(process);
        }
    }

    private static string ClassName(IntPtr hwnd)
    {
        var name = new StringBuilder(256);
        Native.GetClassName(hwnd, name, name.Capacity);
        return name.ToString();
    }

    /// <summary>
    /// Moves and resizes the window so its visible frame is <paramref name="target"/>.
    /// <paramref name="area"/> is the usable area of the destination display.
    /// Returns where the window was put, or null when it can't be moved.
    /// </summary>
    public static RectD? Move(IntPtr hwnd, RectD target, RectD area)
    {
        if (!Native.IsWindow(hwnd) || Native.IsHungAppWindow(hwnd)) return null;
        // A maximized or minimized window has to go back to normal before it can take another size.
        if (Native.IsZoomed(hwnd) || Native.IsIconic(hwnd)) Native.ShowWindow(hwnd, Native.SW_RESTORE);

        bool resizable = IsResizable(hwnd);
        var wanted = target;
        // Windows that can't be resized (most dialogs) keep their size and go to the middle of the area.
        if (!resizable && VisibleFrame(hwnd) is { } current)
            wanted = WindowGeometry.Centered(current.Width, current.Height, target, area);

        Place(hwnd, wanted, resizable);
        // Again: moving to a display with another scaling changes the invisible borders,
        // and some apps resize themselves when that happens.
        Place(hwnd, wanted, resizable);

        // Some apps have a minimum or fixed size. If the window didn't take the requested size,
        // center it on the chosen area instead of leaving it stuck in a corner.
        if (resizable && VisibleFrame(hwnd) is { } actual &&
            (Math.Abs(actual.Width - wanted.Width) > 2 || Math.Abs(actual.Height - wanted.Height) > 2))
        {
            wanted = WindowGeometry.Centered(actual.Width, actual.Height, target, area);
            Place(hwnd, wanted, resize: false);
        }
        return wanted;
    }

    /// <summary>
    /// Moves a window to another display, keeping its relative position and size. A maximized window
    /// is maximized again on the new display.
    /// </summary>
    public static bool MoveToDisplay(IntPtr hwnd, Display from, Display to)
    {
        bool wasMaximized = Native.IsZoomed(hwnd);
        if (wasMaximized || Native.IsIconic(hwnd)) Native.ShowWindow(hwnd, Native.SW_RESTORE);
        if (VisibleFrame(hwnd) is not { } frame) return false;
        var mapped = WindowGeometry.Map(frame, from.WorkArea, to.WorkArea);
        if (Move(hwnd, mapped, to.WorkArea) == null) return false;
        if (wasMaximized) Native.ShowWindow(hwnd, Native.SW_MAXIMIZE);
        return true;
    }

    /// <summary>Puts the window's visible frame at <paramref name="frame"/>, adding the invisible borders.</summary>
    private static void Place(IntPtr hwnd, RectD frame, bool resize)
    {
        if (!Native.GetWindowRect(hwnd, out var outer)) return;
        var visible = VisibleFrame(hwnd) ?? outer.ToRectD();
        int left = Math.Max(0, (int)visible.Left - outer.Left);
        int top = Math.Max(0, (int)visible.Top - outer.Top);
        int right = Math.Max(0, outer.Right - (int)visible.Right);
        int bottom = Math.Max(0, outer.Bottom - (int)visible.Bottom);
        uint flags = Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER;
        if (!resize) flags |= Native.SWP_NOSIZE;
        if (!Native.SetWindowPos(hwnd, IntPtr.Zero, (int)frame.X - left, (int)frame.Y - top,
                                 (int)frame.Width + left + right, (int)frame.Height + top + bottom, flags))
        {
            // Usually a window of an app running as administrator: Windows doesn't let normal apps move it.
            Log.Write($"Windows refused to move \"{Title(hwnd)}\" (error {Marshal.GetLastWin32Error()})");
        }
    }

    /// <summary>Gives the focus to a window.</summary>
    public static void Focus(IntPtr hwnd)
    {
        if (!Native.IsWindow(hwnd)) return;
        if (Native.IsIconic(hwnd)) Native.ShowWindow(hwnd, Native.SW_RESTORE);
        BringToFront(hwnd);
    }

    /// <summary>
    /// Brings a window to the front. Windows only lets the app the user is using do that, so Quadra
    /// first sends an empty mouse event (the same trick Microsoft PowerToys uses).
    /// </summary>
    public static void BringToFront(IntPtr hwnd)
    {
        if (Native.GetForegroundWindow() == hwnd) return;
        if (Native.SetForegroundWindow(hwnd)) return;
        var input = new[] { new Native.INPUT { type = Native.INPUT_MOUSE } }; // no movement, no buttons
        Native.SendInput(1, input, Marshal.SizeOf<Native.INPUT>());
        Native.SetForegroundWindow(hwnd);
    }

    public static void Beep() => Native.MessageBeep(0);
}
