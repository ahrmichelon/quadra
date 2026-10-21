using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Quadra;

/// <summary>The grid of cells that stands for the screen. Drag across it to pick the window's area.</summary>
public sealed class GridCanvas : FrameworkElement
{
    private const double Spacing = 3;

    private int cols = 6, rows = 6;
    private RectD? currentWindow;
    private GridSelection? selection;
    private (int Col, int Row)? dragStart, hover;
    private Color accent = Theme.BrandBlue, ink = Colors.Black;

    /// <summary>The user let go of the mouse: this is the chosen area.</summary>
    public event Action<GridSelection>? Picked;

    /// <summary>The area changes while the user drags (null when the drag ends).</summary>
    public event Action<GridSelection?>? SelectionChanged;

    public GridCanvas()
    {
        Focusable = true;
        FocusVisualStyle = null;
    }

    public int Cols { get => cols; set { cols = Math.Max(1, value); selection = null; InvalidateVisual(); } }
    public int Rows { get => rows; set { rows = Math.Max(1, value); selection = null; InvalidateVisual(); } }

    /// <summary>Where the window is now, as fractions of the screen (0 to 1, origin at the top left).</summary>
    public RectD? CurrentWindow { get => currentWindow; set { currentWindow = value; InvalidateVisual(); } }

    public GridSelection? Selection { get => selection; set { selection = value; InvalidateVisual(); } }

    /// <summary>The accent color (chosen cells) and the ink color (empty cells, lines), from the theme.</summary>
    public void SetColors(Color accentColor, Color inkColor)
    {
        accent = accentColor;
        ink = inkColor;
        InvalidateVisual();
    }

    public void Reset()
    {
        selection = null;
        dragStart = null;
        hover = null;
        InvalidateVisual();
    }

    // Drawing

    private Size CellSize => new(
        Math.Max(0, (ActualWidth - Spacing * (cols - 1)) / cols),
        Math.Max(0, (ActualHeight - Spacing * (rows - 1)) / rows));

    private Rect CellRect(int col, int row)
    {
        var s = CellSize;
        return new Rect(col * (s.Width + Spacing), row * (s.Height + Spacing), s.Width, s.Height);
    }

    private (int Col, int Row) CellAt(Point p)
    {
        var s = CellSize;
        int col = (int)Math.Floor(p.X / (s.Width + Spacing));
        int row = (int)Math.Floor(p.Y / (s.Height + Spacing));
        return (Math.Clamp(col, 0, cols - 1), Math.Clamp(row, 0, rows - 1));
    }

    protected override void OnRender(DrawingContext dc)
    {
        // Transparent background so the gaps between cells also catch the mouse.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

        // Translucent colors, so the see-through panel still shows the apps behind it.
        var chosen = Theme.Brush(accent, 0.7);
        var hovered = Theme.Brush(accent, 0.35);
        var empty = Theme.Brush(ink, 0.1);
        var border = new Pen(Theme.Brush(ink, 0.22), 1);
        border.Freeze();

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                Brush fill = selection?.Contains(col, row) == true ? chosen
                    : dragStart == null && hover is { } h && h.Col == col && h.Row == row ? hovered
                    : empty;
                var r = CellRect(col, row);
                r.Inflate(-0.5, -0.5);
                if (r.Width > 0 && r.Height > 0) dc.DrawRoundedRectangle(fill, border, r, 4, 4);
            }
        }

        // Dashed outline showing where the window is now.
        if (currentWindow is { } w)
        {
            var r = new Rect(w.X * ActualWidth, w.Y * ActualHeight, w.Width * ActualWidth, w.Height * ActualHeight);
            var bounds = new Rect(1, 1, Math.Max(0, ActualWidth - 2), Math.Max(0, ActualHeight - 2));
            r.Intersect(bounds);
            if (!r.IsEmpty && r.Width > 4 && r.Height > 4)
            {
                var dashed = new Pen(Theme.Brush(ink, 0.55), 2) { DashStyle = new DashStyle([2.5, 2], 0) };
                dashed.Freeze();
                dc.DrawRoundedRectangle(null, dashed, r, 5, 5);
            }
        }
    }

    // Mouse

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var c = CellAt(e.GetPosition(this));
        dragStart = c;
        selection = new GridSelection(c.Col, c.Row, c.Col, c.Row);
        CaptureMouse();
        InvalidateVisual();
        SelectionChanged?.Invoke(selection);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var c = CellAt(e.GetPosition(this));
        if (dragStart is { } start)
        {
            var updated = new GridSelection(start.Col, start.Row, c.Col, c.Row);
            if (updated != selection)
            {
                selection = updated;
                InvalidateVisual();
                SelectionChanged?.Invoke(updated);
            }
        }
        else if (hover != c)
        {
            hover = c;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (dragStart == null || selection is not { } chosen) return;
        dragStart = null;
        ReleaseMouseCapture();
        SelectionChanged?.Invoke(null);
        e.Handled = true;
        Picked?.Invoke(chosen);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        // The drag was interrupted (for example by a window popping up): forget it.
        if (dragStart == null) return;
        dragStart = null;
        selection = null;
        InvalidateVisual();
        SelectionChanged?.Invoke(null);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        hover = null;
        InvalidateVisual();
    }
}

/// <summary>Small drawing of the screen with a layout's area painted (in the list of layouts).</summary>
public sealed class AreaPreview : FrameworkElement
{
    private RectD unit;

    public RectD Unit { get => unit; set { unit = value; InvalidateVisual(); } }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        var outline = new Pen(Theme.Brush(Colors.Black, 0.45), 1);
        outline.Freeze();
        dc.DrawRoundedRectangle(Theme.Brush(Colors.Black, 0.04), outline, new Rect(0.5, 0.5, w - 1, h - 1), 3, 3);
        var area = new Rect(2 + (w - 4) * unit.X, 2 + (h - 4) * unit.Y,
                            Math.Max(2, (w - 4) * unit.Width), Math.Max(2, (h - 4) * unit.Height));
        dc.DrawRoundedRectangle(Theme.Brush(Theme.Accent), null, area, 1.5, 1.5);
    }
}
