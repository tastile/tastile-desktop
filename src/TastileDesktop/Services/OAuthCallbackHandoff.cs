using System;
using System.IO;

namespace TastileDesktop.Services;

internal static class OAuthCallbackHandoff
{
    private static readonly string CallbackPath = Path.Combine(Path.GetTempPath(), "tastile-oauth-callback.txt");

    public static void Store(string callbackUrl)
    {
        try
        {
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
}

