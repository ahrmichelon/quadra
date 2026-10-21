using System;
using System.Windows.Media;
using Microsoft.Win32;

namespace Quadra;

/// <summary>Colors that follow Windows: light or dark mode, and the accent color.</summary>
internal static class Theme
{
    /// <summary>Quadra's own blue, used when Windows has no accent color.</summary>
    public static readonly Color BrandBlue = Color.FromRgb(0x3B, 0x62, 0xF4);

    /// <summary>True when Windows apps are set to dark mode.</summary>
    public static bool IsDark
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>The accent color chosen in Settings → Personalization → Colors.</summary>
    public static Color Accent
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
                if (key?.GetValue("AccentColor") is int abgr)
                    return Color.FromRgb((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF));
            }
            catch (Exception)
            {
            }
            return BrandBlue;
        }
    }

    /// <summary>Black or white, whichever reads better on <paramref name="background"/>.</summary>
    public static Color TextOn(Color background) =>
        0.299 * background.R + 0.587 * background.G + 0.114 * background.B > 150 ? Colors.Black : Colors.White;

    public static Color WithAlpha(Color c, double alpha) =>
        Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), c.R, c.G, c.B);

    public static SolidColorBrush Brush(Color c, double alpha = 1)
    {
        var brush = new SolidColorBrush(WithAlpha(c, alpha));
        brush.Freeze();
        return brush;
    }
}
