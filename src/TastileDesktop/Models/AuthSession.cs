using System;
using System.Text.Json.Serialization;

namespace TastileDesktop.Models;

/// <summary>
/// Cognito-derived session. Persisted via <c>SecureTokenStore</c> (DPAPI).
/// Distinct from the legacy tastile-daemon AuthSession type in
/// <c>TastileDesktop.Services</c> (deleted in Phase 1).
/// </summary>
public sealed record AuthSession(
    [property: JsonPropertyName("id_token")] string IdToken,
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("sub")] string Sub,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);
