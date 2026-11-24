using System;
using System.Security.Cryptography;
using System.Text;

namespace TastileDesktop.Services;

/// <summary>
/// PKCE (RFC 7636) helpers for the Desktop Authorization Code + PKCE flow
/// (NF04-D01 / desktop#53). The wire contract is fixed by
/// tastile-web `docs/auth/desktop-auth-contract.md` (web#253): Desktop
/// reuses the CLI endpoints `/cli/authorize` + `/api/cli/token` with
/// `client_id=tastile-desktop` and a loopback `redirect_uri`.
/// </summary>
public static class Pkce
{
    /// <summary>Registered public client id on the web side.</summary>
    public const string ClientId = "tastile-desktop";

    /// <summary>Scopes requested at authorize time (registered set ∩ request).</summary>
    public const string Scopes = "tastile.read tastile.write";

    /// <summary>Loopback callback path the redirect_uri must end with.</summary>
    public const string CallbackPath = "/callback";

    /// <summary>How long the loopback listener waits for the browser redirect.</summary>
    public static readonly TimeSpan CallbackTimeout = TimeSpan.FromMinutes(5);

    private const string UnreservedChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    /// <summary>
    /// Generates a high-entropy code verifier (43–128 unreserved characters;
    /// 64 by default per RFC 7636 guidance).
    /// </summary>
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

    /// <summary>RFC 7636 S256 challenge: BASE64URL(SHA256(ASCII(verifier))).</summary>
    public static string ComputeS256Challenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    /// <summary>Opaque CSRF state value echoed back on the loopback redirect.</summary>
    public static string GenerateState()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Builds the <c>GET {web}/cli/authorize</c> URL. All seven parameters
    /// required by the web route are always emitted — including
    /// <c>scope</c>, which the web route rejects when absent even though the
    /// contract documents a registered-set default.
    /// </summary>
    public static string BuildAuthorizeUrl(
        string webBaseUrl,
        string redirectUri,
        string codeChallenge,
        string state)
    {
        if (string.IsNullOrWhiteSpace(webBaseUrl))
        {
            throw new ArgumentException("webBaseUrl must be non-empty", nameof(webBaseUrl));
        }

        var query = string.Join("&",
            "response_type=code",
            $"client_id={ClientId}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            $"scope={Uri.EscapeDataString(Scopes)}",
            $"state={Uri.EscapeDataString(state)}",
            $"code_challenge={Uri.EscapeDataString(codeChallenge)}",
            "code_challenge_method=S256");

        return $"{webBaseUrl.TrimEnd('/')}/cli/authorize?{query}";
    }

    internal static string Base64UrlEncode(ReadOnlySpan<byte> data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
