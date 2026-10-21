using System.Linq;
using Xunit;

namespace Quadra.Tests;

// Ported from the Mac app's LayoutTests.
[Collection("Language")]
public class LayoutTests
{
    private static readonly RectD Visible = new(0, 0, 1500, 900);

    [Fact]
    public void DefaultLayoutsCoverTheExpectedAreas()
    {
        var frames = SavedLayout.Defaults().Select(l => WindowGeometry.Frame(l.UnitRect, Visible, 0)).ToArray();
        Assert.Equal(
            [
                new RectD(0, 0, 750, 900),     // left half
                new RectD(750, 0, 750, 900),   // right half
                new RectD(0, 0, 1500, 450),    // top half
                new RectD(0, 450, 1500, 450),  // bottom half
                new RectD(0, 0, 1500, 900),    // full screen
                new RectD(250, 150, 1000, 600), // center
            ],
            frames);
    }

    [Fact]
    public void DefaultShortcutsMatchTheReadme()
    {
        var combos = SavedLayout.Defaults().Select(l => l.HotKey!.Value.Display).ToArray();
        Assert.Equal(["Ctrl+Alt+←", "Ctrl+Alt+→", "Ctrl+Alt+↑", "Ctrl+Alt+↓", "Ctrl+Alt+Enter", "Ctrl+Alt+C"], combos);
        Assert.Equal("Ctrl+Alt+G", HotKeyCombo.ShowGridDefault.Display);
        Assert.Equal("Ctrl+Alt+Shift+→", HotKeyCombo.NextScreenDefault.Display);
        Assert.Equal("Ctrl+Alt+Shift+←", HotKeyCombo.PreviousScreenDefault.Display);
    }

    [Fact]
    public void DefaultLayoutsHaveDifferentShortcuts()
    {
        var combos = SavedLayout.Defaults().Select(l => l.HotKey).ToList();
        Assert.Equal(combos.Count, combos.Distinct().Count());
    }

    [Fact]
    public void ResizingKeepsTheArea()
    {
        var left = SavedLayout.Defaults()[0];
        left.Resize(4, 2);
        Assert.Equal([0, 1, 0, 1], new[] { left.C0, left.C1, left.R0, left.R1 });

        var full = SavedLayout.Defaults()[4];
        full.Resize(20, 20);
        Assert.Equal([0, 19, 0, 19], new[] { full.C0, full.C1, full.R0, full.R1 });
    }

    [Fact]
    public void ResizingKeepsACenteredAreaCentered()
    {
        var center = SavedLayout.Defaults()[5];
        center.Resize(3, 3);
        Assert.Equal([1, 1, 1, 1], new[] { center.C0, center.C1, center.R0, center.R1 });
    }

    [Fact]
    public void ALayoutFromAHandEditedFileIsKeptInsideItsGrid()
    {
        var layout = new SavedLayout { Cols = 50, Rows = 0, C0 = 30, R0 = -2, C1 = 40, R1 = 3 };
        layout.Normalize();
        Assert.Equal((20, 1), (layout.Cols, layout.Rows));
        Assert.Equal(new GridSelection(19, 0, 19, 0), layout.Selection);
    }

    // Shortcuts

    [Fact]
    public void ShortcutText()
    {
        Assert.Equal("Ctrl+Shift+F5", new HotKeyCombo(Keys.F1 + 4, HotKeyCombo.Control | HotKeyCombo.Shift).Display);
        Assert.Equal("Win+Alt+Num 4", new HotKeyCombo(Keys.NumPad0 + 4, HotKeyCombo.Win | HotKeyCombo.Alt).Display);
        Assert.Equal("Ctrl+Alt+PgUp", new HotKeyCombo(Keys.PageUp, HotKeyCombo.Control | HotKeyCombo.Alt).Display);
    }

    [Fact]
    public void ShortcutsNeedAModifierOrAnFKey()
    {
        Assert.True(new HotKeyCombo(Keys.F1 + 4, 0).IsUsable);
        Assert.True(new HotKeyCombo(Keys.A, HotKeyCombo.Win).IsUsable);
        Assert.False(new HotKeyCombo(Keys.A, HotKeyCombo.Shift).IsUsable);
        Assert.False(new HotKeyCombo(Keys.A, 0).IsUsable);
    }

    [Fact]
    public void ModifierKeysAreNotShortcutKeys()
    {
        Assert.True(Keys.IsModifier(0x11));  // Ctrl
        Assert.True(Keys.IsModifier(0xA4));  // left Alt
        Assert.True(Keys.IsModifier(0x5B));  // left Win
        Assert.False(Keys.IsModifier(Keys.G));
    }

    [Fact]
    public void DuplicateShortcutsAreFlagged()
    {
        var store = new SettingsStore(null);
        var layouts = store.Current.Layouts;
        Assert.Null(store.Warning(layouts[4].HotKey));
        store.Edit(s => s.Layouts[1].HotKey = s.Layouts[0].HotKey);
        Assert.NotNull(store.Warning(layouts[0].HotKey));
        // The grid shortcut counts too.
        store.Edit(s => s.Layouts[2].HotKey = s.ShowGridHotKey);
        Assert.NotNull(store.Warning(store.Current.ShowGridHotKey));
    }

    [Fact]
    public void ShortcutsTakenByOtherAppsAreFlagged()
    {
        var store = new SettingsStore(null);
        int changes = 0;
        store.Changed += () => changes++;
        store.SetUnavailable([HotKeyCombo.ShowGridDefault]);
        Assert.Equal(1, changes);
        Assert.NotNull(store.Warning(HotKeyCombo.ShowGridDefault));
        Assert.Null(store.Warning(HotKeyCombo.NextScreenDefault));
        // The same list again changes nothing.
        store.SetUnavailable([HotKeyCombo.ShowGridDefault]);
        Assert.Equal(1, changes);
    }
}
