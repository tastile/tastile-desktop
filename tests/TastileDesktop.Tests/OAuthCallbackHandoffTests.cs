using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class OAuthCallbackHandoffTests
{
    [Fact]
    public void ExtractStateFromAuthUrl_ReturnsStateQueryParameter()
    {
        var state = OAuthCallbackHandoff.ExtractStateFromAuthUrl(
            "https://example.com/oauth?provider=google&state=expected-state&code_challenge=abc");

        Assert.Equal("expected-state", state);
    }

    [Fact]
    public void MatchesExpectedState_ReturnsTrueOnlyForExactMatch()
    {
        OAuthCallbackHandoff.StoreExpectedState("expected-state");

        Assert.True(OAuthCallbackHandoff.MatchesExpectedState("expected-state"));
        Assert.False(OAuthCallbackHandoff.MatchesExpectedState("other-state"));
        Assert.False(OAuthCallbackHandoff.MatchesExpectedState(null));

        OAuthCallbackHandoff.ClearExpectedState();
    }
}
