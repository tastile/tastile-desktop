using System;

namespace TastileDesktop.Services;

/// <summary>
/// Process-wide configuration sourced from environment variables.
/// Replaces the daemon-coupled <c>RuntimeProfile</c>.
/// </summary>
public static class AppSettings
{
    public static string WebAccountUrl
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable("TASTILE_WEB_ACCOUNT_URL")?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                throw new InvalidOperationException(
                    "Missing environment variable TASTILE_WEB_ACCOUNT_URL — please set it before running. See .env.example for the contract.");
            }

            RequireHttpsForRemoteUrl(raw, "TASTILE_WEB_ACCOUNT_URL");
            return raw;
        }
    }

    public static string ApiBaseUrl
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable("TASTILE_API_BASE_URL")?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                throw new InvalidOperationException(
                    "Missing environment variable TASTILE_API_BASE_URL — please set it before running. See .env.example for the contract.");
            }

            var trimmed = raw.TrimEnd('/');
            RequireHttpsForRemoteUrl(trimmed, "TASTILE_API_BASE_URL");
            return trimmed;
        }
    }

    /// <summary>
    /// Root URL of the <c>tastile-web</c> deployment. Used as the base for
    /// BetterAuth (<c>/api/auth/*</c>) and the mobile API-token bridge
    /// (<c>/api/mobile/api-token</c>). Replaces the Cognito Hosted UI URL,
    /// WebLoginUrl, and CallbackUrl from the pre-BetterAuth era.
    /// </summary>
    public static string WebBaseUrl
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable("TASTILE_WEB_BASE_URL")?.Trim();
            if (!string.IsNullOrEmpty(raw))
            {
                var trimmed = raw.TrimEnd('/');
                RequireHttpsForRemoteUrl(trimmed, "TASTILE_WEB_BASE_URL");
                return trimmed;
            }

            // Fallback: derive from WebAccountUrl (the user-account dashboard).
            // The auth endpoints live under the same origin.
            var accountUrl = Environment.GetEnvironmentVariable("TASTILE_WEB_ACCOUNT_URL")?.Trim();
            if (!string.IsNullOrEmpty(accountUrl))
            {
                var trimmed = accountUrl.TrimEnd('/');
                RequireHttpsForRemoteUrl(trimmed, "TASTILE_WEB_BASE_URL");
                return trimmed;
            }

            throw new InvalidOperationException(
                "Missing environment variable TASTILE_WEB_BASE_URL — please set it before running. See .env.example for the contract.");
        }
    }

    /// <summary>0 disables the idle refresh timer entirely.</summary>
    public static int PollIdleSeconds
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable("TASTILE_POLL_IDLE_SECONDS");
            return int.TryParse(raw, out var s) && s >= 0 ? s : 60;
        }
    }

    public static bool EnableSse =>
        Environment.GetEnvironmentVariable("TASTILE_ENABLE_SSE") == "1";

    /// <summary>
    /// Reject cleartext http:// URLs unless the host is a loopback address used
    /// for local development. Production deploys must always use https:// so
    /// the desktop does not transmit BetterAuth session/api tokens in cleartext
    /// (CWE-319). Loopback hosts are exempt so <c>127.0.0.1</c>, <c>localhost</c>,
    /// and <c>[::1]</c> remain usable for the local tastile-core daemon flow.
    /// </summary>
    private static void RequireHttpsForRemoteUrl(string raw, string envVarName)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                $"{envVarName} is not a valid absolute URL: '{raw}'.");
        }
        if (uri.Scheme != Uri.UriSchemeHttps && !IsLoopbackHost(uri))
        {
            throw new InvalidOperationException(
                $"{envVarName} must use https:// unless pointing at a loopback host; got '{raw}'.");
        }
    }

    private static bool IsLoopbackHost(Uri uri)
    {
        var host = uri.Host;
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.Ordinal)
            || host.Equals("[::1]", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }
}
