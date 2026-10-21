using System;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace Quadra;

/// <summary>The Quadra icon next to the clock. Left click opens the grid; right click shows the menu.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon icon;

    public WinForms.ContextMenuStrip Menu { get; } = new() { ShowImageMargin = false, ShowCheckMargin = true };

    public bool MenuIsOpen { get; private set; }

    /// <summary>Left click on the icon.</summary>
    public event Action? Clicked;

    /// <summary>The menu is about to open: fill <see cref="Menu"/> now.</summary>
    public event Action? MenuOpening;

    public event Action? MenuClosed;

    /// <summary>The user clicked the last notification shown with <see cref="ShowBalloon"/>.</summary>
    public event Action? BalloonClicked;

    public TrayIcon()
    {
        Menu.Opening += (_, e) =>
        {
            MenuOpening?.Invoke();
            MenuIsOpen = true;
            e.Cancel = false; // WinForms cancels by default when the menu was empty before filling it
        };
        Menu.Closed += (_, _) =>
        {
            MenuIsOpen = false;
            MenuClosed?.Invoke();
        };

        icon = new WinForms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = Loc.Get("Tray_Tip"),
            ContextMenuStrip = Menu,
            Visible = true,
        };
        icon.BalloonTipClicked += (_, _) => BalloonClicked?.Invoke();
        icon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left) Clicked?.Invoke();
        };
    }

    private static System.Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Quadra.ico"));
        using var stream = resource.Stream;
        return new System.Drawing.Icon(stream, WinForms.SystemInformation.SmallIconSize);
    }

    /// <summary>Shows the menu with its top left corner at a screen point (pixels).</summary>
    public void ShowMenuAt(Point point) => Menu.Show(new System.Drawing.Point((int)point.X, (int)point.Y));

    public void ShowBalloon(string title, string text) =>
        icon.ShowBalloonTip(10000, title, text, WinForms.ToolTipIcon.Info);

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
        Menu.Dispose();
    }
}
