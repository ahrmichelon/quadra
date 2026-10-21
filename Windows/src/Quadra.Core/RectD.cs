using System;

namespace Quadra;

/// <summary>
/// A rectangle with the origin at the top left and y growing down, like Windows screen coordinates.
/// Used both for screen areas in physical pixels and for areas as fractions of the screen (0 to 1).
/// </summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static RectD FromEdges(double left, double top, double right, double bottom) =>
        new(left, top, right - left, bottom - top);

    /// <summary>The overlapping part, or an empty rectangle when they don't touch.</summary>
    public RectD Intersect(RectD other)
    {
        double left = Math.Max(Left, other.Left), top = Math.Max(Top, other.Top);
        double right = Math.Min(Right, other.Right), bottom = Math.Min(Bottom, other.Bottom);
        return right > left && bottom > top ? FromEdges(left, top, right, bottom) : default;
    }

    public bool Contains(RectD other) =>
        other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;

    public bool Contains(double x, double y) => x >= Left && x < Right && y >= Top && y < Bottom;

    /// <summary>Grows the rectangle by <paramref name="by"/> on every side (shrinks when negative).</summary>
    public RectD Inflate(double by) => new(X - by, Y - by, Width + by * 2, Height + by * 2);

    public override string ToString() => $"({X}, {Y}, {Width} × {Height})";
}
