using System;

namespace Quadra;

/// <summary>A rectangle of grid cells. Row 0 is at the top.</summary>
public readonly record struct GridSelection
{
    public int C0 { get; }
    public int R0 { get; }
    public int C1 { get; }
    public int R1 { get; }

    /// <summary>The cells between two corners, in any order (dragging left or right gives the same area).</summary>
    public GridSelection(int col0, int row0, int col1, int row1)
    {
        C0 = Math.Min(col0, col1);
        C1 = Math.Max(col0, col1);
        R0 = Math.Min(row0, row1);
        R1 = Math.Max(row0, row1);
    }

    public bool Contains(int col, int row) => col >= C0 && col <= C1 && row >= R0 && row <= R1;

    /// <summary>The selection as fractions of the screen (0 to 1, origin at the top left).</summary>
    public RectD UnitRect(int cols, int rows) =>
        new((double)C0 / cols, (double)R0 / rows, (double)(C1 - C0 + 1) / cols, (double)(R1 - R0 + 1) / rows);
}
