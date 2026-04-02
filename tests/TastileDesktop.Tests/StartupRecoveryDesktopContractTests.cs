namespace TastileDesktop.Tests;

public sealed class StartupRecoveryDesktopContractTests
{
    private static string ReadRepoFile(params string[] parts)
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        var path = Path.Combine([repoRoot, .. parts]);
        return File.ReadAllText(path);
    }

    [Fact]
    public void MainViewModelSource_ClearsDismissReplayBlockAfterStartupDismiss()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "ViewModels", "MainViewModel.cs");

        Assert.Contains("if (_toastDismissedByAction && prompt.Prompt.PromptId == _lastHandledPromptId)", source);
        Assert.DoesNotContain("Skipping - already dismissed by action", source);
        Assert.Contains("_toastDismissedByAction = false;", source);
        Assert.Contains("_lastHandledPromptId = null;", source);
    }

    [Fact]
    public void TilesWindowSource_HandlesConfirmStopAtInStartupRecoverySwitch()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Views", "TilesWindow.xaml.cs");

        Assert.Contains("case \"CONFIRM_STOP_AT\":", source);
    }
}
