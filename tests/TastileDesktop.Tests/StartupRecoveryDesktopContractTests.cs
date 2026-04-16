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
    public void MainViewModelSource_AutoExecutesOnlyFixedSchedulePrompts()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "ViewModels", "MainViewModel.cs");

        Assert.Contains("PromptAutoActionPolicy.Resolve(", source);
        Assert.Contains("PromptAutoExecutionDelay", source);
        Assert.Contains("autoActionId", source);
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

        Assert.Contains("await ExecutePromptActionAsync(id, prompt, stopAt, settings.Current.DefaultBreakMinutes);", source);
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
        Assert.Contains("catch (COMException ex)", source);
        Assert.Contains("[TimelineWindow] ChangeView failed in wheel zoom", source);
    }

    [Fact]
    public void AppSource_TriggersStartupTickAfterMainWindowInitialization()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "App.xaml.cs");

        Assert.Contains("await apiClient.TriggerTickAsync()", source);
    }

    [Fact]
    public void PromptToastWindowSource_DoesNotOpenNestedStopAtDialog()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Services", "PromptToastWindow.cs");

        Assert.DoesNotContain("PromptStopAtAsync(", source);
        Assert.DoesNotContain("ContentDialog", source);
        Assert.DoesNotContain("id == \"CONFIRM_STOP_AT\" ? DateTimeOffset.Now", source);
    }

    [Fact]
    public void PromptActionDispatcherSource_UsesStartupPromptGuard()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Services", "PromptActionDispatcher.cs");

        Assert.Contains("if (IsStartupRecoveryPrompt(prompt) && IsStartupRecoveryAction(id))", source);
    }

    [Fact]
    public void PromptActionDispatcherSource_MapsSnakeCaseActionAliases()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Services", "PromptActionDispatcher.cs");

        Assert.Contains("\"EXTEND\" or \"EXTEND_PHASE\"", source);
        Assert.Contains("\"DEFER\" or \"DEFER_TILE\"", source);
    }

    [Fact]
    public void PromptActionDispatcherSource_ReportsUnsupportedActions()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Services", "PromptActionDispatcher.cs");

        Assert.Contains("unsupported prompt action", source);
    }

}
