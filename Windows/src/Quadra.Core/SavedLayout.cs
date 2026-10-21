using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Quadra;

/// <summary>A saved screen area with its own shortcut (like Divvy's "shortcuts").</summary>
public sealed class SavedLayout
{
    [JsonPropertyName("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    /// <summary>The grid used to pick the area.</summary>
    [JsonPropertyName("cols")] public int Cols { get; set; } = 6;
    [JsonPropertyName("rows")] public int Rows { get; set; } = 6;

    /// <summary>The chosen cells (row 0 at the top).</summary>
    [JsonPropertyName("c0")] public int C0 { get; set; }
    [JsonPropertyName("r0")] public int R0 { get; set; }
    [JsonPropertyName("c1")] public int C1 { get; set; }
    [JsonPropertyName("r1")] public int R1 { get; set; }

    [JsonPropertyName("hotKey")] public HotKeyCombo? HotKey { get; set; }

    [JsonIgnore]
    public GridSelection Selection
    {
        get => new(C0, R0, C1, R1);
        set { C0 = value.C0; R0 = value.R0; C1 = value.C1; R1 = value.R1; }
    }

    /// <summary>The area as fractions of the screen (0 to 1, origin at the top left).</summary>
    [JsonIgnore]
    public RectD UnitRect => Selection.UnitRect(Cols, Rows);

    public SavedLayout Clone() => (SavedLayout)MemberwiseClone();

    /// <summary>Changes the grid size while keeping the area as close as possible.</summary>
    public void Resize(int newCols, int newRows)
    {
        var u = UnitRect;
        (C0, C1) = Cells(u.Left, u.Right, newCols);
        (R0, R1) = Cells(u.Top, u.Bottom, newRows);
        Cols = newCols;
        Rows = newRows;

        // Round to the nearest cell; on a tie, pull the edges inwards so a centered area stays centered.
        static (int, int) Cells(double start, double end, int count)
        {
            int a = Math.Min(Math.Max((int)Math.Floor(start * count + 0.5), 0), count - 1);
            int b = Math.Min(Math.Max((int)Math.Ceiling(end * count - 0.5) - 1, a), count - 1);
            return (a, b);
        }
    }

    /// <summary>Keeps the grid size and the cells within limits (a hand-edited file may have anything).</summary>
    public void Normalize()
    {
        Cols = AppSettings.ClampSize(Cols);
        Rows = AppSettings.ClampSize(Rows);
        Selection = new GridSelection(
            Math.Clamp(C0, 0, Cols - 1), Math.Clamp(R0, 0, Rows - 1),
            Math.Clamp(C1, 0, Cols - 1), Math.Clamp(R1, 0, Rows - 1));
        Name ??= "";
        if (HotKey is { } combo && (combo.Key <= 0 || combo.Key > 0xFE)) HotKey = null;
    }

    private static SavedLayout Preset(string nameKey, (int, int) grid, (int, int) from, (int, int) to, int key) =>
        new()
        {
            Name = Loc.Get(nameKey),
            Cols = grid.Item1, Rows = grid.Item2,
            C0 = from.Item1, R0 = from.Item2, C1 = to.Item1, R1 = to.Item2,
            HotKey = new HotKeyCombo(key, HotKeyCombo.Control | HotKeyCombo.Alt),
        };

    /// <summary>The layouts the app starts with.</summary>
    public static List<SavedLayout> Defaults() =>
    [
        Preset("Layout_LeftHalf", (2, 1), (0, 0), (0, 0), Keys.Left),
        Preset("Layout_RightHalf", (2, 1), (1, 0), (1, 0), Keys.Right),
        Preset("Layout_TopHalf", (1, 2), (0, 0), (0, 0), Keys.Up),
        Preset("Layout_BottomHalf", (1, 2), (0, 1), (0, 1), Keys.Down),
        Preset("Layout_FullScreen", (1, 1), (0, 0), (0, 0), Keys.Return),
        Preset("Layout_Center", (6, 6), (1, 1), (4, 4), Keys.C),
    ];
}
