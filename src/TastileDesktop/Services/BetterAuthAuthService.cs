using System;
using System.Net.Http;
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
    {
        _store = store;
        _client = client;
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
    public async Task<AuthResult> SignInWithEmailAsync(string email, string password)
    {
        try
        {
            var signIn = await _client.SignInAsync(email, password).ConfigureAwait(false);
            var sessionToken = ExtractSessionToken(signIn.SetCookie, signIn.User);
            if (string.IsNullOrEmpty(sessionToken))
            {
                return new AuthResult(false, "missing_session_cookie", null);
            }

            var apiTokenResult = await _client.MintApiTokenAsync(sessionToken).ConfigureAwait(false);
            var userId = signIn.User?.Id ?? string.Empty;
            var emailClaim = signIn.User?.Email ?? email;
            var expiresAt = apiTokenResult.ExpiresAt ?? DateTimeOffset.UtcNow.AddHours(24);

            _current = new Models.AuthSession(
                SessionToken: sessionToken,
                ApiToken: apiTokenResult.Token,
                UserId: userId,
                Email: emailClaim,
                ExpiresAt: expiresAt);

            await _store.SaveAsync(_current).ConfigureAwait(false);
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
            return new AuthResult(true);
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
    /// Sign up with email + password + display name. BetterAuth typically
    /// returns the user payload without a usable session cookie (the account
    /// must be verified first), so on success we immediately try to sign in
    /// with the same credentials. If sign-up fails because the account
    /// already exists, fall back to sign-in.
    /// </summary>
    public async Task<AuthResult> SignUpWithEmailAsync(string email, string password, string name)
    {
        try
        {
            var signUp = await _client.SignUpAsync(email, password, name).ConfigureAwait(false);
            var sessionToken = ExtractSessionToken(signUp.SetCookie, signUp.User);
            if (!string.IsNullOrEmpty(sessionToken))
            {
                var apiTokenResult = await _client.MintApiTokenAsync(sessionToken).ConfigureAwait(false);
                _current = new Models.AuthSession(
                    SessionToken: sessionToken,
                    ApiToken: apiTokenResult.Token,
                    UserId: signUp.User?.Id ?? string.Empty,
                    Email: signUp.User?.Email ?? email,
                    ExpiresAt: apiTokenResult.ExpiresAt ?? DateTimeOffset.UtcNow.AddHours(24));
                await _store.SaveAsync(_current).ConfigureAwait(false);
                AuthStateChanged?.Invoke(this, EventArgs.Empty);
                return new AuthResult(true);
            }
        }
        catch (BetterAuthHttpException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // Account already exists → fall through to sign-in.
        }
        catch (BetterAuthHttpException ex)
        {
            return new AuthResult(false, ex.ErrorCode, ex.Detail);
        }

        return await SignInWithEmailAsync(email, password).ConfigureAwait(false);
    }

    public async Task SignOutAsync()
    {
        var previous = _current;
        _current = null;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);

        if (previous is not null)
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
    /// Returns the BetterAuth session token for use as a Bearer on v1 API
    /// requests. The long-lived API token is also stored in the session but
    /// v1 currently expects the session token per the mobile-bridge contract.
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
