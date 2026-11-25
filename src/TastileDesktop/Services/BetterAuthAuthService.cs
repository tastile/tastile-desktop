using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// Manages the native BetterAuth email/password sign-in flow for the desktop
/// client. Replaces the Cognito PKCE + Hosted UI flow that lived here until
/// the Cognito → BetterAuth migration (ADR 2026-08-22).
///
/// Sign-in flow:
///   1. <c>POST {WebBaseUrl}/api/auth/sign-in/email</c> with email + password.
///   2. Extract the <c>better-auth.session_token</c> cookie from the response.
///   3. <c>POST {WebBaseUrl}/api/mobile/api-token</c> with the session token
///      as Bearer to mint a long-lived v1 API token.
///   4. Persist both tokens to DPAPI-protected <see cref="SecureTokenStore"/>.
///
/// Sign-out flow:
///   1. <c>POST {WebBaseUrl}/api/auth/sign-out</c> (best effort).
///   2. Clear <see cref="SecureTokenStore"/>.
///   3. Reset <see cref="CurrentSession"/> to <c>null</c> and raise
///      <see cref="AuthStateChanged"/>.
///
/// Social sign-in (Google/Apple) is not implemented natively yet; callers
/// continue to open the web browser at <c>{WebBaseUrl}/login?provider=...</c>
/// and rely on the redirect-to-desktop follow-up to be implemented in
/// <c>tastile-web</c>. See ADR 2026-08-22 for the social sign-in roadmap.
/// </summary>
public sealed class BetterAuthAuthService
{
    public static BetterAuthAuthService Instance { get; } = new(new SecureTokenStore());

    private readonly ITokenStore _store;
    private readonly BetterAuthHttpClient _client;
    private readonly Func<HttpClient>? _coreHttpFactory;
    private Models.AuthSession? _current;

    public Models.AuthSession? CurrentSession => _current;

    public bool IsAuthenticated =>
        _current is { SessionToken: { Length: > 0 } };

    public event EventHandler? AuthStateChanged;

    public BetterAuthAuthService(ITokenStore store)
        : this(store, new BetterAuthHttpClient(AppSettings.WebBaseUrl))
    {
    }

    internal BetterAuthAuthService(ITokenStore store, BetterAuthHttpClient client)
        : this(store, client, coreHttpFactory: null)
    {
    }

    /// <summary>
    /// <paramref name="coreHttpFactory"/> supplies the HttpClient used for
    /// Core-side PKCE token lifecycle calls (probe / signout). Tests inject a
    /// stub-backed client; production uses <see cref="AppSettings.ApiBaseUrl"/>.
    /// The factory must return a usable client per call.
    /// </summary>
    internal BetterAuthAuthService(ITokenStore store, BetterAuthHttpClient client, Func<HttpClient>? coreHttpFactory)
    {
        _store = store;
        _client = client;
        _coreHttpFactory = coreHttpFactory;
    }

    /// <summary>Hydrate <see cref="CurrentSession"/> from <see cref="ITokenStore"/>.</summary>
    public async Task<Models.AuthSession?> TryLoadFromStoreAsync()
    {
        _current = await _store.LoadAsync().ConfigureAwait(false);
        if (_current is not null)
        {
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
        }
        return _current;
    }

    /// <summary>
    /// Sign in with email + password via BetterAuth's native endpoint.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the in-flight HTTP request. <c>ThrowIfCancellationRequested</c>
    /// is also checked before persisting the session, so a caller that has
    /// already discarded the sign-in (for example the auth window closing
    /// after the user pressed Cancel) cannot end up with the user signed in
    /// against their intent.
    /// </param>
    public async Task<AuthResult> SignInWithEmailAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var signIn = await _client.SignInAsync(email, password, cancellationToken).ConfigureAwait(false);
            var sessionToken = ExtractSessionToken(signIn.SetCookie, signIn.User);
            if (string.IsNullOrEmpty(sessionToken))
            {
                return new AuthResult(false, "missing_session_cookie", null);
            }

