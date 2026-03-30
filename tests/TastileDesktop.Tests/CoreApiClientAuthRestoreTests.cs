using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class CoreApiClientAuthRestoreTests
{
    [Fact]
    public async Task TriggerSyncAsync_RestoresSessionAndRetries_WhenDaemonReturnsUnauthenticated()
    {
        var restoreCalls = 0;
        var triggerCalls = 0;
        var sessionCalls = 0;

        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == "/auth/session")
            {
                sessionCalls++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "user_id": "user-123",
                      "email": "user@example.com",
                      "access_token": "token-123",
                      "refresh_token": "refresh-123",
                      "expires_at": "2099-01-01T00:00:00Z"
                    }
                    """),
                };
            }

            if (path == "/sync/trigger")
            {
                triggerCalls++;
                if (triggerCalls == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    {
                        Content = new StringContent("""{"error":"auth error: Not authenticated"}"""),
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }

            if (path == "/auth/session/restore")
            {
                restoreCalls++;
                var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                var json = JsonDocument.Parse(body).RootElement;
                Assert.Equal("user-123", json.GetProperty("user_id").GetString());
                Assert.Equal("token-123", json.GetProperty("access_token").GetString());

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "user_id": "user-123",
                      "email": "user@example.com",
                      "access_token": "token-123",
                      "refresh_token": "refresh-123",
                      "expires_at": "2099-01-01T00:00:00Z"
                    }
                    """),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        await client.TriggerSyncAsync();

        Assert.Equal(2, triggerCalls);
        Assert.Equal(1, restoreCalls);
        Assert.Equal(1, sessionCalls);
    }

    [Fact]
    public async Task SignInWithOAuthAsync_UsesExchangeEndpoint_WithState()
    {
        var exchangeCalls = 0;

        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == "/auth/oauth/exchange")
            {
                exchangeCalls++;
                var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                var json = JsonDocument.Parse(body).RootElement;
                Assert.Equal("code-123", json.GetProperty("code").GetString());
                Assert.Equal("state-123", json.GetProperty("state").GetString());
                Assert.False(json.TryGetProperty("redirect_uri", out _));
                Assert.False(json.TryGetProperty("provider", out _));

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "access_token": "token-123",
                      "refresh_token": "refresh-123",
                      "expires_at": 999999,
                      "user": { "id": "u1", "email": "user@example.com" }
                    }
                    """),
                };
            }

            if (path == "/auth/oauth/callback")
            {
                throw new Exception("Legacy callback endpoint should not be called");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var result = await client.SignInWithOAuthAsync("google", "code-123", "tastile://auth/callback", "state-123");

        Assert.NotNull(result);
        Assert.Equal("token-123", result!.AccessToken);
        Assert.Equal(1, exchangeCalls);
    }

    [Fact]
    public async Task GetTileQuotaAsync_RestoresSessionAndRetries_WhenDaemonReturnsUnauthorized()
    {
        var restoreCalls = 0;
        var quotaCalls = 0;
        var sessionCalls = 0;

        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == "/auth/session")
            {
                sessionCalls++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "user_id": "user-123",
                      "email": "user@example.com",
                      "access_token": "token-123",
                      "refresh_token": "refresh-123",
                      "expires_at": "2099-01-01T00:00:00Z"
                    }
                    """),
                };
            }

            if (path == "/auth/tile-quota")
            {
                quotaCalls++;
                if (quotaCalls == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "plan": "free",
                      "tile_count": 5,
                      "max_tiles": 100,
                      "remaining_tiles": 95,
                      "limit_reached": false
                    }
                    """),
                };
            }

            if (path == "/auth/session/restore")
            {
                restoreCalls++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "user_id": "user-123",
                      "email": "user@example.com",
                      "access_token": "token-123",
                      "refresh_token": "refresh-123",
                      "expires_at": "2099-01-01T00:00:00Z"
                    }
                    """),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var quota = await client.GetTileQuotaAsync();

        Assert.NotNull(quota);
        Assert.False(quota!.LimitReached);
        Assert.Equal(2, quotaCalls);
        Assert.Equal(1, restoreCalls);
        Assert.Equal(1, sessionCalls);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
