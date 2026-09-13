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
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);
