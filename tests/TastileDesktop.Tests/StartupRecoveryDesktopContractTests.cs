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

    [Fact]
    public void MainViewModelSource_DeclaresThirtySecondPromptAutoExecution()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "ViewModels", "MainViewModel.cs");

        Assert.Contains("PromptAutoExecutionDelay", source);
        Assert.Contains("TimeSpan.FromSeconds(30)", source);
    }

    [Fact]
    public void MainViewModelSource_MapsAutoActions_ForStartAndEndPrompts()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "ViewModels", "MainViewModel.cs");

        Assert.Contains("ResolveAutoActionId", source);
        Assert.Contains("case \"start\":", source);
        Assert.Contains("case \"end\":", source);
        Assert.Contains("return \"START\";", source);
        Assert.Contains("return \"COMPLETE\";", source);
    }

    [Fact]
    public void AppSource_TriggersStartupTickAfterMainWindowInitialization()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "App.xaml.cs");

        Assert.Contains("await apiClient.TriggerTickAsync()", source);
    }
}
