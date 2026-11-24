using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

/// <summary>
/// Tests for the NF04-D01 PKCE surface: verifier/challenge generation,
/// the <c>/cli/authorize</c> URL contract, the loopback callback listener,
/// and the <see cref="BetterAuthAuthService.SignInWithBrowserAsync"/>
/// orchestration including token exchange, state mismatch, and denial.
/// </summary>
public sealed class PkceAuthTests
{
    [Fact]
    public void GenerateVerifier_ProducesUnreservedCharacters_WithinRfc7636Bounds()
    {
        var verifier = Pkce.GenerateVerifier();

        Assert.InRange(verifier.Length, 43, 128);
        Assert.All(verifier, c => Assert.Contains(c, PkceUnreserved));
    }

    [Fact]
    public void ComputeS256Challenge_MatchesRfc7636AppendixB()
    {
        // RFC 7636 appendix B example verifier.
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string expected = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        Assert.Equal(expected, Pkce.ComputeS256Challenge(verifier));
    }

    [Fact]
    public void GenerateState_ProducesUniqueBase64UrlValues()
    {
        var a = Pkce.GenerateState();
        var b = Pkce.GenerateState();

        Assert.NotEqual(a, b);
        Assert.DoesNotContain("+", a);
        Assert.DoesNotContain("/", a);
        Assert.DoesNotContain("=", a);
    }

    [Fact]
    public void BuildAuthorizeUrl_EmitsAllSevenContractParameters()
    {
        var url = Pkce.BuildAuthorizeUrl(
            "https://app.tastile.example/",
            "http://127.0.0.1:53111/callback",
            "challenge123",
            "state456");

        var uri = new Uri(url);
        Assert.Equal("https", uri.Scheme);
        Assert.Equal("/cli/authorize", uri.AbsolutePath);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("tastile-desktop", query["client_id"]);
        Assert.Equal("http://127.0.0.1:53111/callback", query["redirect_uri"]);
        Assert.Equal("tastile.read tastile.write", query["scope"]);
        Assert.Equal("state456", query["state"]);
        Assert.Equal("challenge123", query["code_challenge"]);
        Assert.Equal("S256", query["code_challenge_method"]);
    }

    [Fact]
    public async Task LoopbackCallbackListener_ReturnsParsedCallback_OnCallbackPath()
    {
        await using var listener = new LoopbackCallbackListener();
        Assert.StartsWith("http://127.0.0.1:", listener.RedirectUri);
        Assert.EndsWith("/callback", listener.RedirectUri);

        var waitTask = listener.WaitForCallbackAsync();
        using var http = new HttpClient();
        var response = await http.GetAsync($"{listener.RedirectUri}?code=authcode1&state=state1");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Sign-in complete", body);

        var callback = await waitTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("authcode1", callback.Code);
        Assert.Equal("state1", callback.State);
        Assert.Null(callback.Error);
    }

    [Fact]
    public async Task LoopbackCallbackListener_SurfacesErrorParameters()
    {
        await using var listener = new LoopbackCallbackListener();
        var waitTask = listener.WaitForCallbackAsync();
        using var http = new HttpClient();
        await http.GetAsync($"{listener.RedirectUri}?error=access_denied&error_description=user+denied&state=s1");

        var callback = await waitTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("access_denied", callback.Error);
        Assert.Equal("user denied", callback.ErrorDescription);
        Assert.Equal("s1", callback.State);
    }

    [Fact]
    public async Task LoopbackCallbackListener_IgnoresNonCallbackPaths()
    {
        await using var listener = new LoopbackCallbackListener();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var waitTask = listener.WaitForCallbackAsync(cts.Token);
        using var http = new HttpClient();

        var root = new Uri(listener.RedirectUri).GetLeftPart(UriPartial.Authority);
        var notFound = await http.GetAsync($"{root}/favicon.ico");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, notFound.StatusCode);

