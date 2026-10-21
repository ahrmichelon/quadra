using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
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
             {"name": "Quadra-x64.exe", "browser_download_url": "https://github.com/ahrmichelon/quadra/releases/download/windows-v1.1.0/Quadra-x64.exe",
              "digest": "sha256:062ECE52D51F0B3A5357D86D7C6F254C3EE170CDF4D5A5751D774949FE2BF113"},
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
        // GitHub's checksum, when there is one.
        Assert.Equal("062ece52d51f0b3a5357d86d7c6f254c3ee170cdf4d5a5751d774949fe2bf113", release.ExeFor("x64")!.Sha256);
        Assert.Null(release.ExeFor("arm64")!.Sha256);
    }

    /// <summary>Answers every request with the same bytes, like GitHub serving a file.</summary>
    private sealed class FakeServer(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
    }

    private static async Task<(bool Saved, Exception? Error)> Download(byte[] served, string? sha256)
    {
        string path = Path.Combine(Path.GetTempPath(), $"QuadraTest-{Guid.NewGuid():N}.exe");
        try
        {
            using var http = new HttpClient(new FakeServer(served));
            var error = await Record.ExceptionAsync(() => Updates.DownloadAsync(http, new Asset("https://example.com/Quadra-x64.exe", sha256), path));
            Assert.False(File.Exists(path + ".part"), "the partial download must be cleaned up");
            return (File.Exists(path), error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ADownloadIsSavedOnlyWhenItMatchesGitHubsChecksum()
    {
        byte[] program = [(byte)'M', (byte)'Z', 1, 2, 3, 4];
        string good = Convert.ToHexStringLower(SHA256.HashData(program));

        var ok = await Download(program, good);
        Assert.True(ok.Saved);
        Assert.Null(ok.Error);

        var withoutChecksum = await Download(program, null);
        Assert.True(withoutChecksum.Saved);

        var tampered = await Download([(byte)'M', (byte)'Z', 9, 9, 9], good);
        Assert.False(tampered.Saved);
        Assert.IsType<InvalidDataException>(tampered.Error);

        var notAProgram = await Download("<html>Not found</html>"u8.ToArray(), null);
        Assert.False(notAProgram.Saved);
        Assert.IsType<InvalidDataException>(notAProgram.Error);
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
