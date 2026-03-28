using System;
using System.IO;

namespace TastileDesktop.Services;

internal static class OAuthCallbackHandoff
{
    private static readonly string StorageDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tastile",
        "Auth");
    private static readonly string CallbackPath = Path.Combine(StorageDirectory, "oauth-callback.txt");
    private static readonly string ExpectedStatePath = Path.Combine(StorageDirectory, "oauth-state.txt");

    public static void StoreExpectedState(string? expectedState)
    {
        try
        {
            EnsureStorageDirectory();
            if (string.IsNullOrWhiteSpace(expectedState))
            {
                if (File.Exists(ExpectedStatePath))
                {
                    File.Delete(ExpectedStatePath);
                }
                return;
            }

            File.WriteAllText(ExpectedStatePath, expectedState.Trim());
        }
        catch
        {
            // Best-effort state persistence only.
        }
    }

    public static void ClearExpectedState()
        => StoreExpectedState(null);

    public static bool MatchesExpectedState(string? actualState)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(actualState) || !File.Exists(ExpectedStatePath))
            {
                return false;
            }

            var expectedState = File.ReadAllText(ExpectedStatePath).Trim();
            return !string.IsNullOrWhiteSpace(expectedState)
                && string.Equals(expectedState, actualState.Trim(), StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static void Store(string callbackUrl)
    {
        try
        {
            EnsureStorageDirectory();
            File.WriteAllText(CallbackPath, callbackUrl);
        }
        catch
        {
            // Best-effort handoff only.
        }
    }

    public static string? Take()
    {
        try
        {
            if (!File.Exists(CallbackPath))
            {
                return null;
            }

            var value = File.ReadAllText(CallbackPath).Trim();
            File.Delete(CallbackPath);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    public static string? ExtractStateFromAuthUrl(string? authUrl)
    {
        if (string.IsNullOrWhiteSpace(authUrl) ||
            !Uri.TryCreate(authUrl, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Query))
        {
            return null;
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 0)
            {
                continue;
            }

            if (!string.Equals(Uri.UnescapeDataString(parts[0]), "state", StringComparison.Ordinal))
            {
                continue;
            }

            return parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        }

        return null;
    }

    private static void EnsureStorageDirectory()
    {
        if (!Directory.Exists(StorageDirectory))
        {
            Directory.CreateDirectory(StorageDirectory);
        }
    }
}

