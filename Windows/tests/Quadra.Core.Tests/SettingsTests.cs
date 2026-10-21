using System;
using System.IO;
using Xunit;

namespace Quadra.Tests;

[Collection("Language")]
public class SettingsTests
{
    [Fact]
    public void SavedSettingsRoundTrip()
    {
        var s = new AppSettings { Columns = 8, Rows = 3, Gap = 4, PanelOpacity = 0.45, PanelSize = 500, PanelShape = PanelShape.Screen };
        s.NextScreenHotKey = null; // the user removed this shortcut
        s.Layouts[0].Name = "Código";
        var decoded = AppSettings.FromJson(s.ToJson());
        Assert.Equal(s.ToJson(), decoded.ToJson());
        Assert.Null(decoded.NextScreenHotKey);
        Assert.Equal(PanelShape.Screen, decoded.PanelShape);
        Assert.Equal(s.Layouts[0].Id, decoded.Layouts[0].Id);
        Assert.Equal("Código", decoded.Layouts[0].Name);
    }

    [Fact]
    public void MissingShortcutsGetTheirDefaults()
    {
        // Saved before the display shortcuts existed: only layouts and the grid shortcut.
        var decoded = AppSettings.FromJson("""{"layouts": [], "showGridHotKey": {"key": 71, "modifiers": 3}}""");
        Assert.Equal(HotKeyCombo.ShowGridDefault, decoded.ShowGridHotKey);
        Assert.Equal(HotKeyCombo.NextScreenDefault, decoded.NextScreenHotKey);
        Assert.Equal(HotKeyCombo.PreviousScreenDefault, decoded.PreviousScreenHotKey);
        Assert.Empty(decoded.Layouts); // the user deleted every layout
    }

    [Fact]
    public void AFileWithoutLayoutsGetsTheDefaultLayouts()
    {
        var decoded = AppSettings.FromJson("{}");
        Assert.Equal(6, decoded.Layouts.Count);
        Assert.Equal(6, decoded.Columns);
        Assert.Equal(PanelShape.FourThree, decoded.PanelShape);
        Assert.False(decoded.Welcomed);
    }

    [Fact]
    public void ValuesOutOfRangeAreFixed()
    {
        var decoded = AppSettings.FromJson("""
            {"columns": 99, "rows": 0, "gap": -5, "panelOpacity": 7, "panelSize": 10, "panelShape": "hexagon",
             "layouts": [{"name": "ok", "cols": 2, "rows": 1, "c0": 1, "r0": 0, "c1": 1, "r1": 0}, 42, null]}
            """);
        Assert.Equal(20, decoded.Columns);
        Assert.Equal(1, decoded.Rows);
        Assert.Equal(0, decoded.Gap);
        Assert.Equal(1, decoded.PanelOpacity);
        Assert.Equal(AppSettings.MinPanelSize, decoded.PanelSize);
        Assert.Equal(PanelShape.FourThree, decoded.PanelShape);
        var layout = Assert.Single(decoded.Layouts); // the broken entries are skipped
        Assert.Equal("ok", layout.Name);
        Assert.Null(layout.HotKey);
    }

    [Fact]
    public void PanelGridSizeFollowsTheShape()
    {
        var s = new AppSettings { PanelSize = 400, PanelShape = PanelShape.SixteenNine };
        Assert.Equal((400.0, 225.0), s.PanelGridSize(1920, 1080));
        s.PanelShape = PanelShape.Square;
        Assert.Equal((400.0, 400.0), s.PanelGridSize(1920, 1080));
        // "Like the display" on an ultrawide gets flat, but never below 120.
        s.PanelShape = PanelShape.Screen;
        Assert.Equal((400.0, 120.0), s.PanelGridSize(5120, 1440));
        s.PanelSize = 720;
        s.PanelShape = PanelShape.Screen;
        Assert.Equal((720.0, 203.0), s.PanelGridSize(5120, 1440));
    }

    [Fact]
    public void TheStoreSavesToItsFileAndReadsItBack()
    {
        string dir = Path.Combine(Path.GetTempPath(), "QuadraTests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "settings.json");
        try
        {
            var store = new SettingsStore(path);
            store.Edit(s => { s.Columns = 12; s.ShowGridHotKey = null; });
            Assert.True(File.Exists(path));
            var again = new SettingsStore(path);
            Assert.Equal(12, again.Current.Columns);
            Assert.Null(again.Current.ShowGridHotKey);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ABrokenFileIsKeptAsideAndTheDefaultsAreUsed()
    {
        string dir = Path.Combine(Path.GetTempPath(), "QuadraTests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "settings.json");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, "{ not json");
            var store = new SettingsStore(path);
            Assert.Equal(6, store.Current.Columns);
            Assert.True(File.Exists(path + ".broken"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
