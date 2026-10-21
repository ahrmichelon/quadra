using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Quadra;

/// <summary>
/// Registers the global shortcuts with RegisterHotKey. Its hidden window also receives the message a second
/// copy of Quadra sends to open the settings.
/// </summary>
internal sealed class HotKeyCenter : IDisposable
{
    /// <summary>The hidden window's name, which other copies of Quadra look for.</summary>
    public static string WindowName => AppPaths.Name + ".MessageWindow";
    public static readonly uint ShowSettingsMessage = Native.RegisterWindowMessage("Quadra.ShowSettings");
    /// <summary>Sent by a newer Quadra.exe that is installing itself, or by Uninstall.</summary>
    public static readonly uint QuitMessage = Native.RegisterWindowMessage("Quadra.Quit");

    private readonly HwndSource window;
    private readonly Dictionary<int, (HotKeyCombo Combo, Action Action)> bindings = [];
    private readonly List<int> registered = [];
    private bool suspended;

    /// <summary>Called after every registration with the shortcuts that could not be used.</summary>
    public event Action<IReadOnlyCollection<HotKeyCombo>>? Updated;

    /// <summary>Someone opened Quadra.exe again while it was already running.</summary>
    public event Action? ShowSettingsRequested;

    /// <summary>A newer Quadra is replacing this one, or Quadra is being uninstalled.</summary>
    public event Action? QuitRequested;

    public HotKeyCenter()
    {
        // A message-only window: invisible, never in Alt+Tab, but it receives WM_HOTKEY.
        window = new HwndSource(new HwndSourceParameters(WindowName)
        {
            ParentWindow = Native.HWND_MESSAGE,
            WindowStyle = 0,
            Width = 0,
            Height = 0,
        });
        window.AddHook(WndProc);
    }

    /// <summary>Replaces all shortcuts at once.</summary>
    public void Set(IEnumerable<(HotKeyCombo Combo, Action Action)> list)
    {
        UnregisterAll();
        bindings.Clear();
        int id = 1;
        foreach (var entry in list) bindings[id++] = entry;
        if (!suspended) RegisterAll();
    }

    /// <summary>Turns the shortcuts off while the user records a new one (otherwise the old one would fire).</summary>
    public void Suspend()
    {
        suspended = true;
        UnregisterAll();
    }

    public void Resume()
    {
        if (!suspended) return;
        suspended = false;
        RegisterAll();
    }

    private void RegisterAll()
    {
        UnregisterAll();
        var failed = new HashSet<HotKeyCombo>();
        var seen = new HashSet<HotKeyCombo>();
        foreach (var (id, binding) in bindings.OrderBy(b => b.Key))
        {
            // The same shortcut twice: the first one wins and the settings show a warning.
            if (!seen.Add(binding.Combo)) continue;
            uint modifiers = (uint)binding.Combo.Modifiers | Native.MOD_NOREPEAT;
            if (Native.RegisterHotKey(window.Handle, id, modifiers, (uint)binding.Combo.Key))
            {
                registered.Add(id);
            }
            else
            {
                failed.Add(binding.Combo);
                Log.Write($"Shortcut {binding.Combo.Display} is taken (error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()})");
            }
        }
        Updated?.Invoke(failed);
    }

    private void UnregisterAll()
    {
        foreach (int id in registered) Native.UnregisterHotKey(window.Handle, id);
        registered.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == Native.WM_HOTKEY)
        {
            if (bindings.TryGetValue(wParam.ToInt32(), out var binding))
            {
                // Run after this message is done, so the action can open windows freely.
                window.Dispatcher.BeginInvoke(binding.Action, DispatcherPriority.Input);
            }
            handled = true;
        }
        else if ((uint)message == ShowSettingsMessage)
        {
            window.Dispatcher.BeginInvoke(() => ShowSettingsRequested?.Invoke());
            handled = true;
        }
        else if ((uint)message == QuitMessage)
        {
            window.Dispatcher.BeginInvoke(() => QuitRequested?.Invoke());
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        window.Dispose();
    }

    /// <summary>Tells a copy of Quadra that is already running to open its settings. False if none was found.</summary>
    public static bool AskRunningCopyToShowSettings()
    {
        var other = Native.FindWindowEx(Native.HWND_MESSAGE, IntPtr.Zero, null, WindowName);
        if (other == IntPtr.Zero) return false;
        Native.AllowSetForegroundWindow(Native.ASFW_ANY);
        return Native.PostMessage(other, ShowSettingsMessage, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// Names keys whose label depends on the keyboard layout (such as Ç, ´ or ;), for <see cref="Keys.Name"/>.
    /// </summary>
    public static string? KeyName(int key)
    {
        uint ch = Native.MapVirtualKey((uint)key, Native.MAPVK_VK_TO_CHAR) & 0xFFFF;
        if (ch > 32) return char.ToUpperInvariant((char)ch).ToString();
        uint scanCode = Native.MapVirtualKey((uint)key, Native.MAPVK_VK_TO_VSC);
        if (scanCode == 0) return null;
        var name = new StringBuilder(64);
        return Native.GetKeyNameText((int)(scanCode << 16), name, name.Capacity) > 0 ? name.ToString() : null;
    }
}
