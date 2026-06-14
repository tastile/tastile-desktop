using System;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// Process-wide configuration sourced from environment variables.
/// Replaces the daemon-coupled <c>RuntimeProfile</c>.
/// </summary>
public static class AppSettings
{
    public const string DefaultApiBaseUrl = "https://api.tastile.app";
    public const string WebAccountUrl = "https://app.tastile.app/dashboard/account";

    public static string ApiBaseUrl
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable("TASTILE_API_BASE_URL")?.Trim();
            return string.IsNullOrEmpty(raw) ? DefaultApiBaseUrl : raw.TrimEnd('/');
        }
    }

    public static CognitoConfig Cognito => CognitoConfig.FromEnv();

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
