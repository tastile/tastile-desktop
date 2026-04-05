using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class PromptAutoActionResolverTests
{
    [Fact]
    public void Resolve_ReturnsNull_ForNormalEndPromptWithBreakAction()
    {
        var prompt = new PromptView(
            PromptId: "p1",
            Kind: "end",
            Severity: null,
            TileId: "tile-1",
            Title: "Close task",
            Body: "",
            Why: "",
            SuggestedMinutes: null,
            Actions:
            [
                new PromptActionView("START_BREAK_PARALLEL", "Start break"),
                new PromptActionView("COMPLETE_PHASE", "Complete"),
                new PromptActionView("DISMISS", "Dismiss"),
            ],
            CreatedAt: null,
            ExpiresAt: null,
            Stale: false);

        var action = PromptAutoActionResolver.Resolve(prompt);
        Assert.Null(action);
    }

    [Fact]
    public void Resolve_PrefersStartupRecoveryActions_WhenPresent()
    {
        var prompt = new PromptView(
            PromptId: "p2",
            Kind: "startup_recovery",
            Severity: null,
            TileId: "tile-2",
            Title: "Recovery",
            Body: "",
            Why: "",
            SuggestedMinutes: null,
            Actions:
            [
                new PromptActionView("DISMISS", "Dismiss"),
                new PromptActionView("CONFIRM_CONTINUE", "Continue"),
            ],
            CreatedAt: null,
            ExpiresAt: null,
            Stale: false);

        var action = PromptAutoActionResolver.Resolve(prompt);
        Assert.Equal("CONFIRM_CONTINUE", action);
    }
}
