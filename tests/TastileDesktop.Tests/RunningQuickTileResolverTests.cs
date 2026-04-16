namespace TastileDesktop.Tests;

public sealed class RunningQuickTileResolverTests
{
    [Fact]
    public void MainViewModelSource_UsesExecutionViewForRunningQuickTiles()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "ViewModels", "MainViewModel.cs");

        Assert.Contains("RunningQuickTileResolver.Resolve(_allTiles, _executionView?.TilesInProgress)", source);
    }

    [Fact]
    public void MainViewModelSource_RefreshesRunningTilesWhenExecutionViewChanges()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "ViewModels", "MainViewModel.cs");

        Assert.Contains("OnPropertyChanged(nameof(RunningQuickTiles));", source);
        Assert.Contains("OnPropertyChanged(nameof(MainRunningTask));", source);
    }

    private static string ReadRepoFile(params string[] parts)
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        var path = Path.Combine([repoRoot, .. parts]);
        return File.ReadAllText(path);
    }
}
