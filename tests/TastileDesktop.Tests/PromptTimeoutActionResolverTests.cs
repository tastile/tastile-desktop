using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class PromptTimeoutActionResolverTests
{
    [Fact]
    public void Resolve_ReturnsServerDefaultAction_WhenPresentInActions()
    {
        var prompt = BuildPrompt(
            defaultActionId: "COMPLETE_TILE",
            actions: [new PromptActionView("DEFER_TILE", "Defer"), new PromptActionView("COMPLETE_TILE", "Complete")]);

        var resolved = PromptTimeoutActionResolver.Resolve(prompt);

        Assert.Equal("COMPLETE_TILE", resolved);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenServerDefaultActionMissing()
    {
        var prompt = BuildPrompt(
            defaultActionId: "COMPLETE_TILE",
            actions: [new PromptActionView("DEFER_TILE", "Defer")]);

        var resolved = PromptTimeoutActionResolver.Resolve(prompt);

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenDefaultActionIdIsEmpty()
    {
        var prompt = BuildPrompt(
            defaultActionId: null,
            actions: [new PromptActionView("DISMISS", "Dismiss")]);

        var resolved = PromptTimeoutActionResolver.Resolve(prompt);

        Assert.Null(resolved);
    }

    private static PromptView BuildPrompt(string? defaultActionId, List<PromptActionView> actions)
        => new(
            PromptId: "prompt-1",
            Kind: "end",
            Severity: "critical",
            TileId: "tile-1",
            Title: "title",
            Body: "body",
            Why: "why",
            SuggestedMinutes: null,
            Actions: actions,
            DefaultActionId: defaultActionId,
            CreatedAt: "2026-04-16T07:00:00Z",
            ExpiresAt: "2026-04-16T07:00:30Z",
            Stale: false);
}
