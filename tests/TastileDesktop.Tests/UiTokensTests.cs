using System.Xml.Linq;
using Xunit;

namespace TastileDesktop.Tests;

public class UiTokensTests
{
    // WPF / WinUI XAML uses the x: prefix for resource keys, which is bound to
    // the http://schemas.microsoft.com/winfx/2006/xaml namespace. A namespace-
    // aware lookup is required — e.Attribute("Key") returns null for every key.
    private static readonly XName KeyAttr = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");

    private static readonly string AppXamlPath =
        Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TastileDesktop", "App.xaml");

    [Fact]
    public void AppXaml_DefinesOverrideAccentFillBrush_BothThemes()
    {
        var doc = XDocument.Load(AppXamlPath);
        foreach (var theme in new[] { "Light", "Dark" })
        {
            var dictionary = doc.Descendants()
                .Single(e => e.Name.LocalName == "ResourceDictionary"
                    && (string?)e.Attribute(KeyAttr) == theme);
            var keys = dictionary.Elements()
                .Where(e => e.Name.LocalName == "SolidColorBrush")
                .Select(e => (string?)e.Attribute(KeyAttr) ?? "")
                .ToHashSet();

            Assert.Contains("OverrideAccentFillBrush", keys);
            Assert.Contains("OverrideAccentFillSecondaryBrush", keys);
        }
    }

    [Fact]
    public void AppXaml_RemovedBrushes_AreNotPresent()
    {
        var doc = XDocument.Load(AppXamlPath);
        var keys = doc.Descendants()
            .Where(e => e.Name.LocalName == "SolidColorBrush")
            .Select(e => (string?)e.Attribute(KeyAttr) ?? "")
            .ToHashSet();

        var removed = new[]
        {
            "AppBackgroundBrush", "AppSurface0Brush", "AppSurface1Brush",
            "AppSurface2Brush", "AppSurfaceElevatedBrush", "AppForegroundBrush",
            "AppForegroundMutedBrush", "AppForegroundSubtleBrush",
            "AppBorderBrush", "AppBorderStrongBrush", "AppPrimaryBrush",
            "AppPrimaryForegroundBrush", "AppPrimaryHoverBrush",
            "QuickPanelBackgroundBrush", "QuickPanelBorderBrush",
            "PrimaryForegroundBrush", "SecondaryForegroundBrush",
            "TertiaryForegroundBrush", "AccentBrush",
        };

        foreach (var key in removed)
            Assert.DoesNotContain(key, keys);
    }

    [Fact]
    public void AppXaml_DefinesLayoutTokens()
    {
        var doc = XDocument.Load(AppXamlPath);
        var tokens = doc.Descendants()
            .Select(e => (string?)e.Attribute(KeyAttr) ?? "")
            .Where(k => !string.IsNullOrEmpty(k))
            .ToHashSet();

        Assert.Contains("WindowTitleBarHeight", tokens);
        Assert.Contains("WindowCardCornerRadius", tokens);
        Assert.Contains("ControlSpacingTight", tokens);
        Assert.Contains("ControlSpacingNormal", tokens);
        Assert.Contains("ControlSpacingLoose", tokens);
        Assert.Contains("SettingRowHeight", tokens);
        Assert.Contains("InterventionScrimBrush", tokens);
        Assert.Contains("InterventionEmergencyBrush", tokens);
    }
}
