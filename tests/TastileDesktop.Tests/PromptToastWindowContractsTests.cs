namespace TastileDesktop.Tests;

public sealed class PromptToastWindowContractsTests
{
    [Fact]
    public void ShowPrompt_OnlyConfiguresCountdownWhenTimeoutActionExists()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Services", "PromptToastWindow.cs");
        Assert.Contains("ConfigureCountdown(_timeoutActionId is null ? null : prompt.ExpiresAt);", source);
    }

    [Fact]
    public void ShowBackdrop_DoesNotDisplayAutoExecuteCountdown()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Services", "PromptToastWindow.cs");
        Assert.Contains("ConfigureCountdown(null);", source);
    }

    private static string ReadRepoFile(params string[] parts)
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        var path = Path.Combine([repoRoot, .. parts]);
        return File.ReadAllText(path);
    }
}
