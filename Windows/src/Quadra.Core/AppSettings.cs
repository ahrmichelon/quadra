using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Quadra;

/// <summary>Shape of the grid in the panel.</summary>
public enum PanelShape { Square, FourThree, ThreeTwo, SixteenNine, Screen }

public static class PanelShapes
{
    public static readonly PanelShape[] All =
        [PanelShape.Square, PanelShape.FourThree, PanelShape.ThreeTwo, PanelShape.SixteenNine, PanelShape.Screen];

    public static string Title(this PanelShape shape) => shape switch
    {
        PanelShape.Square => Loc.Get("Shape_Square"),
        PanelShape.FourThree => "4:3",
        PanelShape.ThreeTwo => "3:2",
        PanelShape.SixteenNine => "16:9",
        _ => Loc.Get("Shape_Display"),
    };

    /// <summary>Height divided by width (null = the display's own proportion).</summary>
    public static double? Ratio(this PanelShape shape) => shape switch
    {
        PanelShape.Square => 1,
        PanelShape.FourThree => 3.0 / 4.0,
        PanelShape.ThreeTwo => 2.0 / 3.0,
        PanelShape.SixteenNine => 9.0 / 16.0,
        _ => null,
    };

    /// <summary>The name saved in the settings file (the same as in the Mac app).</summary>
    public static string Key(this PanelShape shape) => shape switch
    {
        PanelShape.Square => "square",
        PanelShape.FourThree => "fourThree",
        PanelShape.ThreeTwo => "threeTwo",
        PanelShape.SixteenNine => "sixteenNine",
        _ => "screen",
    };

    public static PanelShape Parse(string? key) =>
        All.FirstOrDefault(s => s.Key() == key, PanelShape.FourThree);
}

/// <summary>Everything Quadra remembers between launches. Saved as JSON by <see cref="SettingsStore"/>.</summary>
public sealed class AppSettings
{
    /// <summary>Limits for columns and rows.</summary>
    public const int MinSize = 1, MaxSize = 20;
    public const double DefaultPanelOpacity = 0.6, MinPanelOpacity = 0.2;
    public const double DefaultPanelSize = 340, MinPanelSize = 240, MaxPanelSize = 720;
    /// <summary>Spaces offered between windows, in device-independent pixels.</summary>
    public static readonly int[] GapChoices = [0, 4, 8, 16];

    public int Columns { get; set; } = 6;
    public int Rows { get; set; } = 6;
    /// <summary>Space between windows and around the screen edges, in device-independent pixels.</summary>
    public int Gap { get; set; }
    /// <summary>Opacity of the panel background (0.2 to 1). Lower shows more of the apps behind it.</summary>
    public double PanelOpacity { get; set; } = DefaultPanelOpacity;
    /// <summary>Width of the grid in the panel, in device-independent pixels.</summary>
    public double PanelSize { get; set; } = DefaultPanelSize;
    public PanelShape PanelShape { get; set; } = PanelShape.FourThree;

    public List<SavedLayout> Layouts { get; set; } = SavedLayout.Defaults();
    public HotKeyCombo? ShowGridHotKey { get; set; } = HotKeyCombo.ShowGridDefault;
    public HotKeyCombo? NextScreenHotKey { get; set; } = HotKeyCombo.NextScreenDefault;
    public HotKeyCombo? PreviousScreenHotKey { get; set; } = HotKeyCombo.PreviousScreenDefault;

    /// <summary>Whether the first-launch tip was already shown.</summary>
    public bool Welcomed { get; set; }

    public static int ClampSize(int v) => Math.Clamp(v, MinSize, MaxSize);

    /// <summary>Keeps every value within its limits.</summary>
    public void Normalize()
    {
        Columns = ClampSize(Columns);
        Rows = ClampSize(Rows);
        Gap = Math.Clamp(Gap, 0, 64);
        PanelOpacity = double.IsFinite(PanelOpacity) ? Math.Clamp(PanelOpacity, MinPanelOpacity, 1) : DefaultPanelOpacity;
        PanelSize = double.IsFinite(PanelSize) ? Math.Clamp(PanelSize, MinPanelSize, MaxPanelSize) : DefaultPanelSize;
        if (!Enum.IsDefined(PanelShape)) PanelShape = PanelShape.FourThree;
        Layouts ??= [];
        foreach (var layout in Layouts) layout.Normalize();
    }

