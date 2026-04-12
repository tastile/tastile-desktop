using System.IO;
namespace TastileDesktop.Tests;

public sealed class TilesWindowContractsTests
{
    private static string ReadRepoFile(params string[] parts)
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        var path = Path.Combine([repoRoot, .. parts]);
        return File.ReadAllText(path);
    }

    [Fact]
    public void MainViewModelSource_OnTilesChanged_UsesTileListItemMapper()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "ViewModels", "MainViewModel.cs");

        Assert.Contains("TileListItemMapper.Map(t)", source);
        Assert.DoesNotContain("NextStartLabel = ResolveNextStartLabel(t.ProjectedNextStartAt)", source);
    }

    [Fact]
    public void MainWindowSource_OpensTilesWindow_WithSharedPollingService()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "MainWindow.xaml.cs");

        Assert.Contains("new TilesWindow(ViewModel.PollingService)", source);
    }

    [Fact]
    public void TilesWindowXaml_StartedSection_UsesStartedStatusGlyph()
    {
        var xaml = ReadRepoFile("src", "TastileDesktop", "Views", "TilesWindow.xaml");

        Assert.Contains("<StackPanel x:Name=\"StartedSection\" Spacing=\"12\">", xaml);
        Assert.Contains("Glyph=\"&#xE945;\" FontSize=\"16\" Foreground=\"{StaticResource PrimaryForegroundBrush}\"", xaml);
    }

    [Fact]
    public void TilesWindowXaml_BindsTargetDurationAndScheduledDisplay_FromTileListItemContract()
    {
        var xaml = ReadRepoFile("src", "TastileDesktop", "Views", "TilesWindow.xaml");

        Assert.Contains("Text=\"{Binding TargetDurationText}\"", xaml);
        Assert.Contains("Text=\"{Binding ScheduledTimeDisplay}\"", xaml);
    }

    [Fact]
    public void TilesWindowSource_EditWindowClose_RefreshesTiles()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Views", "TilesWindow.xaml.cs");

        Assert.Contains("createWindow.Closed += async (_, _) => await RefreshTilesAsync();", source);
    }
}
