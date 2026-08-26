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

            return raw.TrimEnd('/');
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
                return raw.TrimEnd('/');
            }

            // Fallback: derive from WebAccountUrl (the user-account dashboard).
            // The auth endpoints live under the same origin.
            var accountUrl = Environment.GetEnvironmentVariable("TASTILE_WEB_ACCOUNT_URL")?.Trim();
            if (!string.IsNullOrEmpty(accountUrl))
            {
                return accountUrl.TrimEnd('/');
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
}
