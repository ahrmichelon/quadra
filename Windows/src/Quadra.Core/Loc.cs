using System.Globalization;
using System.Resources;

namespace Quadra;

/// <summary>
/// Texts shown to people. English is in Strings.resx, Brazilian Portuguese in Strings.pt-BR.resx.
/// Windows picks the language from its display language.
/// </summary>
public static class Loc
{
    private static readonly ResourceManager Resources = new("Quadra.Strings", typeof(Loc).Assembly);

    /// <summary>The language to use; null follows Windows. Tests set it.</summary>
    public static CultureInfo? Culture { get; set; }

    /// <summary>The text for a key, or the key itself if it is missing (never crashes).</summary>
    public static string Get(string key) => Resources.GetString(key, Culture) ?? key;

    /// <summary>A text with values in it, such as "Columns: {0}".</summary>
    public static string Format(string key, params object[] args) =>
        string.Format(Culture ?? CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>True when the texts are in Portuguese (used to pick the Portuguese web pages).</summary>
    public static bool IsPortuguese => Get("Language_Code").StartsWith("pt", System.StringComparison.Ordinal);
}
