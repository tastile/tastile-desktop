using System;
using System.Security.Cryptography;
using System.Text;

namespace TastileDesktop.Services;

/// <summary>
/// PKCE (Proof Key for Code Exchange) helper for OAuth 2.0.
/// Required for secure authentication in public clients (desktop apps).
/// </summary>
public static class PkceHelper
{
    /// <summary>
    /// Generate a cryptographically random code verifier (43-128 chars).
    /// </summary>
    public static string GenerateCodeVerifier(int length = 128)
    {
        // RFC 7636: code verifier must be 43-128 chars of [A-Z] / [a-z] / [0-9] / "-" / "." / "_" / "~"
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        var bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        var sb = new StringBuilder(length);
        foreach (var b in bytes)
        {
            sb.Append(chars[b % chars.Length]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Generate code challenge from verifier using SHA-256 (S256 method).
    /// </summary>
    public static string GenerateCodeChallenge(string codeVerifier)
    {
        using (var sha256 = SHA256.Create())
        {
            var bytes = Encoding.ASCII.GetBytes(codeVerifier);
            var hash = sha256.ComputeHash(bytes);
            return Base64UrlEncode(hash);
        }
    }

    /// <summary>
    /// Base64 URL-safe encoding (no padding, no +/, replace with -_).
    /// </summary>
    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
