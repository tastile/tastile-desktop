using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TastileDesktop.Services;

/// <summary>
/// Authentication session info from tastile-core.
/// </summary>
public class AuthSession
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("expires_at")]
    public string ExpiresAt { get; set; } = "";
}

/// <summary>
/// Manages authentication via tastile-core daemon.
/// </summary>
public class AuthService
{
    private static AuthService? _instance;
    public static AuthService Instance => _instance ??= new AuthService();

    private AuthSession? _currentSession;

    public bool IsAuthenticated => IsSessionValid(_currentSession);
    public AuthSession? CurrentSession => _currentSession;
    public string? UserEmail => _currentSession?.Email;
    public string? UserId => _currentSession?.UserId;

    public event EventHandler? AuthStateChanged;

    private AuthService() { }

    private static string SessionFilePath
    {
        get
        {
            var appData = Environment.GetEnvironmentVariable("APPDATA");
            if (string.IsNullOrWhiteSpace(appData))
            {
                appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }

            return Path.Combine(appData, "Tastile", "session.json");
        }
    }

    /// <summary>
    /// Initialize and load existing session.
    /// </summary>
    public async Task InitializeAsync(CoreApiClient api)
    {
        try
        {
            // Require an active daemon session. Do not silently restore from local file.
            var session = await api.GetSessionAsync();
            UpdateSession(session);
        }
        catch
        {
            UpdateSession(null);
        }
    }

    /// <summary>
    /// Refresh session from daemon after an external auth flow completes.
    /// Returns true when auth state or session contents changed.
    /// </summary>
    public async Task<bool> RefreshSessionFromDaemonAsync(CoreApiClient api)
    {
        AuthSession? session;
        try
        {
            session = await api.GetSessionAsync();
        }
        catch
        {
            session = null;
        }

        return UpdateSession(session);
    }

    /// <summary>
    /// Save session to file for persistence across restarts.
    /// </summary>
    private void SaveSessionToFile(AuthSession session)
    {
        try
        {
            var directory = Path.GetDirectoryName(SessionFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(session);
            File.WriteAllText(SessionFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save session: {ex.Message}");
        }
    }

    /// <summary>
    /// Load session from file.
    /// </summary>
    private AuthSession? LoadSessionFromFile()
    {
        try
        {
            if (!File.Exists(SessionFilePath))
                return null;

            var json = File.ReadAllText(SessionFilePath);
            return JsonSerializer.Deserialize<AuthSession>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load session: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Delete saved session file.
    /// </summary>
    private void DeleteSessionFile()
    {
        try
        {
            if (File.Exists(SessionFilePath))
            {
                File.Delete(SessionFilePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete session file: {ex.Message}");
        }
    }

    /// <summary>
    /// Restore session to daemon (currently not supported by daemon API, this is a placeholder).
    /// </summary>
    private async Task<AuthSession?> RestoreSessionToDaemonAsync(CoreApiClient api, AuthSession session)
    {
        try
        {
            return await api.RestoreSessionAsync(session);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore session to daemon: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sign out via tastile-core.
    /// </summary>
    public async Task SignOutAsync(CoreApiClient api)
    {
        try
        {
            await api.SignOutAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sign out error: {ex.Message}");
        }
        finally
        {
            UpdateSession(null);
        }
    }

    /// <summary>
    /// Get access token for API calls.
    /// </summary>
    public string? GetAccessToken()
    {
        return _currentSession?.AccessToken;
    }

    private bool UpdateSession(AuthSession? session)
    {
        if (!IsSessionValid(session))
        {
            session = null;
        }

        var changed = !SessionsEqual(_currentSession, session);

        _currentSession = session;

        if (session != null)
        {
            SaveSessionToFile(session);
        }
        else
        {
            DeleteSessionFile();
        }

        if (changed)
        {
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    private static bool SessionsEqual(AuthSession? left, AuthSession? right)
    {
        if (left == null && right == null)
            return true;

        if (left == null || right == null)
            return false;

        return left.UserId == right.UserId
            && left.Email == right.Email
            && left.AccessToken == right.AccessToken
            && left.RefreshToken == right.RefreshToken
            && left.ExpiresAt == right.ExpiresAt;
    }

    private static bool IsSessionValid(AuthSession? session)
    {
        return session != null
            && !string.IsNullOrWhiteSpace(session.UserId)
            && !string.IsNullOrWhiteSpace(session.Email)
            && !string.IsNullOrWhiteSpace(session.AccessToken);
    }
}
