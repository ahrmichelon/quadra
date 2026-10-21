using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Quadra.Tests;

/// <summary>Tests that change <see cref="Loc.Culture"/> must not run at the same time as the others.</summary>
[CollectionDefinition("Language", DisableParallelization = true)]
public class LanguageCollection;

[Collection("Language")]
public class LocalizationTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Quadra.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Quadra.slnx not found");
    }

    private static Dictionary<string, string> ReadResx(string name) =>
        XDocument.Load(Path.Combine(RepoRoot(), "src", "Quadra.Core", name))
            .Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);

    [Fact]
    public void BothLanguagesHaveTheSameTexts()
    {
        var en = ReadResx("Strings.resx");
        var pt = ReadResx("Strings.pt-BR.resx");
        Assert.Empty(en.Keys.Except(pt.Keys));
        Assert.Empty(pt.Keys.Except(en.Keys));
        // A translation must keep the same {0} placeholders.
        foreach (var key in en.Keys)
        {
            var holes = (string s) => Regex.Matches(s, @"\{\d\}").Select(m => m.Value).Order().ToArray();
            Assert.True(holes(en[key]).SequenceEqual(holes(pt[key])), $"Placeholders differ in {key}");
        }
    }

    [Fact]
    public void EveryTextUsedInTheCodeExists()
    {
        var en = ReadResx("Strings.resx");
        var used = new HashSet<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file).StartsWith("._")) continue;
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"Loc\.(?:Get|Format)\(""([A-Za-z_]+)"""))
                used.Add(m.Groups[1].Value);
            // Keys passed around as plain strings, such as the default layout names.
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"""((?:Layout|Panel|Menu|Settings|HotKey|Warn|Shape|Key|Display|Tray|About|Welcome|Error)_[A-Za-z]+)"""))
                used.Add(m.Groups[1].Value);
        }
        Assert.NotEmpty(used);
        Assert.Empty(used.Except(en.Keys));
    }

    [Fact]
    public void TextsFollowTheLanguage()
    {
        try
        {
            Loc.Culture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("Left half", Loc.Get("Layout_LeftHalf"));
            Assert.Equal("Columns: 6", Loc.Format("Panel_Columns", 6));
            Assert.False(Loc.IsPortuguese);

            Loc.Culture = CultureInfo.GetCultureInfo("pt-BR");
            Assert.Equal("Metade esquerda", Loc.Get("Layout_LeftHalf"));
            Assert.Equal("Colunas: 6", Loc.Format("Panel_Columns", 6));
            Assert.Equal("Espaço", Keys.Name(Keys.Space));
            Assert.True(Loc.IsPortuguese);
            Assert.Equal("Metade esquerda", SavedLayout.Defaults()[0].Name);
        }
        finally
        {
            Loc.Culture = null;
        }
    }

    [Fact]
    public void AMissingTextShowsItsKeyInsteadOfCrashing()
    {
        Assert.Equal("No_Such_Text", Loc.Get("No_Such_Text"));
    }
}
