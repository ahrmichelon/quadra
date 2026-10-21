using System;
using Xunit;

namespace Quadra.Tests;

// The version comparison is ported from the Mac app's UpdateCheckerTests.
public class UpdateTests
{
    [Fact]
    public void VersionsAreComparedNumberByNumber()
    {
        Assert.True(Updates.IsNewer("1.1.0", "1.0.1"));
        Assert.True(Updates.IsNewer("1.10.0", "1.9.2"));
        Assert.True(Updates.IsNewer("2.0", "1.99.99"));
        Assert.True(Updates.IsNewer("1.0.1", "1.0"));
        Assert.False(Updates.IsNewer("1.0.0", "1.0"));
        Assert.False(Updates.IsNewer("1.0.1", "1.0.1"));
        Assert.False(Updates.IsNewer("1.0.9", "1.1.0"));
    }

    // GitHub's list of releases, newest first: Mac and Windows releases mixed, plus a draft and a pre-release.
    private const string Releases = """
        [
          {"tag_name": "windows-v1.3.0", "draft": true, "prerelease": false, "html_url": "https://example.com/draft", "assets": []},
          {"tag_name": "windows-v1.2.0", "draft": false, "prerelease": true, "html_url": "https://example.com/beta", "assets": []},
          {"tag_name": "v1.4.0", "draft": false, "prerelease": false,
           "html_url": "https://github.com/ahrmichelon/quadra/releases/tag/v1.4.0",
           "assets": [{"name": "Quadra.zip", "browser_download_url": "https://github.com/ahrmichelon/quadra/releases/download/v1.4.0/Quadra.zip"}]},
          {"tag_name": "windows-v1.1.0", "draft": false, "prerelease": false,
           "html_url": "https://github.com/ahrmichelon/quadra/releases/tag/windows-v1.1.0",
           "assets": [
             {"name": "Quadra-x64.exe", "browser_download_url": "https://github.com/ahrmichelon/quadra/releases/download/windows-v1.1.0/Quadra-x64.exe"},
             {"name": "Quadra-arm64.exe", "browser_download_url": "https://github.com/ahrmichelon/quadra/releases/download/windows-v1.1.0/Quadra-arm64.exe"}
           ]},
          {"tag_name": "windows-v1.0.0", "draft": false, "prerelease": false, "html_url": "https://example.com/1.0.0", "assets": []}
        ]
        """;

    [Fact]
    public void PicksTheNewestWindowsRelease()
    {
        var release = Updates.NewestWindowsRelease(Releases);
        Assert.NotNull(release);
        Assert.Equal("1.1.0", release.Version); // not the Mac's 1.4.0, the draft or the pre-release
        Assert.Equal("https://github.com/ahrmichelon/quadra/releases/tag/windows-v1.1.0", release.PageUrl);
        Assert.Equal("https://github.com/ahrmichelon/quadra/releases/download/windows-v1.1.0/Quadra-arm64.exe",
                     release.DownloadUrl("arm64"));
        // A processor without its own file gets the release page.
        Assert.Equal(release.PageUrl, release.DownloadUrl("x86"));
    }

    [Fact]
    public void NoWindowsReleaseYet()
    {
        Assert.Null(Updates.NewestWindowsRelease("""[{"tag_name": "v1.1.0", "draft": false, "prerelease": false}]"""));
        Assert.Null(Updates.NewestWindowsRelease("[]"));
        Assert.Null(Updates.NewestWindowsRelease("""{"message": "API rate limit exceeded"}"""));
    }

    [Fact]
    public void AutomaticChecksHappenOnceADay()
    {
        var now = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        Assert.True(Updates.IsDue(null, now));
        Assert.False(Updates.IsDue(now.AddHours(-23), now));
        Assert.True(Updates.IsDue(now.AddHours(-24), now));
        Assert.True(Updates.IsDue(now.AddDays(3), now)); // the clock went back
    }

    [Fact]
    public void UpdateSettingsAreSaved()
    {
        var s = new AppSettings
        {
            CheckForUpdates = false,
            LastUpdateCheck = new DateTimeOffset(2026, 10, 21, 9, 30, 0, TimeSpan.FromHours(-3)),
            AnnouncedVersion = "1.1.0",
        };
        var decoded = AppSettings.FromJson(s.ToJson());
        Assert.False(decoded.CheckForUpdates);
        Assert.Equal(s.LastUpdateCheck, decoded.LastUpdateCheck);
        Assert.Equal("1.1.0", decoded.AnnouncedVersion);

        // Settings saved by 1.0.0 have none of this: checks are on, nothing was announced.
        var old = AppSettings.FromJson("{}");
        Assert.True(old.CheckForUpdates);
        Assert.Null(old.LastUpdateCheck);
        Assert.Null(old.AnnouncedVersion);
    }
}
