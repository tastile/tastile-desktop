using TastileDesktop;

namespace TastileDesktop.Tests;

public sealed class ProtocolHandlerTests
{
    [Fact]
    public void ParseTokenCallback_ReadsDesktopTokenHandoffFragment()
    {
        var callback = "tastile://auth/callback?state=state-1#id_token=id.1&access_token=access.1&refresh_token=refresh.1&expires_in=3600";

        var result = ProtocolHandler.ParseTokenCallback(callback);

        Assert.NotNull(result);
        Assert.Equal("state-1", result.State);
        Assert.Equal("id.1", result.IdToken);
        Assert.Equal("access.1", result.AccessToken);
        Assert.Equal("refresh.1", result.RefreshToken);
        Assert.Equal(3600, result.ExpiresIn);
    }
}
