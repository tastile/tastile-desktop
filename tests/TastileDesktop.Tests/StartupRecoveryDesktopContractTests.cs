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

        Assert.Contains("if (_toastDismissedByAction && promptFingerprint == _lastHandledPromptFingerprint)", source);
        Assert.DoesNotContain("Skipping - already dismissed by action", source);
        Assert.Contains("_toastDismissedByAction = false;", source);
        Assert.Contains("_lastHandledPromptFingerprint = null;", source);
    }

    [Fact]
    public void MainViewModelSource_DoesNotClientSideAutoExecutePromptActions()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "ViewModels", "MainViewModel.cs");

        Assert.DoesNotContain("PromptAutoExecutionDelay", source);
        Assert.DoesNotContain("StartPromptAutoExecution(", source);
        Assert.DoesNotContain("PromptAutoActionResolver.Resolve(prompt)", source);
    }

    [Fact]
    public void MainViewModelSource_DoesNotSpecialCaseBreakStateInPromptRouting()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "ViewModels", "MainViewModel.cs");

        Assert.DoesNotContain("ShouldSuppressPromptOnBreak(", source);
        Assert.DoesNotContain("PromptActionSafetyPolicy", source);
    }

    [Fact]
    public void MainViewModelSource_UsesUnifiedPromptActionExecutionPath()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "ViewModels", "MainViewModel.cs");

        Assert.Contains("await ExecutePromptActionAsync(id, prompt, stopAt);", source);
        Assert.DoesNotContain("CommandResponse? result = id switch", source);
    }

    [Fact]
    public void TilesWindowSource_UsesPromptActionDispatcher()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Views", "TilesWindow.xaml.cs");

        Assert.Contains("PromptActionDispatcher.ExecuteAsync(", source);
        Assert.DoesNotContain("switch (actionId.ToUpperInvariant())", source);
    }

    [Fact]
    public void TimelineWindowSource_UsesPromptActionDispatcher_AndNoSilentCatch()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs");

        Assert.Contains("PromptActionDispatcher.ExecuteAsync(", source);
        Assert.DoesNotContain("switch (actionId.ToUpperInvariant())", source);
        Assert.Contains("catch (Exception ex)", source);
        Assert.DoesNotContain("catch\r\n        {\r\n        }", source);
    }

    [Fact]
    public void AppSource_TriggersStartupTickAfterMainWindowInitialization()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "App.xaml.cs");

        Assert.Contains("await apiClient.TriggerTickAsync()", source);
    }
}
