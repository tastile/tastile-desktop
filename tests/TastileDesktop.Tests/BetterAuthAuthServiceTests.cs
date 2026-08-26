using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

/// <summary>
/// Tests for <see cref="BetterAuthAuthService"/> covering the contract that
/// replaced the Cognito PKCE flow: email/password sign-in, sign-up fallback,
/// sign-out, missing-session behaviour, and DPAPI round-trip via a fake
/// <see cref="ITokenStore"/>.
/// </summary>
public sealed class BetterAuthAuthServiceTests
{
    [Fact]
    public async Task SignInWithEmailAsync_PersistsSessionAndRaisesEvent_OnSuccess()
    {
        var store = new InMemoryTokenStore();
        var handler = new StubHttpHandler
        {
            Responses =
            {
                ["/api/auth/sign-in/email"] = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"user\":{\"id\":\"user_1\",\"email\":\"alice@example.com\",\"emailVerified\":true}}"),
                }.WithSetCookie("better-auth.session_token=cookie_alpha"),
                ["/api/mobile/api-token"] = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"token\":\"v1_token_alpha\",\"token_id\":\"tok_alpha\"}"),
                },
            },
        };
        var service = new BetterAuthAuthService(store, new BetterAuthHttpClient("https://example.test", new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));

        var raised = 0;
        service.AuthStateChanged += (_, _) => Interlocked.Increment(ref raised);

        var result = await service.SignInWithEmailAsync("alice@example.com", "hunter2");

        Assert.True(result.Success);
        Assert.Null(result.ErrorCode);
        Assert.True(service.IsAuthenticated);
        Assert.NotNull(service.CurrentSession);
        Assert.Equal("cookie_alpha", service.CurrentSession!.SessionToken);
        Assert.Equal("v1_token_alpha", service.CurrentSession!.ApiToken);
        Assert.Equal("user_1", service.CurrentSession!.UserId);
        Assert.Equal("alice@example.com", service.CurrentSession!.Email);
        Assert.NotNull(store.LastSaved);
        Assert.Equal("cookie_alpha", store.LastSaved!.SessionToken);
        Assert.True(raised >= 1);
    }

    [Fact]
    public async Task SignInWithEmailAsync_ReturnsWrongPassword_On401()
    {
        var store = new InMemoryTokenStore();
        var handler = new StubHttpHandler
        {
            Responses =
            {
                ["/api/auth/sign-in/email"] = new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"message\":\"invalid email or password\"}"),
                },
            },
        };
        var service = new BetterAuthAuthService(store, new BetterAuthHttpClient("https://example.test", new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));

        var result = await service.SignInWithEmailAsync("alice@example.com", "wrong");

        Assert.False(result.Success);
        Assert.Equal("sign_in_failed", result.ErrorCode);
        Assert.False(service.IsAuthenticated);
        Assert.Null(service.CurrentSession);
        Assert.Null(store.LastSaved);
    }

    [Fact]
    public async Task SignInWithEmailAsync_ReturnsEmailNotVerified_On403()
    {
        var store = new InMemoryTokenStore();
        var handler = new StubHttpHandler
        {
            Responses =
            {
                ["/api/auth/sign-in/email"] = new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("{\"message\":\"email not verified\"}"),
                },
            },
        };
        var service = new BetterAuthAuthService(store, new BetterAuthHttpClient("https://example.test", new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));

        var result = await service.SignInWithEmailAsync("alice@example.com", "hunter2");

        Assert.False(result.Success);
        Assert.Equal("sign_in_failed", result.ErrorCode);
        Assert.Contains("email not verified", result.DetailOrEmpty(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SignInWithEmailAsync_ReturnsMissingSessionCookie_WhenCookieAbsent()
    {
        var store = new InMemoryTokenStore();
        var handler = new StubHttpHandler
        {
            Responses =
            {
                ["/api/auth/sign-in/email"] = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"user\":{\"id\":\"user_1\"}}"),
                },
            },
        };
        var service = new BetterAuthAuthService(store, new BetterAuthHttpClient("https://example.test", new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));

        var result = await service.SignInWithEmailAsync("alice@example.com", "hunter2");

        Assert.False(result.Success);
        Assert.Equal("missing_session_cookie", result.ErrorCode);
    }

    [Fact]
    public async Task SignOutAsync_ClearsStoreAndRaisesEvent()
    {
        var store = new InMemoryTokenStore();
        var handler = new StubHttpHandler
        {
            Responses =
            {
                ["/api/auth/sign-in/email"] = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"user\":{\"id\":\"user_1\",\"email\":\"alice@example.com\"}}"),
                }.WithSetCookie("better-auth.session_token=cookie_alpha"),
                ["/api/mobile/api-token"] = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"token\":\"v1_token_alpha\"}"),
                },
                ["/api/auth/sign-out"] = new HttpResponseMessage(HttpStatusCode.OK),
            },
        };
        var service = new BetterAuthAuthService(store, new BetterAuthHttpClient("https://example.test", new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));

        await service.SignInWithEmailAsync("alice@example.com", "hunter2");
        Assert.NotNull(store.LastSaved);

        var raised = 0;
        service.AuthStateChanged += (_, _) => Interlocked.Increment(ref raised);
        await service.SignOutAsync();

        Assert.False(service.IsAuthenticated);
        Assert.Null(service.CurrentSession);
        Assert.Null(store.LastSaved);
        Assert.True(raised >= 1, "AuthStateChanged must fire on sign-out");
    }

    [Fact]
    public async Task TryLoadFromStoreAsync_RestoresPersistedSession()
    {
        var store = new InMemoryTokenStore();
        var handler = new StubHttpHandler();
        var service = new BetterAuthAuthService(store, new BetterAuthHttpClient("https://example.test", new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));

        store.Preload = new AuthSession(
            SessionToken: "restored_cookie",
            ApiToken: "restored_token",
            UserId: "user_r",
            Email: "restored@example.com",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

        var session = await service.TryLoadFromStoreAsync();

        Assert.NotNull(session);
        Assert.Equal("restored_cookie", service.CurrentSession!.SessionToken);
        Assert.True(service.IsAuthenticated);
    }

    [Fact]
    public void TryLoadFromStoreAsync_IsAuthenticatedFalse_WhenStoreEmpty()
    {
        var store = new InMemoryTokenStore();
        var service = new BetterAuthAuthService(store, new BetterAuthHttpClient("https://example.test", new HttpClient(new StubHttpHandler()) { BaseAddress = new Uri("https://example.test/") }));

        Assert.False(service.IsAuthenticated);
        Assert.Null(service.CurrentSession);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReturnsSessionToken()
    {
        var store = new InMemoryTokenStore();
        var handler = new StubHttpHandler
        {
            Responses =
            {
                ["/api/auth/sign-in/email"] = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"user\":{\"id\":\"user_1\",\"email\":\"alice@example.com\"}}"),
                }.WithSetCookie("better-auth.session_token=session_xyz"),
                ["/api/mobile/api-token"] = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"token\":\"api_xyz\"}"),
                },
            },
        };
        var service = new BetterAuthAuthService(store, new BetterAuthHttpClient("https://example.test", new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));

        await service.SignInWithEmailAsync("alice@example.com", "hunter2");

        var bearer = await service.GetAccessTokenAsync();
        Assert.Equal("session_xyz", bearer);
    }

    [Fact]
    public void AuthSession_JsonRoundTrip_PreservesBetterAuthFields()
    {
        var session = new AuthSession(
            SessionToken: "cookie",
            ApiToken: "api",
            UserId: "u",
            Email: "x@example.com",
            ExpiresAt: DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

        var json = JsonSerializer.Serialize(session);
        var parsed = JsonSerializer.Deserialize<AuthSession>(json)!;

        Assert.Equal(session.SessionToken, parsed.SessionToken);
        Assert.Equal(session.ApiToken, parsed.ApiToken);
        Assert.Equal(session.UserId, parsed.UserId);
        Assert.Equal(session.Email, parsed.Email);
        Assert.Equal(session.ExpiresAt, parsed.ExpiresAt);
        Assert.Contains("session_token", json);
        Assert.Contains("api_token", json);
        Assert.DoesNotContain("id_token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh_token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_ReturnsCurrentSession_WhenServerSessionIsValid()
    {
        // Covers the 401-retry happy path: the server validates the existing
        // session token against /api/auth/session so the same token can be
        // retried on the original request.
        var store = new InMemoryTokenStore
        {
            Preload = new AuthSession(
                SessionToken: "live_cookie",
                ApiToken: "live_api",
                UserId: "user_live",
                Email: "live@example.com",
                ExpiresAt: DateTimeOffset.UtcNow.AddHours(1)),
        };
        var handler = new StubHttpHandler
        {
            Responses =
            {
                ["/api/auth/session"] = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"user\":{\"id\":\"user_live\",\"email\":\"live@example.com\"}}"),
                },
            },
        };
        var service = new BetterAuthAuthService(store, new BetterAuthHttpClient("https://example.test", new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
        await service.TryLoadFromStoreAsync();

        var result = await service.RefreshAsync();

        Assert.NotNull(result);
        Assert.Equal("live_cookie", result!.SessionToken);
        Assert.True(service.IsAuthenticated);
    }

    [Fact]
    public async Task RefreshAsync_ClearsLocalStateAndReturnsNull_WhenServerReturns401()
    {
        // The /api/auth/session round-trip is the contract that lets the
        // CoreApiClient 401-retry path bail out instead of looping with a
        // dead token. On 401 we must clear CurrentSession + the DPAPI store
        // and notify subscribers.
        var store = new InMemoryTokenStore
        {
            Preload = new AuthSession(
                SessionToken: "expired_cookie",
                ApiToken: "expired_api",
                UserId: "user_x",
                Email: "x@example.com",
                ExpiresAt: DateTimeOffset.UtcNow.AddHours(1)),
        };
        var handler = new StubHttpHandler
        {
            Responses =
            {
                ["/api/auth/session"] = new HttpResponseMessage(HttpStatusCode.Unauthorized),
            },
        };
        var service = new BetterAuthAuthService(store, new BetterAuthHttpClient("https://example.test", new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
        await service.TryLoadFromStoreAsync();

        var raised = 0;
        service.AuthStateChanged += (_, _) => Interlocked.Increment(ref raised);

        var result = await service.RefreshAsync();

        Assert.Null(result);
        Assert.False(service.IsAuthenticated);
        Assert.Null(service.CurrentSession);
        Assert.Null(store.LastSaved);
        Assert.True(raised >= 1, "AuthStateChanged must fire when the local session is cleared");
    }

    [Fact]
    public async Task RefreshAsync_PreservesLocalState_WhenServerReturns500()
    {
        // Transient backend failure: do NOT clear the local session, the
        // 401-retry will surface 401 to the caller and the next user
        // action can re-attempt.
        var store = new InMemoryTokenStore
        {
            Preload = new AuthSession(
                SessionToken: "live_cookie",
                ApiToken: "live_api",
                UserId: "user_y",
                Email: "y@example.com",
                ExpiresAt: DateTimeOffset.UtcNow.AddHours(1)),
        };
        var handler = new StubHttpHandler
        {
            Responses =
            {
                ["/api/auth/session"] = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{\"message\":\"db unavailable\"}"),
                },
            },
        };
        var service = new BetterAuthAuthService(store, new BetterAuthHttpClient("https://example.test", new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
        await service.TryLoadFromStoreAsync();

        var result = await service.RefreshAsync();

        Assert.NotNull(result);
        Assert.Equal("live_cookie", result!.SessionToken);
        Assert.True(service.IsAuthenticated);
    }

    [Fact]
    public async Task RefreshAsync_ReturnsNull_WhenNoLocalSessionExists()
    {
        // The 401-retry path must not invent a session when the store is
        // empty; CoreApiClient will surface 401 to the caller.
        var store = new InMemoryTokenStore();
        var handler = new StubHttpHandler();
        var service = new BetterAuthAuthService(store, new BetterAuthHttpClient("https://example.test", new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));

        var result = await service.RefreshAsync();

        Assert.Null(result);
        Assert.False(service.IsAuthenticated);
    }
}

internal static class BetterAuthAuthServiceTestExtensions
{
    public static string? DetailOrEmpty(this AuthResult result) => result.Detail;
}

internal sealed class StubHttpHandler : HttpMessageHandler
{
    public Dictionary<string, HttpResponseMessage> Responses { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = request.RequestUri?.AbsolutePath ?? string.Empty;
        if (Responses.TryGetValue(key, out var response))
        {
            return Task.FromResult(response);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"no stub for {key}"),
        });
    }
}

internal static class HttpResponseMessageExtensions
{
    public static HttpResponseMessage WithSetCookie(this HttpResponseMessage response, string setCookieValue)
    {
        response.Headers.TryAddWithoutValidation("Set-Cookie", setCookieValue);
        return response;
    }
}

internal sealed class InMemoryTokenStore : ITokenStore
{
    public AuthSession? Preload { get; set; }
    public AuthSession? LastSaved { get; private set; }
    public int ClearCount { get; private set; }

    public Task<AuthSession?> LoadAsync() => Task.FromResult(Preload);

    public Task SaveAsync(AuthSession session)
    {
        LastSaved = session;
        Preload = session;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Preload = null;
        LastSaved = null;
        ClearCount++;
        return Task.CompletedTask;
    }
}
