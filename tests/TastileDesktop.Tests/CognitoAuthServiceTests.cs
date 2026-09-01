using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class CognitoAuthServiceTests
{
    [Fact]
    public void BuildWebLoginUrl_OpensTastileLoginPage_WithDesktopPkce()
    {
        var cfg = new CognitoConfig(
            UserPoolId: "pool",
            ClientId: "client",
            HostedUiDomain: "tastile-beta",
            Region: "ap-northeast-1",
            CallbackUrl: "tastile://auth/callback",
            WebLoginUrl: "https://app.tastile.app/login");

        var url = CognitoAuthService.BuildWebLoginUrl(cfg, "challenge-123", "state-456");

        Assert.StartsWith("https://app.tastile.app/login?", url);
        Assert.Contains("redirect_uri=tastile%3A%2F%2Fauth%2Fcallback", url);
        Assert.Contains("code_challenge=challenge-123", url);
        Assert.Contains("state=state-456", url);
    }
}
