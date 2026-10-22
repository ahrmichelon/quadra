using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Quadra;

/// <summary>A Quadra for Windows release on GitHub.</summary>
/// <param name="Version">Such as "1.1.0" (the tag without "windows-v").</param>
/// <param name="PageUrl">The release's page, with the notes and both downloads.</param>
/// <param name="Downloads">Each file, by file name (such as "Quadra-x64.exe").</param>
public sealed record Release(string Version, string PageUrl, IReadOnlyDictionary<string, Asset> Downloads);

/// <summary>A file of a release: where to download it and its SHA-256 as GitHub reports it (when it does).</summary>
public sealed record Asset(string Url, string? Sha256);

/// <summary>
/// Asks GitHub whether a newer Quadra for Windows exists. The Mac and Windows apps share one repository,
/// so this reads the list of releases and keeps only the Windows ones (tags such as windows-v1.1.0); the
/// repository's "latest" release is the Mac one.
/// </summary>
public static class Updates
{
    public const string ReleasesApi = "https://api.github.com/repos/vulpini-br/quadra/releases?per_page=30";
    public const string TagPrefix = "windows-v";

    /// <summary>Automatic checks happen at most this often.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    /// <summary>Whether an automatic check is due (also when the clock went back since the last one).</summary>
    public static bool IsDue(DateTimeOffset? lastCheck, DateTimeOffset now) =>
        lastCheck is not { } last || now - last >= Interval || last > now;

    /// <summary>Compares versions number by number, so "1.10.0" is newer than "1.9.2".</summary>
    public static bool IsNewer(string a, string than)
    {
        int[] x = Numbers(a), y = Numbers(than);
        for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            int l = i < x.Length ? x[i] : 0, r = i < y.Length ? y[i] : 0;
            if (l != r) return l > r;
        }
        return false;
    }

    private static int[] Numbers(string version) =>
        version.Split('.').Select(part =>
        {
            var digits = new string(part.TakeWhile(char.IsAsciiDigit).ToArray());
            return int.TryParse(digits, out int n) ? n : 0;
        }).ToArray();

    /// <summary>
    /// The newest Windows release in GitHub's list of releases (the JSON of the releases API), leaving out
    /// drafts, pre-releases and the Mac releases. Null when there is none.
    /// </summary>
    public static Release? NewestWindowsRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return null;
        Release? newest = null;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            string tag = Text(item, "tag_name");
            if (!tag.StartsWith(TagPrefix, StringComparison.Ordinal)) continue;
            if (Flag(item, "draft") || Flag(item, "prerelease")) continue;
            string version = tag[TagPrefix.Length..];
            if (version.Length == 0 || !char.IsAsciiDigit(version[0])) continue;

            var downloads = new Dictionary<string, Asset>(StringComparer.OrdinalIgnoreCase);
            if (item.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string name = Text(asset, "name"), url = Text(asset, "browser_download_url");
                    string digest = Text(asset, "digest");
                    string? sha256 = digest.StartsWith("sha256:", StringComparison.Ordinal) ? digest[7..].ToLowerInvariant() : null;
                    if (name.Length > 0 && url.StartsWith("https://", StringComparison.Ordinal))
                        downloads[name] = new Asset(url, sha256);
                }
            }
            var release = new Release(version, Text(item, "html_url"), downloads);
            if (newest == null || IsNewer(release.Version, newest.Version)) newest = release;
        }
        return newest;
    }

    /// <summary>
    /// Asks GitHub for the newest Windows release. Returns it when it is newer than
    /// <paramref name="currentVersion"/>, null when Quadra is up to date. Network problems throw.
    /// </summary>
    public static async Task<Release?> CheckAsync(HttpClient http, string currentVersion, CancellationToken cancel = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApi);
        request.Headers.Add("Accept", "application/vnd.github+json");
        request.Headers.Add("User-Agent", $"Quadra-Windows/{currentVersion}");
        using var response = await http.SendAsync(request, cancel).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
        var newest = NewestWindowsRelease(json);
        return newest != null && IsNewer(newest.Version, currentVersion) ? newest : null;
    }

    /// <summary>The Quadra.exe for this PC's processor ("x64" or "arm64"), if the release has one.</summary>
    public static Asset? ExeFor(this Release release, string architecture) =>
        release.Downloads.TryGetValue($"Quadra-{architecture}.exe", out var asset) ? asset : null;

    /// <summary>The download address for this PC's processor, or the release page.</summary>
    public static string DownloadUrl(this Release release, string architecture) =>
        release.ExeFor(architecture)?.Url ?? release.PageUrl;

    /// <summary>
    /// Downloads a release file to <paramref name="path"/>. The file must be a Windows program and, when GitHub
    /// reported its SHA-256, match it; otherwise nothing is saved and this throws.
    /// </summary>
    public static async Task DownloadAsync(HttpClient http, Asset asset, string path, CancellationToken cancel = default)
    {
        string partial = path + ".part";
        try
        {
            using (var response = await http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await using var file = System.IO.File.Create(partial);
                await response.Content.CopyToAsync(file, cancel).ConfigureAwait(false);
            }
            await using (var file = System.IO.File.OpenRead(partial))
            {
                var header = new byte[2];
                if (await file.ReadAsync(header, cancel).ConfigureAwait(false) != 2 || header[0] != 'M' || header[1] != 'Z')
                    throw new InvalidDataException("The download is not a Windows program.");
                if (asset.Sha256 != null)
                {
                    file.Position = 0;
                    string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancel).ConfigureAwait(false));
                    if (hash != asset.Sha256)
                        throw new InvalidDataException("The download doesn't match the file published on GitHub.");
                }
            }
            System.IO.File.Move(partial, path, overwrite: true);
        }
        finally
        {
            if (System.IO.File.Exists(partial)) System.IO.File.Delete(partial);
        }
    }

    private static string Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static bool Flag(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}
