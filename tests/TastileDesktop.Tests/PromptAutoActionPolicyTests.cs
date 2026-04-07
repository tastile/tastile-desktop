using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class PromptAutoActionPolicyTests
{
    [Fact]
    public void Resolve_ReturnsStartTile_ForFixedStartPrompt()
    {
        var prompt = BuildPrompt(kind: "start", actions: ["START_TILE", "DEFER_TILE"]);

        var action = PromptAutoActionPolicy.Resolve(prompt, isFixedScheduleTile: true);

        Assert.Equal("START_TILE", action);
    }

    [Fact]
    public void Resolve_ReturnsCompletePhase_ForFixedEndPrompt()
    {
        var prompt = BuildPrompt(kind: "end", actions: ["COMPLETE_PHASE", "DEFER_TILE"]);

        var action = PromptAutoActionPolicy.Resolve(prompt, isFixedScheduleTile: true);

        Assert.Equal("COMPLETE_PHASE", action);
    }

    [Fact]
    public void Resolve_ReturnsNull_ForNonFixedTile()
    {
        var prompt = BuildPrompt(kind: "start", actions: ["START_TILE", "DEFER_TILE"]);

        var action = PromptAutoActionPolicy.Resolve(prompt, isFixedScheduleTile: false);

        Assert.Null(action);
    }

    [Fact]
    public void Resolve_ReturnsNull_ForStartupRecoveryPrompt()
    {
        var prompt = BuildPrompt(kind: "start", actions: ["CONFIRM_EXECUTED", "CONFIRM_SKIPPED", "DISMISS"]);

        var action = PromptAutoActionPolicy.Resolve(prompt, isFixedScheduleTile: true);

        Assert.Null(action);
    }

    private static PromptView BuildPrompt(string kind, IReadOnlyList<string> actions)
    {
        return new PromptView(
            PromptId: "prompt-1",
            Kind: kind,
            Severity: "elevated",
            TileId: "tile-1",
            Title: "Prompt",
            Body: "Body",
            Why: "Reason",
            SuggestedMinutes: null,
            Actions: actions.Select(action => new PromptActionView(action, action)).ToList(),
            CreatedAt: "2026-04-07T10:00:00Z",
            ExpiresAt: null,
            Stale: false);
    }
}
