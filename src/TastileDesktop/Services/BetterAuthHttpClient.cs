using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace TastileDesktop.Services;

/// <summary>
/// HTTP wrapper for the BetterAuth endpoints exposed by
/// <c>tastile-web</c>'s <c>/api/auth/*</c> and <c>/api/mobile/api-token</c>.
///
/// Designed to be constructed once per process (registered as a singleton in DI)
/// and to keep the BetterAuth session token as the v1 API Bearer.
/// </summary>
public sealed class BetterAuthHttpClient
{
    private const string SessionCookieName = "better-auth.session_token";

    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public BetterAuthHttpClient(string baseUrl)
        : this(baseUrl, new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
    {
    }

    /// <summary>
    /// Internal constructor used by tests so a fake <see cref="HttpMessageHandler"/>
    /// can be injected.
    /// </summary>
    internal BetterAuthHttpClient(string baseUrl, HttpClient http)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("baseUrl must be non-empty", nameof(baseUrl));
        }

        _baseUrl = baseUrl.TrimEnd('/');
        _http = http;
        if (_http.BaseAddress is null && !string.IsNullOrEmpty(_baseUrl))
        {
            _http.BaseAddress = new Uri(_baseUrl + "/");
        }
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("TastileDesktop/0.4");
        }
    }

    /// <summary>
    /// <c>POST /api/auth/sign-in/email</c>. Returns the user payload from the
    /// response body and the <c>Set-Cookie</c> header that contains the
    /// BetterAuth session token.
    /// </summary>
    public async Task<SignInEmailResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("email is required", nameof(email));
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("password is required", nameof(password));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in/email")
        {
            Content = JsonContent.Create(new { email, password }),
        };
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var setCookie = ExtractSetCookie(response);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new BetterAuthHttpException(response.StatusCode, "sign_in_failed", TryParseErrorMessage(body));
        }

        var user = ParseSignInResponse(body);
        return new SignInEmailResult(user, setCookie);
    }

    /// <summary>
    /// <c>POST /api/auth/sign-up/email</c>. BetterAuth's sign-up may return
    /// either an auto-created session or a 4xx requesting email verification
    /// — callers should fall back to <see cref="SignInAsync"/> when the
    /// response is success-without-cookie.
    /// </summary>
    public async Task<SignUpEmailResult> SignUpAsync(string email, string password, string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("email is required", nameof(email));
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("password is required", nameof(password));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("name is required", nameof(name));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-up/email")
        {
            Content = JsonContent.Create(new { email, password, name }),
        };
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var setCookie = ExtractSetCookie(response);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new BetterAuthHttpException(response.StatusCode, "sign_up_failed", TryParseErrorMessage(body));
        }

        var user = ParseSignInResponse(body);
        return new SignUpEmailResult(user, setCookie);
    }

    /// <summary>
    /// <c>POST /api/auth/sign-out</c>. Best-effort; callers still clear the
    /// local store even when this fails.
    /// </summary>
    public async Task SignOutAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-out");
        if (!string.IsNullOrEmpty(sessionToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sessionToken);
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        // Treat 401 as "already signed out" and continue.
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Unauthorized)
        {
            throw new BetterAuthHttpException(response.StatusCode, "sign_out_failed", null);
        }
    }

    /// <summary>
    /// <c>GET /api/auth/session</c>. Returns the parsed session payload, or
    /// <c>null</c> when the session is no longer valid (HTTP 401).
    /// </summary>
    public async Task<BetterAuthSession?> GetSessionAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sessionToken);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new BetterAuthHttpException(response.StatusCode, "session_lookup_failed", TryParseErrorMessage(body));
        }

        return ParseSession(body, sessionToken);
    }

    /// <summary>
    /// <c>POST /api/mobile/api-token</c> with <c>Authorization: Bearer sessionToken</c>.
    /// Mints a long-lived v1 API token from the BetterAuth session cookie.
    /// </summary>
    public async Task<MobileApiTokenResult> MintApiTokenAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            throw new ArgumentException("sessionToken is required", nameof(sessionToken));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/mobile/api-token");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sessionToken);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new BetterAuthHttpException(response.StatusCode, "api_token_failed", TryParseErrorMessage(body));
        }

        var parsed = JsonSerializer.Deserialize<MobileApiTokenResponse>(body)
            ?? throw new BetterAuthHttpException(response.StatusCode, "api_token_failed", "Empty response body");
        var token = parsed.Token ?? parsed.ApiToken
            ?? throw new BetterAuthHttpException(response.StatusCode, "api_token_failed", "Response missing token");
        return new MobileApiTokenResult(token, parsed.TokenId, parsed.ExpiresAt);
    }

    private static string ExtractSetCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return string.Empty;
        }

        foreach (var value in values)
        {
            var separator = value.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var name = value[..separator].Trim();
            var cookieName = StripCookiePrefix(name);
            if (string.Equals(cookieName, SessionCookieName, StringComparison.OrdinalIgnoreCase))
            {
                var raw = value[(separator + 1)..];
                var semi = raw.IndexOf(';');
                return semi >= 0 ? raw[..semi] : raw;
            }
        }

        return string.Empty;
    }

    // RFC 6265 §4.1.3 / §5.3 cookie name prefixes. Production BetterAuth
    // deployments behind TLS pin the cookie with `__Secure-` (and sometimes
    // `__Host-`); both must resolve to the same `SessionCookieName` match.
    private static string StripCookiePrefix(string name)
    {
        foreach (var prefix in CookieNamePrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return name[prefix.Length..];
            }
        }

        return name;
    }

    private static readonly string[] CookieNamePrefixes = ["__Secure-", "__Host-"];

    private static BetterAuthUser? ParseSignInResponse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("user", out var userElement))
            {
                return ParseUser(userElement);
            }

            // The whole body IS the user object (BetterAuth direct shape).
            return ParseUser(root);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static BetterAuthUser? ParseUser(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = element.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;
        var email = element.TryGetProperty("email", out var emailEl) ? emailEl.GetString() : null;
        var name = element.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
        var emailVerified = element.TryGetProperty("emailVerified", out var evEl)
            && evEl.ValueKind == JsonValueKind.True;
        return new BetterAuthUser(id, email, name, emailVerified);
    }

    private static BetterAuthSession? ParseSession(string body, string fallbackToken)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var user = root.TryGetProperty("user", out var userEl) ? ParseUser(userEl) : null;
            var sessionEl = root.TryGetProperty("session", out var s) ? s : root;
            var sessionToken = sessionEl.TryGetProperty("token", out var tokenEl)
                ? tokenEl.GetString() ?? fallbackToken
                : fallbackToken;
            var userId = user?.Id;
            if (string.IsNullOrEmpty(userId) && sessionEl.TryGetProperty("userId", out var uidEl))
            {
                userId = uidEl.ToString();
            }
            var email = user?.Email;
            var expiresAt = sessionEl.TryGetProperty("expiresAt", out var expEl)
                ? TryParseEpoch(expEl)
                : DateTimeOffset.UtcNow.AddHours(24);
            return new BetterAuthSession(
                SessionToken: sessionToken ?? fallbackToken,
                UserId: userId ?? string.Empty,
                Email: email,
                ExpiresAtEpochSeconds: expiresAt.ToUnixTimeSeconds());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTimeOffset TryParseEpoch(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        if (element.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(element.GetString(), out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.UtcNow.AddHours(24);
    }

    private static string? TryParseErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return body;
            }

            var root = doc.RootElement;
            if (root.TryGetProperty("message", out var messageEl))
            {
                return messageEl.GetString() ?? body;
            }

            if (root.TryGetProperty("error", out var errorEl))
            {
                if (errorEl.ValueKind == JsonValueKind.String)
                {
                    return errorEl.GetString();
                }

                return errorEl.ToString();
            }
        }
        catch (JsonException)
        {
        }

        return body;
    }
}

public sealed record SignInEmailResult(BetterAuthUser? User, string SetCookie);
public sealed record SignUpEmailResult(BetterAuthUser? User, string SetCookie);
public sealed record MobileApiTokenResult(string Token, string? TokenId, DateTimeOffset? ExpiresAt);

public sealed record BetterAuthUser(string? Id, string? Email, string? Name, bool EmailVerified);

public sealed record BetterAuthSession(
    string SessionToken,
    string UserId,
    string? Email,
    long ExpiresAtEpochSeconds);

public sealed class BetterAuthHttpException : Exception
{
    public BetterAuthHttpException(HttpStatusCode statusCode, string code, string? detail)
        : base(BuildMessage(code, detail))
    {
        StatusCode = statusCode;
        ErrorCode = code;
        Detail = detail;
    }

    public HttpStatusCode StatusCode { get; }
    public string ErrorCode { get; }
    public string? Detail { get; }

    private static string BuildMessage(string code, string? detail) =>
        string.IsNullOrEmpty(detail) ? code : $"{code}: {detail}";
}

internal sealed class MobileApiTokenResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    [JsonPropertyName("api_token")]
    public string? ApiToken { get; init; }

    [JsonPropertyName("token_id")]
    public string? TokenId { get; init; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; init; }
}
