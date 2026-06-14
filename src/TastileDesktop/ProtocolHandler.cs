using Microsoft.Win32;
using System.Diagnostics;

namespace TastileDesktop;

/// <summary>
/// Handles custom URL protocol (tastile://) for OAuth callbacks.
/// </summary>
public static class ProtocolHandler
{
    private const string ProtocolName = "tastile";
    
    /// <summary>
    /// Register the tastile:// protocol with Windows.
    /// Call this on first app launch.
    /// </summary>
    public static void RegisterProtocol()
    {
        try
        {
            // Register under HKEY_CURRENT_USER (no admin required)
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProtocolName}");
            key.SetValue("", "URL:Tastile Protocol");
            key.SetValue("URL Protocol", "");
            
            using var defaultIcon = key.CreateSubKey("DefaultIcon");
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? AppContext.BaseDirectory;
            defaultIcon.SetValue("", exePath);
            
            using var commandKey = key.CreateSubKey(@"shell\open\command");
            var commandValue = $"\"{exePath}\" \"%1\"";
            commandKey.SetValue("", commandValue);
            
            Debug.WriteLine($"Registered {ProtocolName}:// protocol");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register protocol: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Check if the protocol is already registered.
    /// </summary>
    public static bool IsProtocolRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProtocolName}\shell\open\command");
            if (key == null) return false;
            
            var value = key.GetValue("")?.ToString() ?? "";
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? AppContext.BaseDirectory;
            return value.Contains(exePath);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Parse OAuth callback URL (tastile://auth/callback?code=xxx&state=yyy).
    /// Returns (code, state) tuple or null if invalid.
    /// </summary>
    public static (string code, string state)? ParseOAuthCallback(string url)
    {
        try
        {
            if (!url.StartsWith($"{ProtocolName}://auth/callback"))
                return null;
            
            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            
            var code = query["code"];
            var state = query["state"];
            
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                return null;
            
            return (code, state);
        }
        catch
        {
            return null;
        }
    }

    public static TokenCallback? ParseTokenCallback(string url)
    {
        try
        {
            if (!url.StartsWith($"{ProtocolName}://auth/callback"))
                return null;

            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var fragment = uri.Fragment.StartsWith('#') ? uri.Fragment[1..] : uri.Fragment;
            var values = System.Web.HttpUtility.ParseQueryString(fragment);

            var state = query["state"];
            var idToken = values["id_token"];
            var accessToken = values["access_token"];
            var refreshToken = values["refresh_token"];
            var expiresInRaw = values["expires_in"];
            var expiresIn = int.TryParse(expiresInRaw, out var parsed) ? parsed : 3600;

            if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(accessToken))
                return null;

            return new TokenCallback(state, idToken, accessToken, refreshToken ?? string.Empty, expiresIn);
        }
        catch
        {
            return null;
        }
    }
}

public sealed record TokenCallback(
    string State,
    string IdToken,
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);