    /// <summary>
    /// Size of the grid in the panel (device-independent pixels), from the chosen width and shape.
    /// The screen size is only used by the "like the display" shape.
    /// </summary>
    public (double Width, double Height) PanelGridSize(double screenWidth, double screenHeight)
    {
        double width = PanelSize;
        double ratio = PanelShape.Ratio() ?? screenHeight / Math.Max(screenWidth, 1);
        double height = Math.Min(Math.Max(width * ratio, 120), 600);
        return (WindowGeometry.Round(width), WindowGeometry.Round(height));
    }

    // Saving and loading

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string ToJson()
    {
        var o = new JsonObject
        {
            ["columns"] = Columns,
            ["rows"] = Rows,
            ["gap"] = Gap,
            ["panelOpacity"] = PanelOpacity,
            ["panelSize"] = PanelSize,
            ["panelShape"] = PanelShape.Key(),
            ["layouts"] = JsonSerializer.SerializeToNode(Layouts, Json),
            ["showGridHotKey"] = HotKeyNode(ShowGridHotKey),
            ["nextScreenHotKey"] = HotKeyNode(NextScreenHotKey),
            ["previousScreenHotKey"] = HotKeyNode(PreviousScreenHotKey),
            ["welcomed"] = Welcomed,
        };
        return o.ToJsonString(Json);

        static JsonNode? HotKeyNode(HotKeyCombo? combo) => combo is { } c ? JsonSerializer.SerializeToNode(c, Json) : null;
    }

    /// <summary>
    /// Reads saved settings. Anything missing or invalid gets its default value. A shortcut missing
    /// from the file (saved by an older version) gets its default; a shortcut saved as null was
    /// removed by the user and stays off.
    /// </summary>
    public static AppSettings FromJson(string json)
    {
        var o = JsonNode.Parse(json) as JsonObject ?? throw new JsonException("The settings file is not a JSON object.");
        var s = new AppSettings();
        s.Columns = Int(o, "columns") ?? s.Columns;
        s.Rows = Int(o, "rows") ?? s.Rows;
        s.Gap = Int(o, "gap") ?? s.Gap;
        s.PanelOpacity = Double(o, "panelOpacity") ?? s.PanelOpacity;
        s.PanelSize = Double(o, "panelSize") ?? s.PanelSize;
        s.PanelShape = PanelShapes.Parse(String(o, "panelShape"));
        s.Welcomed = Bool(o, "welcomed") ?? false;

        if (o["layouts"] is JsonArray array)
        {
            s.Layouts = [];
            foreach (var item in array)
            {
                SavedLayout? layout = null;
                try { layout = item?.Deserialize<SavedLayout>(Json); }
                catch (JsonException) { } // skip a broken layout, keep the others
                if (layout != null) s.Layouts.Add(layout);
            }
        }

        s.ShowGridHotKey = HotKey(o, "showGridHotKey", HotKeyCombo.ShowGridDefault);
        s.NextScreenHotKey = HotKey(o, "nextScreenHotKey", HotKeyCombo.NextScreenDefault);
        s.PreviousScreenHotKey = HotKey(o, "previousScreenHotKey", HotKeyCombo.PreviousScreenDefault);
        s.Normalize();
        return s;
    }

    private static HotKeyCombo? HotKey(JsonObject o, string key, HotKeyCombo fallback)
    {
        if (!o.TryGetPropertyValue(key, out var node)) return fallback;
        if (node is null) return null;
        try { return node.Deserialize<HotKeyCombo>(Json); }
        catch (JsonException) { return fallback; }
    }

    private static JsonValue? Value(JsonObject o, string key) =>
        o.TryGetPropertyValue(key, out var node) ? node as JsonValue : null;

    private static int? Int(JsonObject o, string key) =>
        Value(o, key) is { } v && v.TryGetValue(out double d) && double.IsFinite(d) ? (int)Math.Round(d) : null;

    private static double? Double(JsonObject o, string key) =>
        Value(o, key) is { } v && v.TryGetValue(out double d) ? d : null;

    private static bool? Bool(JsonObject o, string key) =>
        Value(o, key) is { } v && v.TryGetValue(out bool b) ? b : null;

    private static string? String(JsonObject o, string key) =>
        Value(o, key) is { } v && v.TryGetValue(out string? s) ? s : null;
}
