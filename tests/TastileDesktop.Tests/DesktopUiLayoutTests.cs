using System.Text.RegularExpressions;

namespace TastileDesktop.Tests;

public sealed class DesktopUiLayoutTests
{
    [Fact]
    public void MainWindow_QuickButtons_KeepCreateTileAndUseSingleIntegrationsEntry()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\TastileDesktop\MainWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("x:Name=\"CreateTileButton\"", xaml);
        Assert.Contains("Click=\"OnOpenCreateTileWindowClick\"", xaml);
        Assert.DoesNotContain("x:Name=\"RefreshButton\"", xaml);
        Assert.Single(Regex.Matches(xaml, "Click=\"OnOpenIntegrationsWindowClick\"").Cast<Match>());
    }

    [Fact]
    public void SettingsWindow_Updates_DoesNotExposeManifestUrlField()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\TastileDesktop\Views\SettingsWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.DoesNotContain("Manifest URL", xaml);
        Assert.DoesNotContain("UpdateManifestUrl", xaml);
        Assert.Contains("Check for updates", xaml);
    }
}
