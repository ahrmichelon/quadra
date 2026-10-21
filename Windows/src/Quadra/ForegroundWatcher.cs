using System;

namespace Quadra;

/// <summary>
/// Tells Quadra whenever another app's window comes to the front (a click, Alt+Tab, the taskbar).
/// That is how Quadra knows which window to move and when the user clicks another window while the grid is open.
/// </summary>
internal sealed class ForegroundWatcher : IDisposable
{
    private readonly Native.WinEventProc callback; // kept in a field so it isn't garbage collected
    private readonly IntPtr hook;

    /// <summary>Called on the UI thread with the window that came to the front.</summary>
    public event Action<IntPtr>? Changed;

    public ForegroundWatcher()
    {
        callback = OnEvent;
        hook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
                                      callback, 0, 0, Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);
        if (hook == IntPtr.Zero) Log.Write("Couldn't follow the focused window (SetWinEventHook failed)");
    }

    private void OnEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject == Native.OBJID_WINDOW && hwnd != IntPtr.Zero) Changed?.Invoke(hwnd);
    }

    public void Dispose()
    {
        if (hook != IntPtr.Zero) Native.UnhookWinEvent(hook);
    }
}