            var apiTokenResult = await _client.MintApiTokenAsync(sessionToken, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var userId = signIn.User?.Id ?? string.Empty;
            var emailClaim = signIn.User?.Email ?? email;
            var expiresAt = apiTokenResult.ExpiresAt ?? DateTimeOffset.UtcNow.AddHours(24);

            _current = new Models.AuthSession(
                SessionToken: sessionToken,
                ApiToken: apiTokenResult.Token,
                UserId: userId,
                Email: emailClaim,
                ExpiresAt: expiresAt)
            {
                Flow = AuthFlows.Password,
            };

            await _store.SaveAsync(_current).ConfigureAwait(false);
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
            return new AuthResult(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller cancelled (e.g. the auth window closed after Cancel).
            // _current is intentionally NOT assigned: we must not leave the
            // app authenticated against the user's stated intent.
            throw;
        }
        catch (BetterAuthHttpException ex)
        {
            return new AuthResult(false, ex.ErrorCode, ex.Detail);
        }
        catch (Exception ex)
        {
            return new AuthResult(false, ex.Message, null);
        }
    }

    /// <summary>
    /// NF04-D01: system-browser Authorization Code + PKCE sign-in. Opens
    /// <c>{WebBaseUrl}/cli/authorize?client_id=tastile-desktop</c> in the
    /// default browser, receives the code on a loopback
    /// <c>http://127.0.0.1:&lt;port&gt;/callback</c> listener, exchanges it at
    /// <c>POST /api/cli/token</c>, and persists the scoped Core API token
    /// (<c>tastile.read</c> + <c>tastile.write</c>) in the DPAPI store.
    ///
    /// The minted token IS the v1 Bearer credential: Core's
    /// <c>authenticate_v2</c> resolves it from <c>v1_api_token</c> and
    /// enforces its scopes, so <see cref="GetAccessTokenAsync"/> returns it
    /// unchanged for PKCE sessions. There is no refresh token — renewal is
    /// a fresh PKCE round-trip (contract §3).
    /// </summary>
    public Task<AuthResult> SignInWithBrowserAsync(CancellationToken cancellationToken = default) =>
        SignInWithBrowserAsync(LaunchSystemBrowserAsync, cancellationToken);

