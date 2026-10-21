using System;
using System.Collections.Generic;

namespace Quadra;

/// <summary>
/// Screen math, in Windows coordinates: origin at the top left of the primary display, y growing down,
/// physical pixels. Displays to the left of or above the primary one have negative coordinates.
/// </summary>
public static class WindowGeometry
{
    /// <summary>Rounds like Swift's rounded(): halves go away from zero (Math.Round alone rounds to even).</summary>
    public static double Round(double v) => Math.Round(v, MidpointRounding.AwayFromZero);

    // Displays

    /// <summary>Displays go from left to right (top to bottom when stacked), the order shown in the panel.</summary>
    public static bool IsOrderedBefore(RectD a, RectD b) => a.Left != b.Left ? a.Left < b.Left : a.Top < b.Top;

    /// <summary>Index of the frame that holds most of <paramref name="rect"/>, or null when it touches none.</summary>
    public static int? IndexOfLargestOverlap(RectD rect, IReadOnlyList<RectD> frames)
    {
        int? best = null;
        double bestArea = 0;
        for (int i = 0; i < frames.Count; i++)
        {
            var overlap = frames[i].Intersect(rect);
            double area = overlap.Width * overlap.Height;
            if (area > bestArea)
            {
                best = i;
                bestArea = area;
            }
        }
        return best;
    }

    // Frames

    /// <summary>
    /// The screen area for an area given as fractions of the screen (0 to 1, origin at the top left).
    /// <paramref name="area"/> is the usable area of the display (without the taskbar).
    /// The gap is full at the screen edges and half between neighbouring windows.
    /// </summary>
    public static RectD Frame(RectD unit, RectD area, double gap)
    {
        double left = area.Left + area.Width * unit.Left;
        double right = area.Left + area.Width * unit.Right;
        double top = area.Top + area.Height * unit.Top;
        double bottom = area.Top + area.Height * unit.Bottom;
        if (gap > 0)
        {
            static bool AtEdge(double v) => Math.Abs(v) < 0.001;
            left += AtEdge(unit.Left) ? gap : gap / 2;
            right -= AtEdge(unit.Right - 1) ? gap : gap / 2;
            top += AtEdge(unit.Top) ? gap : gap / 2;
            bottom -= AtEdge(unit.Bottom - 1) ? gap : gap / 2;
        }
        return RectD.FromEdges(Round(left), Round(top), Round(right), Round(bottom));
    }

    /// <summary>Where a window is on a display, as fractions of the usable area (for the outline on the grid).</summary>
    public static RectD UnitRect(RectD frame, RectD area) =>
        new((frame.Left - area.Left) / area.Width, (frame.Top - area.Top) / area.Height,
            frame.Width / area.Width, frame.Height / area.Height);

    /// <summary>
    /// Moves a window frame from one display to another, keeping its relative position and size
    /// (a window on the left half stays on the left half).
    /// </summary>
    public static RectD Map(RectD frame, RectD source, RectD dest)
    {
        double sx = dest.Width / Math.Max(source.Width, 1);
        double sy = dest.Height / Math.Max(source.Height, 1);
        double width = Math.Min(frame.Width * sx, dest.Width);
        double height = Math.Min(frame.Height * sy, dest.Height);
        double x = dest.Left + (frame.Left - source.Left) * sx;
        double y = dest.Top + (frame.Top - source.Top) * sy;
        x = Math.Min(Math.Max(x, dest.Left), dest.Right - width);
        y = Math.Min(Math.Max(y, dest.Top), dest.Bottom - height);
        return new RectD(Round(x), Round(y), Round(width), Round(height));
    }

    /// <summary>
    /// Centers a window of the given size on <paramref name="target"/>, keeping it inside
    /// <paramref name="area"/> when it fits. Used when an app refuses the requested size
    /// (minimum or fixed size).
    /// </summary>
    public static RectD Centered(double width, double height, RectD target, RectD area)
    {
        double x = target.CenterX - width / 2;
        double y = target.CenterY - height / 2;
        x = Math.Max(Math.Min(x, area.Right - width), area.Left);
        // Too tall for the screen: keep the title bar visible at the top.
        y = height > area.Height ? area.Top : Math.Max(Math.Min(y, area.Bottom - height), area.Top);
        return new RectD(Round(x), Round(y), width, height);
    }

    /// <summary>Converts a length in device-independent pixels (96 per inch) to physical pixels.</summary>
    public static double Scale(double dips, double dpi) => Round(dips * dpi / 96.0);
}
