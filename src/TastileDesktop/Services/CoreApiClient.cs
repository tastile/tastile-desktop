namespace TastileDesktop.Services;

using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.IO;
using TastileDesktop.Models;

public class CoreApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "tastile-desktop-debug.log");

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            Debug.WriteLine(message);
        }
        catch { }
    }

    public CoreApiClient(string baseUrl = "http://localhost:3140")
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    internal CoreApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    // OAuth flow for browser-based authentication
    public async Task<OAuthInitResult?> StartOAuthAsync(string provider = "google")
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/auth/oauth/start", new { provider });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<OAuthInitResult>();
        }
        catch (Exception ex)
        {
            Log($"StartOAuth failed: {ex.Message}");
            return null;
        }
    }
    
    // Health
    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // Read endpoints
    public async Task<TilesResponse?> GetTilesAsync()
        => await _httpClient.GetFromJsonAsync<TilesResponse>("/read/tiles");

    public async Task<ActiveTileResponse?> GetActiveTileAsync()
        => await _httpClient.GetFromJsonAsync<ActiveTileResponse>("/read/active-tile");

    public async Task<ExecutionResponse?> GetExecutionAsync()
        => await _httpClient.GetFromJsonAsync<ExecutionResponse>("/read/execution");

    public async Task<PendingPromptResponse?> GetPendingPromptAsync()
        => await _httpClient.GetFromJsonAsync<PendingPromptResponse>("/views/pending-prompt");

    public async Task<TimelineTodayResponse?> GetTodayTimelineAsync()
        => await _httpClient.GetFromJsonAsync<TimelineTodayResponse>("/views/timeline/today");

    // Returns raw JSON because Event uses serde tagged enum
    public async Task<JsonElement?> GetEventsRawAsync()
    {
        try
        {
            var json = await _httpClient.GetStringAsync("/debug/events");
            return JsonDocument.Parse(json).RootElement;
        }
        catch { return null; }
    }

    // Command endpoints
    public async Task<CommandResponse?> CreateTileAsync(string title, string? nextAction = null, string? doneDefinition = null)
        => await PostCommandAsync("/commands/tile/create", new { title, next_action = nextAction, done_definition = doneDefinition });

    public async Task<CommandResponse?> StartTileAsync(string tileId)
        => await PostCommandAsync("/commands/tile/start", new { tile_id = tileId });

    public async Task<CommandResponse?> CompleteTileAsync(string? nextTileId = null)
        => await PostCommandAsync("/commands/tile/complete", new { next_tile_id = nextTileId });

    public async Task<CommandResponse?> DeferTileAsync(string tileId, string? reason = null)
        => await PostCommandAsync("/commands/tile/defer", new { tile_id = tileId, reason });

    public async Task<CommandResponse?> StartBreakAsync(int breakMin)
        => await PostCommandAsync("/commands/break/start", new { break_min = breakMin });

    public async Task<CommandResponse?> EndBreakAsync()
        => await PostCommandAsync("/commands/break/end", new { });

    public async Task<CommandResponse?> AttachMemoAsync(string? tileId, string text, string? memoKind = null)
        => await PostCommandAsync("/commands/memo/attach", new { tile_id = tileId, text, memo_kind = memoKind });

    public async Task<CommandResponse?> ExtendTileAsync(int extendMin)
        => await PostCommandAsync("/commands/tile/extend", new { delta_min = extendMin });

    public async Task<CommandResponse?> DeleteTileAsync(string tileId)
        => await PostCommandAsync("/commands/tile/delete", new { tile_id = tileId });

    private async Task<CommandResponse?> PostCommandAsync<T>(string path, T body)
    {
        var response = await _httpClient.PostAsJsonAsync(path, body);
        return await response.Content.ReadFromJsonAsync<CommandResponse>();
    }

    // Auth endpoints
    public async Task<AuthSession?> SignInAsync(string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("/auth/signin", new { email, password });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthSession>();
    }

    public async Task<AuthSession?> SignUpAsync(string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("/auth/signup", new { email, password });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthSession>();
    }

    public async Task SignOutAsync()
    {
        var response = await _httpClient.PostAsync("/auth/signout", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AuthSession?> GetSessionAsync()
    {
        try
        {
            // Force write to file to test if this code is even executed
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "tastile-getsession-called.txt"),
                $"GetSessionAsync called at {DateTime.Now}");

            Log("[GetSessionAsync] Requesting /auth/session...");
            var response = await _httpClient.GetAsync("/auth/session");
            Log($"[GetSessionAsync] Status: {response.StatusCode}");

            var content = await response.Content.ReadAsStringAsync();
            Log($"[GetSessionAsync] Response: {content}");

            if (!response.IsSuccessStatusCode)
            {
                Log($"[GetSessionAsync] Failed with status {response.StatusCode}");
                return null;
            }

            var session = await response.Content.ReadFromJsonAsync<AuthSession>();
            Log($"[GetSessionAsync] Parsed session: UserId={session?.UserId}, Email={session?.Email}, HasAccessToken={!string.IsNullOrEmpty(session?.AccessToken)}");
            return session;
        }
        catch (Exception ex)
        {
            Log($"[GetSessionAsync] Exception: {ex.Message}");
            Log($"[GetSessionAsync] StackTrace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Exchange OAuth authorization code for session.
    /// </summary>
    public async Task<OAuthTokenResponse?> SignInWithOAuthAsync(string provider, string code, string redirectUri)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/auth/oauth/callback", new 
            { 
                provider, 
                code, 
                redirect_uri = redirectUri 
            });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<OAuthTokenResponse>();
        }
        catch (Exception ex)
        {
            Log($"OAuth callback failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Desktop app asks daemon to start OAuth flow.
    /// Returns the auth URL for the desktop app to open in browser.
    /// </summary>
    public async Task<string?> StartBrowserAuthAsync(string provider = "google")
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/auth/oauth/start", new { provider });
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OAuthInitResult>();
            return result?.AuthUrl;
        }
        catch (Exception ex)
        {
            Log($"Failed to start browser auth: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Check if user is authenticated by asking daemon.
    /// Called periodically during OAuth flow.
    /// </summary>
    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            Log("[IsAuthenticatedAsync] Checking authentication...");
            var session = await GetSessionAsync();
            var isAuth = session != null && !string.IsNullOrEmpty(session.AccessToken);
            Log($"[IsAuthenticatedAsync] Result: {isAuth} (session={session != null}, hasToken={!string.IsNullOrEmpty(session?.AccessToken)})");
            return isAuth;
        }
        catch (Exception ex)
        {
            Log($"[IsAuthenticatedAsync] Exception: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// OAuth flow initialization result
/// </summary>
public class OAuthInitResult
{
    [JsonPropertyName("flow_id")]
    public string FlowId { get; set; } = "";
    
    [JsonPropertyName("auth_url")]
    public string AuthUrl { get; set; } = "";
    
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";
}

/// <summary>
/// OAuth token response from backend
/// </summary>
public class OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
    
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
    
    [JsonPropertyName("expires_at")]
    public long? ExpiresAt { get; set; }
    
    [JsonPropertyName("user")]
    public OAuthUser? User { get; set; }
}

public class OAuthUser
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    
    [JsonPropertyName("user_metadata")]
    public Dictionary<string, object>? UserMetadata { get; set; }
}
