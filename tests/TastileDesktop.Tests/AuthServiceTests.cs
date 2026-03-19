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

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
