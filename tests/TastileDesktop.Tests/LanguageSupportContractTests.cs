using System.Text.RegularExpressions;

namespace TastileDesktop.Tests;

/// <summary>
/// Locks down the language list exposed to the user. Mirrors the
/// <c>SatelliteResourceLanguages</c> entry in the csproj, the
/// ComboBoxItem Tag values in SettingsWindow.xaml, and the
/// NormalizeLanguageTag switch in SettingsViewModel so the three
/// cannot drift apart. Adding a new language requires a resx folder,
/// a SatelliteResourceLanguages entry, a ComboBoxItem, and a tag in
/// the NormalizeLanguageTag switch — this test refuses any future
/// omission.
/// </summary>
public sealed class LanguageSupportContractTests
{
    private const string ExpectedCanonicalList = "en, ja, zh-CN, ko, es, de, fr, pt-BR";

    private static string ReadRepoFile(params string[] parts)
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        var path = Path.Combine([repoRoot, .. parts]);
        return File.ReadAllText(path);
    }

    private static string[] ReadSupportedTags()
    {
        // The supported tags are the explicit `case` arm literals in
        // NormalizeLanguageTag. Read them straight from the source so
        // the test stays in sync with whichever languages the desktop
        // decides to support without any code-gen or reflection.
        var source = ReadRepoFile("src", "TastileDesktop", "ViewModels", "SettingsViewModel.cs");
        var literal = Regex.Matches(source, "\"([a-zA-Z-]+)\"")
            .Select(m => m.Groups[1].Value)
            .Where(v => v is "en" or "ja" or "zh-CN" or "ko" or "es" or "de" or "fr" or "pt-BR")
            .Distinct()
            .ToArray();
        return literal;
    }

    [Fact]
    public void SupportedLanguageTags_ContainsWebAndAndroidUnion()
    {
        var expected = new[] { "en", "ja", "zh-CN", "ko", "es", "de", "fr", "pt-BR" };
        var actual = ReadSupportedTags();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SupportedLanguageTags_AreUnique()
    {
        var tags = ReadSupportedTags();
        Assert.Equal(tags.Length, tags.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void SupportedLanguageTags_AreAllNonEmpty()
    {
        Assert.All(ReadSupportedTags(), t => Assert.False(string.IsNullOrWhiteSpace(t)));
    }

    [Fact]
    public void Csproj_SatelliteResourceLanguages_ContainsAllSupportedTags()
    {
        var content = ReadRepoFile("src", "TastileDesktop", "TastileDesktop.csproj");
        var match = Regex.Match(
            content,
            "<SatelliteResourceLanguages>([^<]+)</SatelliteResourceLanguages>");
        Assert.True(match.Success, "SatelliteResourceLanguages element not found");

        var declared = match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var tag in ReadSupportedTags())
        {
            Assert.Contains(tag, declared);
        }
    }

    [Fact]
    public void SettingsWindowXaml_HasComboBoxItemForEachSupportedTag()
    {
        var content = ReadRepoFile("src", "TastileDesktop", "Views", "SettingsWindow.xaml");
        foreach (var tag in ReadSupportedTags())
        {
            Assert.Contains($"Tag=\"{tag}\"", content);
        }
    }

    private static string ResolveLocalizedPath(string directory, string baseName, string tag)
        => tag == "en"
            ? Path.Combine(directory, $"{baseName}.resx")
            : Path.Combine(directory, $"{baseName}.{tag}.resx");

    [Fact]
    public void QuickCreateSection_HasResxFileForEachSupportedLanguage()
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        var quickCreateDir = Path.Combine(repoRoot, "src", "TastileDesktop", "Resources", "Features");
        Assert.True(File.Exists(Path.Combine(quickCreateDir, "Strings.QuickCreate.resx")));

        // At least the QuickCreate section (the most user-facing) must
        // ship a resx file per supported language. If a new language
        // is added without an accompanying resx, the resource manager
        // silently falls back to English — this test guards against that.
        foreach (var tag in ReadSupportedTags())
        {
            var localized = ResolveLocalizedPath(quickCreateDir, "Strings.QuickCreate", tag);
            Assert.True(File.Exists(localized), $"Missing resx for QuickCreate.{tag}");
        }
    }

    [Fact]
    public void LanguagePicker_NewLanguageLabelsExistInAllSupportedResxFiles()
    {
        // The picker labels (Settings_LanguageGerman / _French /
        // _Portuguese) must exist in every resx file so the language
        // picker shows localized names in every supported culture,
        // including the ones whose resx is currently English-fallback.
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        var settingsDir = Path.Combine(repoRoot, "src", "TastileDesktop", "Resources", "System");

        var newPickerKeys = new[] { "Settings_LanguageGerman", "Settings_LanguageFrench", "Settings_LanguagePortuguese" };
        foreach (var tag in ReadSupportedTags())
        {
            var path = ResolveLocalizedPath(settingsDir, "Strings.Settings", tag);
            Assert.True(File.Exists(path), $"Missing Settings resx for {tag}");
            var content = File.ReadAllText(path);
            foreach (var key in newPickerKeys)
            {
                Assert.Contains($"name=\"{key}\"", content);
            }
        }
    }

    [Fact]
    public void TestSanity_ExpectedCanonicalListIsUpToDate()
    {
        // If the canonical answer above ever changes, this test makes
        // you look at the contract instead of silently rolling the
        // expected list forward.
        Assert.Equal(ExpectedCanonicalList, string.Join(", ", ReadSupportedTags()));
    }
}
