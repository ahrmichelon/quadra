import XCTest
@testable import Quadra

final class GeometryTests: XCTestCase {
    /// A usable area like a display without menu bar and Dock (AppKit coordinates).
    let visible = CGRect(x: 0, y: 0, width: 1500, height: 900)

    private func selection(_ a: (Int, Int), _ b: (Int, Int), _ cols: Int, _ rows: Int) -> CGRect {
        GridSelection(from: a, to: b).unitRect(cols: cols, rows: rows)
    }

    // MARK: Grid areas

    func testHalvesOnSixBySixGrid() {
        XCTAssertEqual(Geometry.frame(for: selection((0, 0), (2, 5), 6, 6), in: visible, gap: 0),
                       CGRect(x: 0, y: 0, width: 750, height: 900))
        // Dragging from right to left gives the same area as left to right.
        XCTAssertEqual(Geometry.frame(for: selection((5, 0), (3, 2), 6, 6), in: visible, gap: 0),
                       CGRect(x: 750, y: 450, width: 750, height: 450))
    }

    func testOddGrids() {
        XCTAssertEqual(Geometry.frame(for: selection((1, 0), (1, 1), 3, 2), in: visible, gap: 0),
                       CGRect(x: 500, y: 0, width: 500, height: 900))
    }

    func testGapIsFullAtEdgesAndHalfBetweenWindows() {
        XCTAssertEqual(Geometry.frame(for: selection((0, 0), (4, 2), 5, 3), in: visible, gap: 8),
                       CGRect(x: 8, y: 8, width: 1484, height: 884))
        XCTAssertEqual(Geometry.frame(for: CGRect(x: 0.5, y: 0, width: 0.5, height: 1), in: visible, gap: 8),
                       CGRect(x: 754, y: 8, width: 738, height: 884))
        XCTAssertEqual(Geometry.frame(for: selection((6, 0), (6, 6), 7, 7), in: visible, gap: 8),
                       CGRect(x: 1290, y: 8, width: 202, height: 884))
    }

    func testAreaOnDisplayWithOffsetOrigin() {
        // A display to the left of the primary one has negative x.
        let left = CGRect(x: -1512, y: 0, width: 1512, height: 944)
        XCTAssertEqual(Geometry.frame(for: CGRect(x: 0.5, y: 0, width: 0.5, height: 1), in: left, gap: 0),
                       CGRect(x: -756, y: 0, width: 756, height: 944))
    }

    // MARK: Coordinate systems

    func testFlipBetweenAppKitAndAccessibility() {
        // Primary display 1080 points tall; a window at the top of it.
        let window = CGRect(x: 100, y: 780, width: 800, height: 300)
        XCTAssertEqual(Geometry.flip(window, primaryHeight: 1080), CGRect(x: 100, y: 0, width: 800, height: 300))
        // Converting twice gives the original back.
        XCTAssertEqual(Geometry.flip(Geometry.flip(window, primaryHeight: 1080), primaryHeight: 1080), window)
    }

    func testFlipOnDisplayAboveThePrimary() {
        // A display stacked above the primary one has negative y in Accessibility coordinates.
        let window = CGRect(x: 0, y: 1080, width: 500, height: 400)
        XCTAssertEqual(Geometry.flip(window, primaryHeight: 1080).minY, -400)
    }

    // MARK: Displays

    func testDisplayWithLargestOverlap() {
        let ultrawide = CGRect(x: 0, y: 0, width: 3840, height: 1080)
        let laptop = CGRect(x: -1512, y: 0, width: 1512, height: 982)
        let frames = [ultrawide, laptop]
        // Mostly on the laptop, a little on the ultrawide.
        XCTAssertEqual(Geometry.indexOfLargestOverlap(CGRect(x: -900, y: 100, width: 1000, height: 600), in: frames), 1)
        XCTAssertEqual(Geometry.indexOfLargestOverlap(CGRect(x: 2000, y: 100, width: 500, height: 500), in: frames), 0)
        XCTAssertNil(Geometry.indexOfLargestOverlap(CGRect(x: 9000, y: 9000, width: 10, height: 10), in: frames))
    }

    func testDisplayOrderIsLeftToRightThenTopToBottom() {
        let left = CGRect(x: -1512, y: 0, width: 1512, height: 982)
        let main = CGRect(x: 0, y: 0, width: 3840, height: 1080)
        let above = CGRect(x: 0, y: 1080, width: 1920, height: 1080)
        XCTAssertTrue(Geometry.isOrderedBefore(left, main))
        XCTAssertTrue(Geometry.isOrderedBefore(above, main))
        XCTAssertFalse(Geometry.isOrderedBefore(main, left))
    }

    func testMovingToAnotherDisplayKeepsRelativePlace() {
        let ultrawide = CGRect(x: 0, y: 0, width: 3840, height: 1055)
        let laptop = CGRect(x: -1512, y: 0, width: 1512, height: 944)
        // Left half of the ultrawide becomes the left half of the laptop.
        let leftHalf = CGRect(x: 0, y: 0, width: 1920, height: 1055)
        XCTAssertEqual(Geometry.map(leftHalf, from: ultrawide, to: laptop),
                       CGRect(x: -1512, y: 0, width: 756, height: 944))
        // And back.
        XCTAssertEqual(Geometry.map(CGRect(x: -1512, y: 0, width: 756, height: 944), from: laptop, to: ultrawide),
                       leftHalf)
    }

    func testMovingToAnotherDisplayStaysInside() {
        let source = CGRect(x: 0, y: 0, width: 1000, height: 1000)
        let dest = CGRect(x: 2000, y: 0, width: 500, height: 500)
        // A window hanging off the edge of the source display.
        let frame = Geometry.map(CGRect(x: 900, y: 900, width: 400, height: 400), from: source, to: dest)
        XCTAssertTrue(dest.contains(frame), "\(frame) should be inside \(dest)")
    }

    // MARK: Apps with a minimum size

    func testWindowThatRefusesTheSizeIsCentered() {
        let target = CGRect(x: 0, y: 0, width: 300, height: 900)
        let frame = Geometry.centered(CGSize(width: 500, height: 900), on: target, within: visible)
        // Wider than asked: it can't be centered on the target without leaving the screen,
        // so it sticks to the left edge.
        XCTAssertEqual(frame, CGRect(x: 0, y: 0, width: 500, height: 900))

        let middle = CGRect(x: 500, y: 300, width: 300, height: 300)
        XCTAssertEqual(Geometry.centered(CGSize(width: 400, height: 400), on: middle, within: visible),
                       CGRect(x: 450, y: 250, width: 400, height: 400))
    }

    func testWindowTallerThanTheScreenKeepsItsTitleBarVisible() {
        let frame = Geometry.centered(CGSize(width: 400, height: 1200), on: visible, within: visible)
        XCTAssertEqual(frame.maxY, visible.maxY)
    }
}
