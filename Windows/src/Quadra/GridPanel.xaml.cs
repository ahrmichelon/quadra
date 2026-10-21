using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Quadra;

/// <summary>
/// Floating, see-through panel with the grid: title, buttons, display picker, grid and a one-line footer.
/// </summary>
public partial class GridPanel : Window
{
    /// <summary>The chosen area, as fractions of the screen (0 to 1, origin at the top left).</summary>
    public event Action<RectD>? Picked;
    /// <summary>The user closed the panel with Esc or the close button.</summary>
    public event Action? Canceled;
    /// <summary>The dragged area changed (null when the drag ends), to show the preview on screen.</summary>
    public event Action<RectD?>? PreviewChanged;
    /// <summary>The user picked another display in the display picker.</summary>
    public event Action<int>? DisplayPicked;
    /// <summary>The gear was clicked; the point (screen pixels) is where the menu should open.</summary>
    public event Action<Point>? GearClicked;
    /// <summary>The user changed columns or rows with − and +.</summary>
    public event Action<int, int>? GridSizeChanged;
    /// <summary>The panel closed, for any reason.</summary>
    public event Action? Hidden;

    /// <summary>When the panel last closed (so a click on the tray icon that closed it doesn't reopen it).</summary>
    public DateTime LastHidden { get; private set; } = DateTime.MinValue;

