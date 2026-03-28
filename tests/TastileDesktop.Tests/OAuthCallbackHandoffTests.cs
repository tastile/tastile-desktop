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
        WithIsolatedLocalAppData(() =>
        {
            OAuthCallbackHandoff.StoreExpectedState("expected-state");

            Assert.True(OAuthCallbackHandoff.MatchesExpectedState("expected-state"));
            Assert.False(OAuthCallbackHandoff.MatchesExpectedState("other-state"));
            Assert.False(OAuthCallbackHandoff.MatchesExpectedState(null));

            OAuthCallbackHandoff.ClearExpectedState();
        });
    }

    [Fact]
    public void Peek_ReturnsStoredCallback_AndClearCallbackRemovesIt()
    {
        WithIsolatedLocalAppData(() =>
        {
            OAuthCallbackHandoff.Store("tastile://auth/callback?code=abc&state=expected");

            Assert.Equal("tastile://auth/callback?code=abc&state=expected", OAuthCallbackHandoff.Peek());
            Assert.Equal("tastile://auth/callback?code=abc&state=expected", OAuthCallbackHandoff.Peek());

            OAuthCallbackHandoff.ClearCallback();

            Assert.Null(OAuthCallbackHandoff.Peek());
        });
    }

    [Fact]
    public void MatchesExpectedState_ReturnsFalse_WhenNoStateStored()
    {
        WithIsolatedLocalAppData(() =>
        {
            Assert.False(OAuthCallbackHandoff.MatchesExpectedState("expected-state"));
        });
    }

    [Fact]
    public void ExtractStateFromAuthUrl_ReturnsEmptyString_WhenStateIsBlank()
    {
        var state = OAuthCallbackHandoff.ExtractStateFromAuthUrl(
            "https://example.com/oauth?provider=google&state=");

        Assert.Equal(string.Empty, state);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("https://example.com/oauth?provider=google")]
    public void ExtractStateFromAuthUrl_ReturnsNull_ForMalformedOrMissingState(string? authUrl)
    {
        Assert.Null(OAuthCallbackHandoff.ExtractStateFromAuthUrl(authUrl));
    }

    private static void WithIsolatedLocalAppData(Action action)
    {
        var originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        var tempLocalAppData = Path.Combine(Path.GetTempPath(), $"tastile-auth-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempLocalAppData);

        Environment.SetEnvironmentVariable("LOCALAPPDATA", tempLocalAppData);

        try
        {
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", originalLocalAppData);
            if (Directory.Exists(tempLocalAppData))
            {
                Directory.Delete(tempLocalAppData, recursive: true);
            }
        }
    }
}
