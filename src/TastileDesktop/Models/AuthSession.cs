using System;
using System.Text.Json.Serialization;

namespace TastileDesktop.Models;

/// <summary>
/// BetterAuth-derived session. Persisted via <c>SecureTokenStore</c> (DPAPI).
///
/// Contains the BetterAuth <c>session_token</c> (cookie value used as the v1
/// API Bearer) and the long-lived <c>api_token</c> minted by
/// <c>POST /api/mobile/api-token</c> on the web side.
///
/// Distinct from the legacy Cognito-shaped session that lived here until the
/// Cognito → BetterAuth migration (ADR 2026-08-22). The previous fields
/// (<c>IdToken</c>, <c>AccessToken</c>, <c>RefreshToken</c>, <c>Sub</c>) were
/// replaced with the BetterAuth shape.
/// </summary>
public sealed record AuthSession(
    [property: JsonPropertyName("session_token")] string SessionToken,
    [property: JsonPropertyName("api_token")] string ApiToken,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt)
{
    /// <summary>
    /// Which sign-in produced this session: <see cref="AuthFlows.Password"/>
    /// (BetterAuth email/password, SessionToken is the web session cookie
    /// token) or <see cref="AuthFlows.Pkce"/> (browser Authorization Code +
    /// PKCE, both token fields carry the scoped Core API token minted by
    /// <c>POST /api/cli/token</c>). <c>null</c> on credentials written before
    /// the PKCE flow existed — treated as <see cref="AuthFlows.Password"/>.
    /// </summary>
    [property: JsonPropertyName("flow")]
    public string? Flow { get; init; }
}

/// <summary>Values for <see cref="AuthSession.Flow"/>.</summary>
public static class AuthFlows
{
    /// <summary>BetterAuth email/password sign-in via <c>/api/auth/sign-in/email</c>.</summary>
    public const string Password = "password";

    /// <summary>
    /// System-browser Authorization Code + PKCE (NF04-D01). The session
    /// carries a Core API token with <c>tastile.read</c>/<c>tastile.write</c>
    /// scopes instead of a BetterAuth web session.
    /// </summary>
    public const string Pkce = "pkce";

    /// <summary>Normalizes a stored <see cref="AuthSession.Flow"/> value.</summary>
    public static string EffectiveFlow(AuthSession? session) =>
        session?.Flow ?? Password;
}
