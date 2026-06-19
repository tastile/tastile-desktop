using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// Manages the Cognito PKCE flow: opens Tastile's web sign-in surface,
/// then exchanges the returned Cognito authorization code.
///
/// State of the in-flight authorization-code exchange is held in
/// <see cref="_pending"/> until the desktop's <c>tastile://auth/callback</c>
/// handler resolves it.
/// </summary>
public sealed class CognitoAuthService
{
    public static CognitoAuthService Instance { get; } = new(new SecureTokenStore());

    private readonly ITokenStore _store;
    private TastileDesktop.Models.AuthSession? _current;
    private PendingFlow? _pending;

    public TastileDesktop.Models.AuthSession? CurrentSession => _current;

    public bool IsAuthenticated =>
        _current is { } s && s.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(-30);

    public event EventHandler? AuthStateChanged;

    public CognitoAuthService(ITokenStore store)
    {
        _store = store;
    }

    /// <summary>Hydrate <see cref="CurrentSession"/> from <see cref="ITokenStore"/>.</summary>
    public async Task<TastileDesktop.Models.AuthSession?> TryLoadFromStoreAsync()
    {
        _current = await _store.LoadAsync().ConfigureAwait(false);
        if (_current is not null)
        {
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
        }
        return _current;
    }

    /// <summary>
    /// Begin the web sign-in flow. Generates a PKCE pair and CSRF state, opens
    /// the system browser, and registers a <see cref="TaskCompletionSource"/>
    /// that <see cref="HandleAuthorizationCodeAsync"/> will signal on callback.
    /// </summary>
    public Task<AuthResult> StartHostedUiAsync()
    {
        var cfg = AppSettings.Cognito;
        var verifier = Pkce.GenerateVerifier();
        var challenge = Pkce.ComputeS256Challenge(verifier);
        var state = Convert.ToHexString(CryptoRandomBytes(16));

        var authUrl = BuildWebLoginUrl(cfg, challenge, state);

        var tcs = new TaskCompletionSource<AuthResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = new PendingFlow(state, verifier, cfg.CallbackUrl, tcs);

        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(authUrl));
        return tcs.Task;
    }

    internal static string BuildWebLoginUrl(CognitoConfig cfg, string codeChallenge, string state)
    {
        var separator = cfg.WebLoginUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{cfg.WebLoginUrl}{separator}" +
            $"redirect_uri={Uri.EscapeDataString(cfg.CallbackUrl)}" +
            $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
            $"&state={Uri.EscapeDataString(state)}";
    }

    public async Task<AuthResult> HandleAuthorizationCodeAsync(string code, string state)
    {
        if (_pending is not { } p)
        {
            return new AuthResult(false, "no_pending_flow");
        }
        if (!string.Equals(p.State, state, StringComparison.Ordinal))
        {
            return new AuthResult(false, "state_mismatch");
        }

        try
        {
            var cfg = AppSettings.Cognito;
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = cfg.ClientId,
                ["code"] = code,
                ["redirect_uri"] = p.RedirectUri,
                ["code_verifier"] = p.CodeVerifier,
            });
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var resp = await http.PostAsync($"{cfg.HostedUiBaseUrl}/oauth2/token", form).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                p.Tcs.TrySetResult(new AuthResult(false, $"token_exchange_failed: {resp.StatusCode}"));
                _pending = null;
                return new AuthResult(false, $"token_exchange_failed: {resp.StatusCode}");
            }

            var token = JsonSerializer.Deserialize<TokenResponse>(body)
                ?? throw new InvalidOperationException("token response was empty");
            var (sub, email, exp) = JwtClaims.ParseIdToken(token.IdToken);

            _current = new TastileDesktop.Models.AuthSession(
                IdToken: token.IdToken,
                AccessToken: token.AccessToken ?? string.Empty,
                RefreshToken: token.RefreshToken ?? string.Empty,
                Sub: sub,
                Email: email,
                ExpiresAt: DateTimeOffset.FromUnixTimeSeconds(exp));

            await _store.SaveAsync(_current).ConfigureAwait(false);
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
            var ok = new AuthResult(true);
            p.Tcs.TrySetResult(ok);
            _pending = null;
            return ok;
        }
        catch (Exception ex)
        {
            p.Tcs.TrySetResult(new AuthResult(false, ex.Message));
            _pending = null;
            return new AuthResult(false, ex.Message);
        }
    }

    public async Task<AuthResult> HandleTokenCallbackAsync(
        string idToken,
        string accessToken,
        string refreshToken,
        int expiresIn,
        string state)
    {
        if (_pending is not { } p)
        {
            System.Diagnostics.Debug.WriteLine($"HandleTokenCallbackAsync: _pending is null, rejecting");
            return new AuthResult(false, "no_pending_flow");
        }
        if (!string.Equals(p.State, state, StringComparison.Ordinal))
        {
            System.Diagnostics.Debug.WriteLine($"HandleTokenCallbackAsync: state mismatch expected={p.State[..8]}... got={state[..8]}...");
            return new AuthResult(false, "state_mismatch");
        }
        System.Diagnostics.Debug.WriteLine($"HandleTokenCallbackAsync: state OK, processing token");

        try
        {
            var (sub, email, exp) = JwtClaims.ParseIdToken(idToken);
            var expiresAt = exp > 0
                ? DateTimeOffset.FromUnixTimeSeconds(exp)
                : DateTimeOffset.UtcNow.AddSeconds(Math.Max(expiresIn, 60));
            _current = new TastileDesktop.Models.AuthSession(
                IdToken: idToken,
                AccessToken: accessToken,
                RefreshToken: refreshToken,
                Sub: sub,
                Email: email,
                ExpiresAt: expiresAt);

            await _store.SaveAsync(_current).ConfigureAwait(false);
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
            var ok = new AuthResult(true);
            p.Tcs.TrySetResult(ok);
            _pending = null;
            return ok;
        }
        catch (Exception ex)
        {
            p.Tcs.TrySetResult(new AuthResult(false, ex.Message));
            _pending = null;
            return new AuthResult(false, ex.Message);
        }
    }

    public async Task<TastileDesktop.Models.AuthSession?> RefreshAsync()
    {
        if (_current is not { RefreshToken: { Length: > 0 } rt })
        {
            return null;
        }

        try
        {
            var cfg = AppSettings.Cognito;
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = cfg.ClientId,
                ["refresh_token"] = rt,
            });
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var resp = await http.PostAsync($"{cfg.HostedUiBaseUrl}/oauth2/token", form).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var token = JsonSerializer.Deserialize<TokenResponse>(body);
            if (token is null || string.IsNullOrEmpty(token.IdToken))
            {
                return null;
            }
            var (sub, email, exp) = JwtClaims.ParseIdToken(token.IdToken);

            _current = new TastileDesktop.Models.AuthSession(
                IdToken: token.IdToken,
                AccessToken: token.AccessToken ?? string.Empty,
                RefreshToken: token.RefreshToken ?? rt,
                Sub: sub,
                Email: email,
                ExpiresAt: DateTimeOffset.FromUnixTimeSeconds(exp));

            await _store.SaveAsync(_current).ConfigureAwait(false);
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
            return _current;
        }
        catch
        {
            return null;
        }
    }

    public async Task SignOutAsync()
    {
        try
        {
            await _store.ClearAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best effort.
        }

        var cfg = AppSettings.Cognito;
        _current = null;
        _pending = null;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);

        // Hit Cognito's logout endpoint to clear any Hosted UI cookie.
        var logoutUrl = $"{cfg.HostedUiBaseUrl}/logout?" +
            $"client_id={Uri.EscapeDataString(cfg.ClientId)}" +
            $"&logout_uri={Uri.EscapeDataString(cfg.CallbackUrl)}";
        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(logoutUrl));
    }

    private static byte[] CryptoRandomBytes(int length)
    {
        var b = new byte[length];
        System.Security.Cryptography.RandomNumberGenerator.Fill(b);
        return b;
    }

    private sealed record PendingFlow(
        string State,
        string CodeVerifier,
        string RedirectUri,
        TaskCompletionSource<AuthResult> Tcs);

    private sealed record TokenResponse(
        [property: JsonPropertyName("id_token")] string IdToken,
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn,
        [property: JsonPropertyName("token_type")] string? TokenType);
}

public sealed record AuthResult(bool Success, string? ErrorCode = null);
