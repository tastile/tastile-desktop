using TastileDesktop.Services;
using TastileDesktop.Models;

namespace TastileDesktop.Tests;

public class PromptActionSelectionPolicyTests
{
    [Fact]
    public void TryResolveAction_ReturnsFalse_WhenActionIsNotInPrompt()
    {
        var prompt = BuildPrompt("START", "COMPLETE_TILE");

        var ok = PromptActionSelectionPolicy.TryResolveAction(prompt, "END_BREAK", out var resolvedActionId);

        Assert.False(ok);
        Assert.Null(resolvedActionId);
    }

    [Fact]
    public void TryResolveAction_ReturnsPromptAction_WithCanonicalCasing()
    {
        var prompt = BuildPrompt("Confirm_Stop_At", "DISMISS");

        var ok = PromptActionSelectionPolicy.TryResolveAction(prompt, "confirm_stop_at", out var resolvedActionId);

        Assert.True(ok);
        Assert.Equal("Confirm_Stop_At", resolvedActionId);
    }

    private static PromptView BuildPrompt(params string[] actions)
    {
        return new PromptView(
            PromptId: "prompt-1",
            Kind: "end",
            Severity: null,
            TileId: "tile-1",
            Title: "Title",
            Body: "Body",
            Why: string.Empty,
            SuggestedMinutes: null,
            Actions: actions.Select(id => new PromptActionView(id, id)).ToList(),
            CreatedAt: null,
            ExpiresAt: null,
            Stale: false);
    }
}
