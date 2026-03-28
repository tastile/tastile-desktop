using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task RefreshSessionFromDaemonAsync_PopulatesSessionAndRaisesAuthStateChanged()
    {
        await WithIsolatedAppDataAsync(async () =>
        {
            var service = CreateIsolatedAuthService();
            var eventCount = 0;
            service.AuthStateChanged += (_, _) => eventCount++;

            var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
            {
                if (request.RequestUri?.AbsolutePath == "/auth/session")
                {
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

            var changed = await service.RefreshSessionFromDaemonAsync(client);

            Assert.True(changed);
            Assert.True(service.IsAuthenticated);
            Assert.Equal("user@example.com", service.UserEmail);
            Assert.Equal(1, eventCount);
        });
    }

    [Fact]
    public async Task RefreshSessionFromDaemonAsync_DoesNotAuthenticate_WhenAccessTokenMissing()
    {
        await WithIsolatedAppDataAsync(async () =>
        {
            var service = CreateIsolatedAuthService();

            var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
            {
                if (request.RequestUri?.AbsolutePath == "/auth/session")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""
                        {
                          "user_id": "user-123",
                          "email": "user@example.com",
                          "access_token": "",
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

            var changed = await service.RefreshSessionFromDaemonAsync(client);

            Assert.False(changed);
            Assert.False(service.IsAuthenticated);
            Assert.Null(service.UserEmail);
        });
    }

    [Fact]
    public async Task InitializeAsync_DoesNotRestoreSavedSession_WhenDaemonSessionIsMissing()
    {
        await WithIsolatedAppDataAsync(async tempAppData =>
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempAppData, "Tastile", "session.json"),
                """
                {
                  "user_id": "user-restore",
                  "email": "restore@example.com",
                  "access_token": "token-restore",
                  "refresh_token": "refresh-restore",
                  "expires_at": "2099-01-01T00:00:00Z"
                }
                """);

            var restoreCalled = false;
            var service = CreateIsolatedAuthService();
            var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
            {
                if (request.RequestUri?.AbsolutePath == "/auth/session")
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                if (request.RequestUri?.AbsolutePath == "/auth/session/restore")
                {
                    restoreCalled = true;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""
                        {
                          "user_id": "user-restore",
                          "email": "restore@example.com",
                          "access_token": "token-restore",
                          "refresh_token": "refresh-restore",
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

            await service.InitializeAsync(client);

            Assert.False(restoreCalled);
            Assert.False(service.IsAuthenticated);
            Assert.Null(service.UserEmail);
        });
    }

    [Fact]
    public async Task GetTileQuotaAsync_ParsesQuotaResponse()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/auth/tile-quota")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "plan": "free",
                      "tile_count": 100,
                      "max_tiles": 100,
                      "remaining_tiles": 0,
                      "limit_reached": true
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
        Assert.Equal("free", quota!.Plan);
        Assert.Equal(100, quota.TileCount);
        Assert.True(quota.LimitReached);
    }

    [Fact]
    public async Task GetSessionAsync_ParsesRefreshTokenFromDaemonResponse()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/auth/session")
            {
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

        var session = await client.GetSessionAsync();

        Assert.NotNull(session);
        Assert.Equal("refresh-123", session!.RefreshToken);
    }

    private static AuthService CreateIsolatedAuthService()
    {
        var ctor = typeof(AuthService).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        Assert.NotNull(ctor);
        return Assert.IsType<AuthService>(ctor!.Invoke(null));
    }

    private static Task WithIsolatedAppDataAsync(Func<Task> action)
        => WithIsolatedAppDataAsync(_ => action());

    private static async Task WithIsolatedAppDataAsync(Func<string, Task> action)
    {
        var originalAppData = Environment.GetEnvironmentVariable("APPDATA");
        var tempAppData = Path.Combine(Path.GetTempPath(), $"tastile-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempAppData, "Tastile"));
        Environment.SetEnvironmentVariable("APPDATA", tempAppData);

        try
        {
            await action(tempAppData);
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPDATA", originalAppData);
            if (Directory.Exists(tempAppData))
            {
                Directory.Delete(tempAppData, recursive: true);
            }
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
