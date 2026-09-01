using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

/// <summary>
/// Tests for <see cref="BetterAuthHttpClient"/> using a fake
/// <see cref="HttpMessageHandler"/> so the suite runs without a live
/// tastile-web instance.
/// </summary>
public sealed class BetterAuthHttpClientTests
{
    [Fact]
    public async Task SignInAsync_ReturnsSessionTokenAndUser_OnSuccess()
    {
        var handler = new CapturingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"user\":{\"id\":\"user_123\",\"email\":\"alice@example.com\",\"emailVerified\":true}}"),
                Headers = { },
            },
            SetCookie = "better-auth.session_token=cookie_value_abc; Path=/; HttpOnly; SameSite=Lax",
        };

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new BetterAuthHttpClient("https://example.test", http);

        var result = await client.SignInAsync("alice@example.com", "hunter2");

        Assert.Equal("cookie_value_abc", result.SetCookie);
        Assert.NotNull(result.User);
        Assert.Equal("user_123", result.User!.Id);
        Assert.Equal("alice@example.com", result.User!.Email);
        Assert.True(result.User!.EmailVerified);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(new Uri("https://example.test/api/auth/sign-in/email"), handler.LastRequest!.RequestUri);
    }

    [Fact]
    public async Task SignInAsync_Throws_OnWrongPassword()
    {
        var handler = new CapturingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"message\":\"invalid email or password\"}"),
            },
        };

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new BetterAuthHttpClient("https://example.test", http);

        var ex = await Assert.ThrowsAsync<BetterAuthHttpException>(() =>
            client.SignInAsync("alice@example.com", "wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal("sign_in_failed", ex.ErrorCode);
        Assert.Contains("invalid email or password", ex.Detail);
    }

    [Fact]
    public async Task SignInAsync_Throws_OnUnverifiedEmail()
    {
        var handler = new CapturingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"message\":\"email not verified\"}"),
            },
        };

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new BetterAuthHttpClient("https://example.test", http);

        var ex = await Assert.ThrowsAsync<BetterAuthHttpException>(() =>
            client.SignInAsync("alice@example.com", "hunter2"));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal("sign_in_failed", ex.ErrorCode);
        Assert.Contains("email not verified", ex.Detail);
    }

    [Fact]
    public async Task SignUpAsync_ReturnsUserAndCookie_OnSuccess()
    {
        var handler = new CapturingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"user\":{\"id\":\"user_999\",\"email\":\"new@example.com\"}}"),
            },
            SetCookie = "better-auth.session_token=new_cookie; Path=/",
        };

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new BetterAuthHttpClient("https://example.test", http);

        var result = await client.SignUpAsync("new@example.com", "secret!", "New User");

        Assert.Equal("new_cookie", result.SetCookie);
        Assert.Equal("new@example.com", result.User!.Email);
    }

    [Fact]
    public async Task SignOutAsync_DoesNotThrow_OnUnauthorized()
    {
        var handler = new CapturingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.Unauthorized),
        };

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new BetterAuthHttpClient("https://example.test", http);

        await client.SignOutAsync("any_token");
        Assert.NotNull(handler.LastRequest);
        Assert.NotNull(handler.LastRequest!.Headers.Authorization);
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("any_token", handler.LastRequest!.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task GetSessionAsync_ReturnsNull_OnUnauthorized()
    {
        var handler = new CapturingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.Unauthorized),
        };

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new BetterAuthHttpClient("https://example.test", http);

        var session = await client.GetSessionAsync("stale_token");
        Assert.Null(session);
    }

    [Fact]
    public async Task MintApiTokenAsync_PostsToApiTokenEndpoint()
    {
        var handler = new CapturingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"token\":\"v1_long_lived_token\",\"token_id\":\"tok_42\"}"),
            },
        };

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new BetterAuthHttpClient("https://example.test", http);

        var result = await client.MintApiTokenAsync("session_cookie_value");

        Assert.Equal("v1_long_lived_token", result.Token);
        Assert.Equal("tok_42", result.TokenId);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(new Uri("https://example.test/api/mobile/api-token"), handler.LastRequest!.RequestUri);
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("session_cookie_value", handler.LastRequest!.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task MintApiTokenAsync_Throws_WhenTokenFieldMissing()
    {
        var handler = new CapturingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"unrelated\":\"field\"}"),
            },
        };

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new BetterAuthHttpClient("https://example.test", http);

        var ex = await Assert.ThrowsAsync<BetterAuthHttpException>(() =>
            client.MintApiTokenAsync("session"));

        Assert.Equal("api_token_failed", ex.ErrorCode);
    }

    [Fact]
    public void Constructor_Throws_OnEmptyBaseUrl()
    {
        Assert.Throws<ArgumentException>(() => new BetterAuthHttpClient(string.Empty));
        Assert.Throws<ArgumentException>(() => new BetterAuthHttpClient("   "));
    }

    [Theory]
    [InlineData("__Secure-better-auth.session_token", "secure_cookie_value")]
    [InlineData("__Host-better-auth.session_token", "host_cookie_value")]
    [InlineData("__secure-better-auth.session_token", "case_insensitive_value")]
    public async Task SignInAsync_ExtractsSessionToken_WithCookieNamePrefix(string cookieName, string expectedValue)
    {
        // BetterAuth production deployments pin the session cookie with
        // RFC 6265 §4.1.3 / §5.3 prefixes (__Secure-, __Host-). The
        // extractor must strip the prefix and match the base cookie name
        // case-insensitively before pulling the value.
        var handler = new CapturingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"user\":{\"id\":\"user_123\",\"email\":\"alice@example.com\"}}"),
            },
            SetCookie = $"{cookieName}={expectedValue}; Path=/; Secure; HttpOnly; SameSite=Lax",
        };

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new BetterAuthHttpClient("https://example.test", http);

        var result = await client.SignInAsync("alice@example.com", "hunter2");

        Assert.Equal(expectedValue, result.SetCookie);
    }

    [Fact]
    public async Task SignInAsync_SkipsUnrelatedCookies_BeforeMatchingPrefixedSessionCookie()
    {
        // When multiple Set-Cookie headers come back (e.g. analytics
        // cookies, theme cookies, the BetterAuth session), the extractor
        // must walk past the unrelated names and match the prefixed
        // session cookie.
        var handler = new CapturingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"user\":{\"id\":\"user_123\"}}"),
            },
        };

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new BetterAuthHttpClient("https://example.test", http);
        // Inject headers AFTER constructing the handler so we control the
        // exact ordering; the production path also funnels through
        // TryAddWithoutValidation so case-insensitive matching exercises
        // the same code branch.
        handler.Response.Headers.TryAddWithoutValidation("Set-Cookie", "_ga=GA1.1.1.1; Path=/");
        handler.Response.Headers.TryAddWithoutValidation("Set-Cookie", "theme=dark; Path=/");
        handler.Response.Headers.TryAddWithoutValidation("Set-Cookie", "__Secure-better-auth.session_token=secure_cookie; Path=/; Secure; HttpOnly");

        var result = await client.SignInAsync("alice@example.com", "hunter2");

        Assert.Equal("secure_cookie", result.SetCookie);
    }
}

internal sealed class CapturingHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
    public string? SetCookie { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (!string.IsNullOrEmpty(SetCookie))
        {
            Response.Headers.TryAddWithoutValidation("Set-Cookie", SetCookie);
        }
        return Task.FromResult(Response);
    }
}