    /// <summary>
    /// Core of <see cref="SignInWithBrowserAsync(CancellationToken)"/> with an
    /// injectable browser launcher and web base URL so tests can drive the
    /// loopback callback without spawning a real browser or depending on
    /// process environment variables.
    /// </summary>
    internal async Task<AuthResult> SignInWithBrowserAsync(
        Func<string, Task> launchBrowserAsync,
        CancellationToken cancellationToken = default,
        string? webBaseUrl = null)
    {
        var verifier = Pkce.GenerateVerifier();
        var challenge = Pkce.ComputeS256Challenge(verifier);
        var state = Pkce.GenerateState();

        LoopbackCallbackListener listener;
        string authorizeUrl;
        try
        {
            listener = new LoopbackCallbackListener();
            authorizeUrl = Pkce.BuildAuthorizeUrl(
                webBaseUrl ?? AppSettings.WebBaseUrl,
                listener.RedirectUri,
                challenge,
                state);
        }
        catch (Exception ex)
        {
            return new AuthResult(false, "pkce_setup_failed", ex.Message);
        }

        await using (listener)
        {

            try
            {
                await launchBrowserAsync(authorizeUrl).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new AuthResult(false, "browser_launch_failed", ex.Message);
            }

            LoopbackCallback callback;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(Pkce.CallbackTimeout);
            try
            {
                callback = await listener.WaitForCallbackAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new AuthResult(false, "authorization_timeout", null);
            }

            if (!string.Equals(callback.State, state, StringComparison.Ordinal))
            {
                return new AuthResult(false, "state_mismatch", null);
            }

            if (!string.IsNullOrEmpty(callback.Error))
            {
                return new AuthResult(false, "authorization_denied", callback.ErrorDescription ?? callback.Error);
            }

            if (string.IsNullOrEmpty(callback.Code))
            {
                return new AuthResult(false, "missing_code", null);
            }

            CliTokenResult token;
            try
            {
                token = await _client.ExchangeCliTokenAsync(
                    callback.Code,
                    verifier,
                    listener.RedirectUri,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (BetterAuthHttpException ex)
            {
                return new AuthResult(false, ex.ErrorCode, ex.Detail);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Caller cancelled (window closed / Cancel pressed): propagate so
                // the session is never persisted against the user's intent.
                throw;
            }
            catch (Exception ex)
            {
                return new AuthResult(false, ex.Message, null);
            }

            if (string.IsNullOrEmpty(token.Subject))
            {
                return new AuthResult(false, "missing_subject", null);
            }

            cancellationToken.ThrowIfCancellationRequested();

            _current = new Models.AuthSession(
                SessionToken: token.Token,
                ApiToken: token.Token,
                UserId: token.Subject,
                Email: null,
                ExpiresAt: token.ExpiresAt ?? DateTimeOffset.UtcNow.AddDays(30))
            {
                Flow = AuthFlows.Pkce,
            };

            await _store.SaveAsync(_current).ConfigureAwait(false);
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
            return new AuthResult(true);
        }
    }

    private static Task LaunchSystemBrowserAsync(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sign up with email + password + display name. BetterAuth typically
    /// returns the user payload without a usable session cookie (the account
    /// must be verified first), so on success we immediately try to sign in
    /// with the same credentials. If sign-up fails because the account
    /// already exists, fall back to sign-in.
    /// </summary>
    public async Task<AuthResult> SignUpWithEmailAsync(string email, string password, string name, CancellationToken cancellationToken = default)
    {
        try
        {
            var signUp = await _client.SignUpAsync(email, password, name, cancellationToken).ConfigureAwait(false);
            var sessionToken = ExtractSessionToken(signUp.SetCookie, signUp.User);
            if (!string.IsNullOrEmpty(sessionToken))
            {
                var apiTokenResult = await _client.MintApiTokenAsync(sessionToken, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                _current = new Models.AuthSession(
                    SessionToken: sessionToken,
                    ApiToken: apiTokenResult.Token,
                    UserId: signUp.User?.Id ?? string.Empty,
                    Email: signUp.User?.Email ?? email,
                    ExpiresAt: apiTokenResult.ExpiresAt ?? DateTimeOffset.UtcNow.AddHours(24))
                {
                    Flow = AuthFlows.Password,
                };
                await _store.SaveAsync(_current).ConfigureAwait(false);
                AuthStateChanged?.Invoke(this, EventArgs.Empty);
                return new AuthResult(true);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (BetterAuthHttpException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // Account already exists → fall through to sign-in.
        }
        catch (BetterAuthHttpException ex)
        {
            return new AuthResult(false, ex.ErrorCode, ex.Detail);
        }

        return await SignInWithEmailAsync(email, password, cancellationToken).ConfigureAwait(false);
    }

    public async Task SignOutAsync()
    {
        var previous = _current;
        _current = null;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);

        if (previous is not null)
        {
            if (AuthFlows.EffectiveFlow(previous) == AuthFlows.Pkce)
            {
                // PKCE sessions hold a scoped Core API token, not a BetterAuth
                // web session. Revocation is Core-side: POST /v1/auth/signout
                // with the token as Bearer revokes the calling credential
                // itself (v1/14 §14.5 Special row; contract §3-1).
                await TryRevokeCoreTokenAsync(previous.ApiToken).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    await _client.SignOutAsync(previous.SessionToken).ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort: server-side session may already be invalid.
                }
            }
        }

        try
        {
            await _store.ClearAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best effort: keep going even when the store is already empty.
        }
    }

    /// <summary>
    /// Validates the current BetterAuth session against <c>GET /api/auth/session</c>
    /// so the <see cref="CoreApiClient"/> 401-retry path can either reuse the
    /// token (transient server-side hiccup) or fall back to a clean sign-out
    /// (server truly says the session is dead). On 401 the local session and
    /// the DPAPI store are cleared; on 5xx / network errors the existing
    /// session is preserved so transient failures do not kick the user out.
    /// </summary>
    public async Task<Models.AuthSession?> RefreshAsync()
    {
        if (_current is null)
        {
            return null;
        }

        if (AuthFlows.EffectiveFlow(_current) == AuthFlows.Pkce)
        {
            // PKCE sessions carry a Core API token, not a BetterAuth web
            // session, so /api/auth/session cannot validate them. Probe Core
            // with a cheap read-scope request instead: a hard 401/403 means
            // the token was revoked or expired (re-auth required); success
            // or transient failure keeps the session.
            var probe = await ProbeCoreTokenAsync(_current.ApiToken).ConfigureAwait(false);
            if (probe == false)
            {
                _current = null;
                AuthStateChanged?.Invoke(this, EventArgs.Empty);
                try
                {
                    await _store.ClearAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort: in-memory state is already cleared.
                }

                return null;
            }

            return _current;
        }

        BetterAuthSession? validated;
        try
        {
            validated = await _client.GetSessionAsync(_current.SessionToken).ConfigureAwait(false);
        }
        catch (BetterAuthHttpException)
        {
            // 5xx or other server-side failure: keep the local session so a
            // transient backend blip does not lock the user out.
            return _current;
        }
        catch (HttpRequestException)
        {
            // Network unreachable, DNS failure, TLS error, etc.
            return _current;
        }
        catch (OperationCanceledException)
        {
            // BetterAuthHttpClient uses an HTTP timeout. Preserve the current
            // session on timeout so CoreApiClient keeps its guarded retry path.
            return _current;
        }

        if (validated is null)
        {
            // Server says the session is no longer valid. Drop the local state
            // so the UI moves to the unauthenticated surface and the next
            // CoreApiClient call does not retry forever with a dead token.
            _current = null;
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
            try
            {
                await _store.ClearAsync().ConfigureAwait(false);
            }
            catch
            {
                // Best-effort: the in-memory state is already cleared and the
                // DPAPI store will be re-written on the next sign-in.
            }

            return null;
        }

        return _current;
    }

    /// <summary>
    /// Best-effort revocation of a PKCE Core API token via
    /// <c>POST {ApiBaseUrl}/v1/auth/signout</c>. Failures are swallowed —
    /// local sign-out must never be blocked by the network.
    /// </summary>
    private async Task TryRevokeCoreTokenAsync(string token)
    {
        try
        {
            var (http, ownsClient) = CreateCoreHttpClient();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/signout");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await http.SendAsync(request).ConfigureAwait(false);
            }
            finally
            {
                if (ownsClient)
                {
                    http.Dispose();
                }
            }
        }
        catch
        {
            // Best-effort: the token may already be revoked or expired.
        }
    }

    /// <summary>
    /// Probes a PKCE Core API token with <c>GET /v1/tiles?limit=1</c>.
    /// Returns <c>true</c> when Core accepts the token, <c>false</c> on a
    /// hard 401/403 (revoked or expired → re-auth required), and
    /// <c>null</c> on transient failures where the session must be kept.
    /// </summary>
    private async Task<bool?> ProbeCoreTokenAsync(string token)
    {
        try
        {
            var (http, ownsClient) = CreateCoreHttpClient();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/tiles?limit=1");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await http.SendAsync(request).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                return response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? false
                    : null;
            }
            finally
            {
                if (ownsClient)
                {
                    http.Dispose();
                }
            }
        }
        catch
        {
            // Network unreachable, DNS failure, TLS error, etc. — keep the
            // session so a transient backend blip does not lock the user out.
            return null;
        }
    }

    private (HttpClient Http, bool OwnsClient) CreateCoreHttpClient()
    {
        if (_coreHttpFactory is not null)
        {
            return (_coreHttpFactory(), false);
        }

        return (new HttpClient
        {
            BaseAddress = new Uri(AppSettings.ApiBaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(15),
        }, true);
    }

    /// <summary>
    /// Returns the credential to use as a Bearer on v1 API requests.
    /// Password-flow sessions return the BetterAuth session token (the
    /// mobile-bridge contract); PKCE sessions return the scoped Core API
    /// token minted by <c>/api/cli/token</c>.
    /// </summary>
    public Task<string?> GetAccessTokenAsync()
    {
        if (_current is { SessionToken: { Length: > 0 } token })
        {
            return Task.FromResult<string?>(token);
        }

        return Task.FromResult<string?>(null);
    }

    private static string ExtractSessionToken(string setCookie, BetterAuthUser? user)
    {
        if (!string.IsNullOrEmpty(setCookie))
        {
            return setCookie;
        }

        // Defensive fallback: BetterAuth sometimes returns the token directly
        // in the response body (newer bearer plugin shapes). We don't accept
        // that for the MVP and require the cookie path; the call to
        // MintApiTokenAsync will fail loudly if we got nothing usable.
        return string.Empty;
    }
}

public sealed record AuthResult(bool Success, string? ErrorCode = null, string? Detail = null);