    public GridPanel()
    {
        InitializeComponent();
        HintText.Text = Loc.Get("Panel_Hint");
        Cells.ToolTip = Loc.Get("Panel_GridTip");
        GearButton.ToolTip = Loc.Get("Panel_Settings");
        CloseButton.ToolTip = Loc.Get("Panel_Close");
        FewerColumns.ToolTip = Loc.Get("Panel_FewerColumns");
        MoreColumns.ToolTip = Loc.Get("Panel_MoreColumns");
        FewerRows.ToolTip = Loc.Get("Panel_FewerRows");
        MoreRows.ToolTip = Loc.Get("Panel_MoreRows");

        Cells.Picked += selection => Picked?.Invoke(selection.UnitRect(Cells.Cols, Cells.Rows));
        Cells.SelectionChanged += selection => PreviewChanged?.Invoke(selection?.UnitRect(Cells.Cols, Cells.Rows));
        CloseButton.Click += (_, _) => Cancel();
        GearButton.Click += (_, _) =>
            GearClicked?.Invoke(GearButton.PointToScreen(new Point(0, GearButton.ActualHeight + 2)));
        FewerColumns.Click += (_, _) => ChangeGridSize(-1, 0);
        MoreColumns.Click += (_, _) => ChangeGridSize(1, 0);
        FewerRows.Click += (_, _) => ChangeGridSize(0, -1);
        MoreRows.Click += (_, _) => ChangeGridSize(0, 1);
        PreviewKeyDown += OnKey;

        SourceInitialized += (_, _) =>
        {
            // Keep the panel out of Alt+Tab.
            var hwnd = new WindowInteropHelper(this).Handle;
            long ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64() | Native.WS_EX_TOOLWINDOW;
            Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(ex));
        };
    }

    public IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

    /// <summary>The middle of the panel on screen, in pixels.</summary>
    public Point ScreenCenter =>
        Native.GetWindowRect(Handle, out var r) ? new Point((r.Left + r.Right) / 2.0, (r.Top + r.Bottom) / 2.0) : default;

    /// <summary>
    /// Shows (or updates) the panel, centered on <paramref name="center"/> (screen pixels) and kept inside
    /// <paramref name="display"/>. The grid size is in device-independent pixels.
    /// </summary>
    public void Present(string title, int cols, int rows, (double Width, double Height) gridSize, RectD? current,
                        IReadOnlyList<string> displayNames, int selectedDisplay, double opacity,
                        Point center, Display display)
    {
        ApplyTheme(opacity);
        TitleText.Text = title;
        Stack.Width = gridSize.Width;
        Cells.Width = gridSize.Width;
        Cells.Height = gridSize.Height;
        Cells.Reset();
        Cells.CurrentWindow = current;
        SetGridSize(cols, rows);
        BuildDisplayPicker(displayNames, selectedDisplay);

        // Size in device-independent pixels, then in real pixels for the display that will show it.
        Frame.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = Math.Ceiling(Frame.DesiredSize.Width), height = Math.Ceiling(Frame.DesiredSize.Height);
        Width = width;
        Height = height;

        var hwnd = Handle;
        int w = (int)Math.Ceiling(width * display.Scale), h = (int)Math.Ceiling(height * display.Scale);
        var area = display.WorkArea;
        int x = (int)Math.Round(center.X - w / 2.0), y = (int)Math.Round(center.Y - h / 2.0);
        x = (int)Math.Max(area.Left, Math.Min(x, area.Right - w));
        y = (int)Math.Max(area.Top, Math.Min(y, area.Bottom - h));

        // Twice: moving to a display with another scaling makes WPF resize the window once.
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, w, h, Native.SWP_NOACTIVATE);
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, w, h, Native.SWP_NOACTIVATE);
        if (!IsVisible) Show();
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, w, h, Native.SWP_NOACTIVATE);

        WindowMover.BringToFront(hwnd);
        Activate();
        Keyboard.Focus(Cells);
    }

    /// <summary>Hides the panel without moving anything.</summary>
    public void HidePanel()
    {
        if (!IsVisible) return;
        Hide();
        LastHidden = DateTime.Now;
        Hidden?.Invoke();
    }

    public void Cancel()
    {
        HidePanel();
        Canceled?.Invoke();
    }

    /// <summary>Updates the grid and the − and + buttons (without raising <see cref="GridSizeChanged"/>).</summary>
    public void SetGridSize(int cols, int rows)
    {
        Cells.Cols = cols;
        Cells.Rows = rows;
        ColumnsText.Text = Loc.Format("Panel_Columns", cols);
        RowsText.Text = Loc.Format("Panel_Rows", rows);
        FewerColumns.IsEnabled = cols > AppSettings.MinSize;
        MoreColumns.IsEnabled = cols < AppSettings.MaxSize;
        FewerRows.IsEnabled = rows > AppSettings.MinSize;
        MoreRows.IsEnabled = rows < AppSettings.MaxSize;
    }

    private void ChangeGridSize(int columnStep, int rowStep)
    {
        int cols = AppSettings.ClampSize(Cells.Cols + columnStep);
        int rows = AppSettings.ClampSize(Cells.Rows + rowStep);
        SetGridSize(cols, rows);
        GridSizeChanged?.Invoke(cols, rows);
        Keyboard.Focus(Cells);
    }

    private void BuildDisplayPicker(IReadOnlyList<string> names, int selected)
    {
        DisplayPicker.Children.Clear();
        DisplayPicker.Visibility = names.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        if (names.Count < 2) return;
        var style = (Style)Resources["Segment"];
        for (int i = 0; i < names.Count; i++)
        {
            int index = i;
            var button = new RadioButton
            {
                Content = names[i],
                ToolTip = names[i],
                Style = style,
                GroupName = "Displays",
                IsChecked = i == selected,
            };
            button.Click += (_, _) => DisplayPicked?.Invoke(index);
            DisplayPicker.Children.Add(button);
        }
    }

    /// <summary>Colors for light or dark mode, the accent color and the chosen opacity.</summary>
    private void ApplyTheme(double opacity)
    {
        bool dark = Theme.IsDark;
        var accent = Theme.Accent;
        var background = dark ? Color.FromRgb(0x20, 0x20, 0x20) : Color.FromRgb(0xF3, 0xF3, 0xF3);
        var ink = dark ? Colors.White : Color.FromRgb(0x1A, 0x1A, 0x1A);

        Resources["PanelBackground"] = Theme.Brush(background, opacity);
        Resources["PanelBorder"] = Theme.Brush(dark ? Colors.White : Colors.Black, dark ? 0.18 : 0.16);
        Resources["PanelText"] = Theme.Brush(ink);
        Resources["PanelSubtleText"] = Theme.Brush(ink, 0.72);
        Resources["HoverFill"] = Theme.Brush(ink, 0.09);
        Resources["PressedFill"] = Theme.Brush(ink, 0.16);
        Resources["SegmentFill"] = Theme.Brush(ink, 0.07);
        Resources["AccentFill"] = Theme.Brush(accent);
        Resources["AccentText"] = Theme.Brush(Theme.TextOn(accent));
        Cells.SetColors(accent, ink);

        // A soft glow in the background color keeps the text readable over the apps behind.
        var glow = new DropShadowEffect { Color = background, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.9 };
        glow.Freeze();
        foreach (var text in new[] { TitleText, ColumnsText, RowsText, HintText }) text.Effect = glow;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancel();
            return;
        }
        RectD? unit = e.Key switch
        {
            Key.Left => new RectD(0, 0, 0.5, 1),   // left half
            Key.Right => new RectD(0.5, 0, 0.5, 1), // right half
            Key.Up => new RectD(0, 0, 1, 0.5),     // top half
            Key.Down => new RectD(0, 0.5, 1, 0.5), // bottom half
            Key.Enter => new RectD(0, 0, 1, 1),    // full screen
            _ => null,
        };
        if (unit is { } u)
        {
            e.Handled = true;
            Picked?.Invoke(u);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Alt+F4 only hides the panel; it is reused every time.
        if (!QuadraApp.IsQuitting)
        {
            e.Cancel = true;
            Cancel();
        }
        base.OnClosing(e);
    }
}
