using Xunit;

namespace Quadra.Tests;

// Ported from the Mac app's GeometryTests. Windows measures from the top left with y growing down,
// so areas at the top have small y here (on the Mac they have large y).
public class GeometryTests
{
    /// <summary>A usable area like a display without the taskbar.</summary>
    private static readonly RectD Visible = new(0, 0, 1500, 900);

    private static RectD Selection((int, int) a, (int, int) b, int cols, int rows) =>
        new GridSelection(a.Item1, a.Item2, b.Item1, b.Item2).UnitRect(cols, rows);

    // Grid areas

    [Fact]
    public void HalvesOnSixBySixGrid()
    {
        Assert.Equal(new RectD(0, 0, 750, 900), WindowGeometry.Frame(Selection((0, 0), (2, 5), 6, 6), Visible, 0));
        // Dragging from right to left gives the same area as left to right: the top right quarter.
        Assert.Equal(new RectD(750, 0, 750, 450), WindowGeometry.Frame(Selection((5, 0), (3, 2), 6, 6), Visible, 0));
    }

    [Fact]
    public void OddGrids()
    {
        Assert.Equal(new RectD(500, 0, 500, 900), WindowGeometry.Frame(Selection((1, 0), (1, 1), 3, 2), Visible, 0));
    }

    [Fact]
    public void GapIsFullAtEdgesAndHalfBetweenWindows()
    {
        Assert.Equal(new RectD(8, 8, 1484, 884), WindowGeometry.Frame(Selection((0, 0), (4, 2), 5, 3), Visible, 8));
        Assert.Equal(new RectD(754, 8, 738, 884), WindowGeometry.Frame(new RectD(0.5, 0, 0.5, 1), Visible, 8));
        Assert.Equal(new RectD(1290, 8, 202, 884), WindowGeometry.Frame(Selection((6, 0), (6, 6), 7, 7), Visible, 8));
        // Bottom half: full gap at the bottom edge, half a gap against the window above.
        Assert.Equal(new RectD(8, 454, 1484, 438), WindowGeometry.Frame(new RectD(0, 0.5, 1, 0.5), Visible, 8));
    }

    [Fact]
    public void AreaOnDisplayWithOffsetOrigin()
    {
        // A display to the left of the primary one has negative x.
        var left = new RectD(-1512, 0, 1512, 944);
        Assert.Equal(new RectD(-756, 0, 756, 944), WindowGeometry.Frame(new RectD(0.5, 0, 0.5, 1), left, 0));
        // A display above the primary one has negative y; the taskbar may leave the work area offset.
        var above = new RectD(0, -1080, 1920, 1032);
        Assert.Equal(new RectD(0, -564, 1920, 516), WindowGeometry.Frame(new RectD(0, 0.5, 1, 0.5), above, 0));
    }

    [Fact]
    public void WindowPositionAsFractionsOfTheDisplay()
    {
        var area = new RectD(100, 50, 1000, 500);
        Assert.Equal(new RectD(0.5, 0, 0.5, 1), WindowGeometry.UnitRect(new RectD(600, 50, 500, 500), area));
    }

    [Fact]
    public void LengthsScaleWithTheDisplayDpi()
    {
        Assert.Equal(8, WindowGeometry.Scale(8, 96));
        Assert.Equal(12, WindowGeometry.Scale(8, 144));
        Assert.Equal(10, WindowGeometry.Scale(8, 120));
    }

    // Displays

    [Fact]
    public void DisplayWithLargestOverlap()
    {
        var ultrawide = new RectD(0, 0, 3840, 1080);
        var laptop = new RectD(-1512, 0, 1512, 982);
        RectD[] frames = [ultrawide, laptop];
        // Mostly on the laptop, a little on the ultrawide.
        Assert.Equal(1, WindowGeometry.IndexOfLargestOverlap(new RectD(-900, 100, 1000, 600), frames));
        Assert.Equal(0, WindowGeometry.IndexOfLargestOverlap(new RectD(2000, 100, 500, 500), frames));
        Assert.Null(WindowGeometry.IndexOfLargestOverlap(new RectD(9000, 9000, 10, 10), frames));
    }

    [Fact]
    public void DisplayOrderIsLeftToRightThenTopToBottom()
    {
        var left = new RectD(-1512, 0, 1512, 982);
        var main = new RectD(0, 0, 3840, 1080);
        var above = new RectD(0, -1080, 1920, 1080);
        Assert.True(WindowGeometry.IsOrderedBefore(left, main));
        Assert.True(WindowGeometry.IsOrderedBefore(above, main));
        Assert.False(WindowGeometry.IsOrderedBefore(main, left));
    }

    [Fact]
    public void MovingToAnotherDisplayKeepsRelativePlace()
    {
        var ultrawide = new RectD(0, 0, 3840, 1055);
        var laptop = new RectD(-1512, 0, 1512, 944);
        // Left half of the ultrawide becomes the left half of the laptop.
        var leftHalf = new RectD(0, 0, 1920, 1055);
        Assert.Equal(new RectD(-1512, 0, 756, 944), WindowGeometry.Map(leftHalf, ultrawide, laptop));
        // And back.
        Assert.Equal(leftHalf, WindowGeometry.Map(new RectD(-1512, 0, 756, 944), laptop, ultrawide));
    }

    [Fact]
    public void MovingToAnotherDisplayStaysInside()
    {
        var source = new RectD(0, 0, 1000, 1000);
        var dest = new RectD(2000, 0, 500, 500);
        // A window hanging off the edge of the source display.
        var frame = WindowGeometry.Map(new RectD(900, 900, 400, 400), source, dest);
        Assert.True(dest.Contains(frame), $"{frame} should be inside {dest}");
    }

    // Apps with a minimum size

    [Fact]
    public void WindowThatRefusesTheSizeIsCentered()
    {
        var target = new RectD(0, 0, 300, 900);
        // Wider than asked: it can't be centered on the target without leaving the screen,
        // so it sticks to the left edge.
        Assert.Equal(new RectD(0, 0, 500, 900), WindowGeometry.Centered(500, 900, target, Visible));

        var middle = new RectD(500, 300, 300, 300);
        Assert.Equal(new RectD(450, 250, 400, 400), WindowGeometry.Centered(400, 400, middle, Visible));
    }

    [Fact]
    public void WindowTallerThanTheScreenKeepsItsTitleBarVisible()
    {
        var frame = WindowGeometry.Centered(400, 1200, Visible, Visible);
        Assert.Equal(Visible.Top, frame.Top);
    }

    [Fact]
    public void RoundingMatchesTheMacApp()
    {
        // Halves go away from zero, not to the nearest even number.
        Assert.Equal(3, WindowGeometry.Round(2.5));
        Assert.Equal(-3, WindowGeometry.Round(-2.5));
    }
}
