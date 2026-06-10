using System;
using System.Security.Cryptography;
using System.Text;

namespace TastileDesktop.Services;

/// <summary>
/// PKCE (RFC 7636) code-verifier / S256 code-challenge helpers for OAuth 2.0.
/// </summary>
public static class Pkce
{
    private const string UnreservedChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    public static string GenerateVerifier(int length = 64)
    {
        if (length is < 43 or > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(length),
                "PKCE verifier length must be between 43 and 128 characters.");
        }

        Span<char> buffer = length <= 256
            ? stackalloc char[length]
            : new char[length];

        for (var i = 0; i < length; i++)
        {
            buffer[i] = UnreservedChars[RandomNumberGenerator.GetInt32(UnreservedChars.Length)];
        }
        return new string(buffer);
    }

    public static string ComputeS256Challenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
