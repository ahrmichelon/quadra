import XCTest
@testable import Quadra

final class UpdateCheckerTests: XCTestCase {
    func testVersionsAreComparedNumberByNumber() {
        XCTAssertTrue(UpdateChecker.isNewer("1.1.0", than: "1.0.1"))
        XCTAssertTrue(UpdateChecker.isNewer("1.10.0", than: "1.9.2"))
        XCTAssertTrue(UpdateChecker.isNewer("2.0", than: "1.99.99"))
        XCTAssertTrue(UpdateChecker.isNewer("1.0.1", than: "1.0"))
        XCTAssertFalse(UpdateChecker.isNewer("1.0.0", than: "1.0"))
        XCTAssertFalse(UpdateChecker.isNewer("1.0.1", than: "1.0.1"))
        XCTAssertFalse(UpdateChecker.isNewer("1.0.9", than: "1.1.0"))
    }

    func testReadsGitHubsLatestReleaseAnswer() throws {
        let json = """
        {"tag_name": "v1.2.3", "html_url": "https://github.com/vulpini-br/quadra/releases/tag/v1.2.3",
         "name": "Quadra v1.2.3", "draft": false, "prerelease": false}
        """
        let release = try JSONDecoder().decode(UpdateChecker.Release.self, from: Data(json.utf8))
        XCTAssertEqual(release.version, "1.2.3")
        XCTAssertEqual(release.htmlURL.absoluteString, "https://github.com/vulpini-br/quadra/releases/tag/v1.2.3")
    }
}
