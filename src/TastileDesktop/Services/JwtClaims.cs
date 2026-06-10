using System;
using System.Text.Json;

namespace TastileDesktop.Services;

/// <summary>
/// Minimal Cognito id_token claim parser. Does NOT verify signatures;
/// signature validation is performed by the daemon. Used only to extract
/// <c>sub</c>, <c>email</c>, and <c>exp</c> for local session state.
/// </summary>
public static class JwtClaims
{
    public static (string Sub, string? Email, long Exp) ParseIdToken(string idToken)
    {
        if (string.IsNullOrEmpty(idToken))
        {
            throw new FormatException("id_token is empty.");
        }

        var parts = idToken.Split('.');
        if (parts.Length < 2)
        {
            throw new FormatException("id_token is not a valid JWT (expected at least 2 segments).");
        }

        using var doc = JsonDocument.Parse(Base64UrlDecode(parts[1]));
        var root = doc.RootElement;

        var sub = root.GetProperty("sub").GetString()
            ?? throw new FormatException("id_token is missing required 'sub' claim.");
        var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
        var exp = root.GetProperty("exp").GetInt64();

        return (sub, email, exp);
    }

    private static string Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