        // The real callback still resolves after the 404.
        var response = await http.GetAsync($"{listener.RedirectUri}?code=c2&state=s2");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var callback = await waitTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("c2", callback.Code);
    }

    [Fact]
    public async Task SignInWithBrowserAsync_PersistsPkceSession_AndExchangesCode()
    {
        var store = new InMemoryTokenStore();
        var handler = new StubHttpHandler();
        handler.Responses["/api/cli/token"] = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"token\":\"core_api_token_1\",\"expires_at\":\"2030-01-01T00:00:00Z\",\"subject\":\"user_42\"}"),
        };
        var service = new BetterAuthAuthService(
            store,
            new BetterAuthHttpClient("https://web.test", new HttpClient(handler) { BaseAddress = new Uri("https://web.test/") }));

        var authorizeUrlSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? callbackTask = null;
        var resultTask = service.SignInWithBrowserAsync(url =>
        {
            authorizeUrlSource.SetResult(url);
            // Fire the loopback redirect in the background: the service only
            // starts accepting after the launcher returns, so awaiting the
            // HTTP round-trip here would deadlock.
            callbackTask = DriveCallbackAsync(url, "code_xyz", ExtractState(url));
            return Task.CompletedTask;
        }, webBaseUrl: "https://web.test");

        var authorizeUrl = await authorizeUrlSource.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("client_id=tastile-desktop", authorizeUrl);
        Assert.Contains("/cli/authorize?", authorizeUrl);

        var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.NotNull(callbackTask);
        await callbackTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(result.Success, $"expected success, got {result.ErrorCode}: {result.Detail}");
        Assert.True(service.IsAuthenticated);
        var session = service.CurrentSession;
        Assert.NotNull(session);
        Assert.Equal(AuthFlows.Pkce, session!.Flow);
        Assert.Equal("core_api_token_1", session.SessionToken);
        Assert.Equal("core_api_token_1", session.ApiToken);
        Assert.Equal("user_42", session.UserId);
        Assert.Null(session.Email);
        Assert.Equal("core_api_token_1", await service.GetAccessTokenAsync());
        Assert.NotNull(store.LastSaved);
        Assert.Equal(AuthFlows.Pkce, store.LastSaved!.Flow);

        // The token exchange must carry the exact three wire keys.
        Assert.NotNull(handler.LastRequestBody);
        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("code_xyz", doc.RootElement.GetProperty("code").GetString());
        Assert.True(doc.RootElement.GetProperty("code_verifier").GetString()!.Length >= 43);
        Assert.StartsWith("http://127.0.0.1:", doc.RootElement.GetProperty("redirect_uri").GetString());
        Assert.Equal(3, doc.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public async Task SignInWithBrowserAsync_RejectsStateMismatch()
    {
        var store = new InMemoryTokenStore();
        var handler = new StubHttpHandler();
        var service = new BetterAuthAuthService(
            store,
            new BetterAuthHttpClient("https://web.test", new HttpClient(handler) { BaseAddress = new Uri("https://web.test/") }));

        Task? callbackTask = null;
        var resultTask = service.SignInWithBrowserAsync(url =>
        {
            callbackTask = DriveCallbackAsync(url, "code_xyz", "attacker_state");
            return Task.CompletedTask;
        }, webBaseUrl: "https://web.test");

        var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.NotNull(callbackTask);
        await callbackTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(result.Success);
        Assert.Equal("state_mismatch", result.ErrorCode);
        Assert.False(service.IsAuthenticated);
        Assert.Null(store.LastSaved);
    }

    [Fact]
    public async Task SignInWithBrowserAsync_SurfacesAuthorizationError()
    {
        var store = new InMemoryTokenStore();
        var handler = new StubHttpHandler();
        var service = new BetterAuthAuthService(
            store,
            new BetterAuthHttpClient("https://web.test", new HttpClient(handler) { BaseAddress = new Uri("https://web.test/") }));

        Task? callbackTask = null;
        var resultTask = service.SignInWithBrowserAsync(url =>
        {
            callbackTask = DriveCallbackAsync(url, code: null, state: ExtractState(url), error: "access_denied");
            return Task.CompletedTask;
        }, webBaseUrl: "https://web.test");

        var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.NotNull(callbackTask);
        await callbackTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(result.Success);
        Assert.Equal("authorization_denied", result.ErrorCode);
        Assert.False(service.IsAuthenticated);
    }

    [Fact]
    public async Task SignInWithBrowserAsync_MapsTokenExchangeFailure()
    {
        var store = new InMemoryTokenStore();
        var handler = new StubHttpHandler();
        handler.Responses["/api/cli/token"] = new HttpResponseMessage(System.Net.HttpStatusCode.Conflict)
        {
            Content = new StringContent("{\"error\":\"already_used\",\"detail\":\"grant consumed\"}"),
        };
        var service = new BetterAuthAuthService(
            store,
            new BetterAuthHttpClient("https://web.test", new HttpClient(handler) { BaseAddress = new Uri("https://web.test/") }));

        Task? callbackTask = null;
        var resultTask = service.SignInWithBrowserAsync(url =>
        {
            callbackTask = DriveCallbackAsync(url, "code_replayed", ExtractState(url));
            return Task.CompletedTask;
        }, webBaseUrl: "https://web.test");

        var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.NotNull(callbackTask);
        await callbackTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(result.Success);
        Assert.Equal("already_used", result.ErrorCode);
        Assert.False(service.IsAuthenticated);
    }

    [Fact]
    public async Task RefreshAsync_ForPkceSession_KeepsSession_OnProbeSuccess_AndDrops_On401()
    {
        var store = new InMemoryTokenStore
        {
            Preload = new AuthSession("core_tok", "core_tok", "user_1", null, DateTimeOffset.UtcNow.AddDays(10))
            {
                Flow = AuthFlows.Pkce,
            },
        };
        var okHandler = new StubHttpHandler();
        okHandler.Responses["/v1/tiles"] = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"tiles\":[]}"),
        };
        var serviceOk = new BetterAuthAuthService(
            store,
            new BetterAuthHttpClient("https://web.test", new HttpClient(new StubHttpHandler()) { BaseAddress = new Uri("https://web.test/") }),
            coreHttpFactory: () => new HttpClient(okHandler) { BaseAddress = new Uri("https://core.test/") });
        await serviceOk.TryLoadFromStoreAsync();

        var kept = await serviceOk.RefreshAsync();
        Assert.NotNull(kept);
        Assert.True(serviceOk.IsAuthenticated);

        var denyHandler = new StubHttpHandler();
        denyHandler.Responses["/v1/tiles"] = new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
        var serviceDenied = new BetterAuthAuthService(
            store,
            new BetterAuthHttpClient("https://web.test", new HttpClient(new StubHttpHandler()) { BaseAddress = new Uri("https://web.test/") }),
            coreHttpFactory: () => new HttpClient(denyHandler) { BaseAddress = new Uri("https://core.test/") });
        await serviceDenied.TryLoadFromStoreAsync();

        var dropped = await serviceDenied.RefreshAsync();
        Assert.Null(dropped);
        Assert.False(serviceDenied.IsAuthenticated);
        Assert.Equal(1, store.ClearCount);
    }

    [Fact]
    public async Task SignOutAsync_ForPkceSession_RevokesCoreToken()
    {
        var store = new InMemoryTokenStore
        {
            Preload = new AuthSession("core_tok", "core_tok", "user_1", null, DateTimeOffset.UtcNow.AddDays(10))
            {
                Flow = AuthFlows.Pkce,
            },
        };
        var coreHandler = new StubHttpHandler();
        coreHandler.Responses["/v1/auth/signout"] = new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
        var service = new BetterAuthAuthService(
            store,
            new BetterAuthHttpClient("https://web.test", new HttpClient(new StubHttpHandler()) { BaseAddress = new Uri("https://web.test/") }),
            coreHttpFactory: () => new HttpClient(coreHandler) { BaseAddress = new Uri("https://core.test/") });
        await service.TryLoadFromStoreAsync();

        await service.SignOutAsync();

        Assert.False(service.IsAuthenticated);
        Assert.True(coreHandler.SawRequest("/v1/auth/signout"));
        Assert.Equal("Bearer", coreHandler.LastAuthorizationScheme);
        Assert.Equal("core_tok", coreHandler.LastAuthorizationParameter);
        Assert.Equal(1, store.ClearCount);
    }

    private static async Task DriveCallbackAsync(string authorizeUrl, string? code, string? state, string? error = null)
    {
        var uri = new Uri(authorizeUrl);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var redirectUri = query["redirect_uri"]!;

        var callbackQuery = error is not null
            ? $"error={Uri.EscapeDataString(error)}&state={Uri.EscapeDataString(state ?? string.Empty)}"
            : $"code={Uri.EscapeDataString(code ?? string.Empty)}&state={Uri.EscapeDataString(state ?? string.Empty)}";

        // Give the listener a moment to reach AcceptTcpClientAsync, then hit it.
        await Task.Delay(50);
        using var http = new HttpClient();
        using var response = await http.GetAsync($"{redirectUri}?{callbackQuery}");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    private static string? ExtractState(string authorizeUrl)
    {
        var uri = new Uri(authorizeUrl);
        return System.Web.HttpUtility.ParseQueryString(uri.Query)["state"];
    }

    private const string PkceUnreserved =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
}
