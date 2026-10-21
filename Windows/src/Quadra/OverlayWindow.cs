using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Quadra;

/// <summary>
/// See-through window that ignores clicks: draws the outline around the chosen window
/// or the preview of the destination area.
/// </summary>
internal sealed class OverlayWindow : Window
{
    public enum Kind { Outline, Fill }

    private readonly Kind kind;
    private readonly Border box = new() { CornerRadius = new CornerRadius(8) };

    public OverlayWindow(Kind kind)
    {
        this.kind = kind;
        Title = "Quadra";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        Content = box;
        SourceInitialized += (_, _) =>
        {
            // Clicks go through to the windows below; never activated; not in Alt+Tab.
            var hwnd = new WindowInteropHelper(this).Handle;
            long ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
            ex |= Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
            Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(ex));
        };
    }

    public IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

    /// <summary>
    /// Shows over <paramref name="frame"/> (physical pixels), just below <paramref name="insertAfter"/>
    /// in the stack of windows (or at the very top with HWND_TOPMOST).
    /// </summary>
    public void ShowAt(RectD frame, IntPtr insertAfter)
    {
        var accent = Theme.Accent;
        if (kind == Kind.Outline)
        {
            box.BorderBrush = Theme.Brush(accent);
            box.BorderThickness = new Thickness(4);
            box.Margin = new Thickness(2);
            box.Background = null;
        }
        else
        {
            box.BorderBrush = Theme.Brush(accent, 0.9);
            box.BorderThickness = new Thickness(3);
            box.Margin = new Thickness(1.5);
            box.Background = Theme.Brush(accent, 0.22);
        }

        var hwnd = Handle;
        int x = (int)frame.X, y = (int)frame.Y, w = (int)frame.Width, h = (int)frame.Height;
        const uint flags = Native.SWP_NOACTIVATE;
        // Twice: crossing to a display with another scaling makes WPF resize the window once.
        Native.SetWindowPos(hwnd, insertAfter, x, y, w, h, flags);
        Native.SetWindowPos(hwnd, insertAfter, x, y, w, h, flags);
        if (!IsVisible) Show();
        Native.SetWindowPos(hwnd, insertAfter, x, y, w, h, flags);
    }

    /// <summary>Moves the overlay just below <paramref name="window"/> without moving it on screen.</summary>
    public void PlaceBelow(IntPtr window)
    {
        if (IsVisible)
            Native.SetWindowPos(Handle, window, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }
}
