using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

public record ApiResult<T>(T? Data, HttpStatusCode? StatusCode, bool IsSuccess);

public class CoreApiClient
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient? _eventClient;
    private readonly Func<Task<string?>>? _getAccessToken;
    private readonly Func<Task<TastileDesktop.Models.AuthSession?>>? _refreshTokens;
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "tastile-desktop-debug.log");
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);

    public static string DebugLogPath => LogPath;

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            Debug.WriteLine(message);
        }
        catch
        {
        }
    }

    public CoreApiClient(
        string? baseUrl = null,
        Func<Task<string?>>? getAccessToken = null,
        Func<Task<TastileDesktop.Models.AuthSession?>>? refreshTokens = null)
    {
        var resolvedBaseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? AppSettings.ApiBaseUrl
            : baseUrl;
        _getAccessToken = getAccessToken;
        _refreshTokens = refreshTokens;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(resolvedBaseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TastileDesktop/0.3");
        // Separate client for SSE with infinite timeout (request/response endpoints use the 10s timeout).
        _eventClient = new HttpClient
        {
            BaseAddress = _httpClient.BaseAddress,
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _eventClient.DefaultRequestHeaders.UserAgent.ParseAdd("TastileDesktop/0.3");
    }

    internal CoreApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (_httpClient.Timeout == TimeSpan.FromSeconds(100))
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }
        _eventClient = httpClient;
    }

    internal CoreApiClient(
        HttpClient httpClient,
        Func<Task<string?>>? getAccessToken,
        Func<Task<TastileDesktop.Models.AuthSession?>>? refreshTokens = null)
        : this(httpClient)
    {
        _getAccessToken = getAccessToken;
        _refreshTokens = refreshTokens;
    }

    /// <summary>
    /// Sends a request with an attached Bearer token (if a provider is configured).
    /// On 401, attempts a token refresh once and retries.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithAuthAsync(
        HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        var initialToken = _getAccessToken is not null ? await _getAccessToken() : null;
        if (!string.IsNullOrEmpty(initialToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", initialToken);
        }

        var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || _refreshTokens is null)
        {
            return response;
        }

        // 401 → serialize refresh attempts to avoid Cognito revocation race
        response.Dispose();
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check: another request may have refreshed already
            var currentToken = _getAccessToken is not null ? await _getAccessToken() : null;
            if (!string.IsNullOrEmpty(currentToken) && currentToken != initialToken)
            {
                // Token was refreshed by another request, retry with it
                var retry = new HttpRequestMessage(request.Method, request.RequestUri);
                if (request.Content is not null) retry.Content = request.Content;
                retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentToken);
                return await client.SendAsync(retry, cancellationToken);
            }

            var refreshed = await _refreshTokens();
            if (refreshed is null)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("token refresh failed"),
                };
            }

            var retryWithRefresh = new HttpRequestMessage(request.Method, request.RequestUri);
            if (request.Content is not null) retryWithRefresh.Content = request.Content;
            retryWithRefresh.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.IdToken);
            return await client.SendAsync(retryWithRefresh, cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task AttachBearerAsync(HttpRequestMessage request)
    {
        if (_getAccessToken is null)
        {
            return;
        }

        try
        {
            var token = await _getAccessToken();
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch (Exception ex)
        {
            Log($"token-provider failed: {ex.Message}");
        }
    }

    private async Task<ApiResult<T>> GetJsonWithStatusAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await SendWithAuthAsync(_httpClient, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Log($"[GetJsonAsync] {path} => {(int)response.StatusCode} {response.StatusCode} body={body}");
            return new ApiResult<T>(default, response.StatusCode, false);
        }
        var data = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        return new ApiResult<T>(data, response.StatusCode, true);
    }

    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await SendWithAuthAsync(_httpClient, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Log($"[GetJsonAsync] {path} => {(int)response.StatusCode} {response.StatusCode} body={body}");
            return default;
        }
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        using var response = await SendWithAuthAsync(_httpClient, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var bodyText = await response.Content.ReadAsStringAsync(cancellationToken);
            Log($"[PostJsonAsync] {path} => {(int)response.StatusCode} {response.StatusCode} body={bodyText}");
        }
        return await ReadCommandResponseAsync<TResponse>(response, cancellationToken);
    }

    private async Task<TResponse?> PostJsonAsync<TResponse>(string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        using var response = await SendWithAuthAsync(_httpClient, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Log($"[PostJsonAsync] {path} => {(int)response.StatusCode} {response.StatusCode} body={body}");
        }
        return await ReadCommandResponseAsync<TResponse>(response, cancellationToken);
    }

    private static async Task<TResponse?> ReadCommandResponseAsync<TResponse>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // Command endpoints often return Ok=false with a populated `Prompt`
        // payload (e.g. create_conflict) on 4xx, so we attempt to parse the
        // body for any 2xx/4xx response and only treat 5xx + transport
        // failures as "no response".
        if ((int)response.StatusCode >= 500)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
    }

    // Health
    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
            using var response = await SendWithAuthAsync(_httpClient, request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Read endpoints
    public Task<TilesResponse?> GetTilesAsync()
        => GetJsonAsync<TilesResponse>("/read/tiles");

    public Task<TilesResponse?> GetTilesAsync(string viewMode, string? lifecycle = null, int? limit = null, string? search = null)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(viewMode)) queryParams.Add($"view_mode={viewMode}");
        if (!string.IsNullOrEmpty(lifecycle)) queryParams.Add($"lifecycle={lifecycle}");
        if (limit.HasValue) queryParams.Add($"limit={limit.Value}");
        if (!string.IsNullOrEmpty(search)) queryParams.Add($"search={Uri.EscapeDataString(search)}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        return GetJsonAsync<TilesResponse>($"/read/tiles{query}");
    }

    public Task<ExecutionView?> GetExecutionViewAsync()
        => GetJsonAsync<ExecutionView>("/read/execution-view");

    public Task<TileView?> GetTileByIdAsync(string tileId)
        => GetJsonAsync<TileView>($"/read/tile/{tileId}");

    public Task<EditableTileView?> GetEditableTileByIdAsync(string tileId)
        => GetJsonAsync<EditableTileView>($"/read/tile/{tileId}/editable");

    public Task<TilesInProgressResponse?> GetTilesInProgressAsync()
        => GetJsonAsync<TilesInProgressResponse>("/read/tiles-in-progress");

    public Task<ActiveTileResponse?> GetActiveTileAsync()
        => GetJsonAsync<ActiveTileResponse>("/read/active-tile");

    public Task<ExecutionResponse?> GetExecutionAsync()
        => GetJsonAsync<ExecutionResponse>("/read/execution");

    public Task<PendingPromptResponse?> GetPendingPromptAsync()
        => GetJsonAsync<PendingPromptResponse>("/views/pending-prompt");

    public Task<TimelineTodayResponse?> GetTodayTimelineAsync()
        => GetJsonAsync<TimelineTodayResponse>("/views/timeline/today");

    public Task<CalendarProjectionResponse?> GetCalendarProjectionAsync(string viewPath, DateTimeOffset anchorLocal)
    {
        var anchor = Uri.EscapeDataString(anchorLocal.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        var tzOffset = (int)anchorLocal.Offset.TotalSeconds;
        return GetJsonAsync<CalendarProjectionResponse>($"{viewPath}?anchor={anchor}&tz_offset={tzOffset}");
    }

    public async Task<TimelineTodayResponse?> GetTimelineForViewportAsync(TimelineViewportSettings viewport)
    {
        var request = CalendarViewportResolver.Resolve(viewport, DateTimeOffset.Now.ToLocalTime());
        var projection = await GetCalendarProjectionAsync(request.ViewPath, request.AnchorLocal);
        if (projection is null)
        {
            return null;
        }

        var projectedBlocks = viewport.ScaleUnit == TimelineScaleUnit.Week
            ? projection.Blocks
            : projection.AllDaySpans.Concat(projection.Blocks).ToList();

        var items = projectedBlocks
            .OrderBy(block => block.StartAt, StringComparer.Ordinal)
            .Select(block => new TimelineItemView(
                Kind: string.IsNullOrWhiteSpace(block.Kind) ? "scheduled" : block.Kind,
                TileId: block.TileId,
                SemanticRole: block.SemanticRole,
                Title: block.Title,
                StartedAt: block.StartAt,
                EndedAt: block.EndAt,
                DurationMin: ResolveDurationMinutes(block.StartAt, block.EndAt),
                IsActive: block.IsActive))
            .ToList();

        return new TimelineTodayResponse(
            Items: items,
            RangeStart: projection.RangeStart,
            RangeEnd: projection.RangeEnd);
    }

    public async IAsyncEnumerable<string> StreamStateEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var eventClient = _eventClient ?? _httpClient;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/read/events/state");
        request.Headers.Accept.ParseAdd("text/event-stream");
        await AttachBearerAsync(request);
        using var response = await eventClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                yield break;
            }

            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line["data:".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(payload))
            {
                yield return payload;
            }
        }
    }

    // Returns raw JSON because Event uses serde tagged enum
    public async Task<JsonElement?> GetEventsRawAsync()
    {
        try
        {
            var json = await _httpClient.GetStringAsync("/debug/events");
            return JsonDocument.Parse(json).RootElement;
        }
        catch
        {
            return null;
        }
    }

    // Command endpoints
    public Task<CommandResponse?> CreateTileAsync(string title, string? nextAction = null, string? doneDefinition = null)
        => CreateTileAsync(new CreateTileRequest(title, nextAction, doneDefinition, null, null, null, null, null, null));

    public Task<CommandResponse?> CreateTileAsync(CreateTileRequest request)
        => PostJsonAsync<CreateTileRequest, CommandResponse>("/commands/tile/create", request);

    public Task<CommandResponse?> StartTileAsync(string tileId)
        => PostJsonAsync<object, CommandResponse>("/commands/tile/start", new { tile_id = tileId });

    public Task<CommandResponse?> CompleteTileAsync(string? tileId = null, string? nextTileId = null, string? scope = null)
        => PostJsonAsync<object, CommandResponse>("/commands/tile/complete", new { tile_id = tileId, next_tile_id = nextTileId, scope });

    public Task<CommandResponse?> DeferTileAsync(string tileId, string? reason = null, int? minutes = null)
        => PostJsonAsync<object, CommandResponse>("/commands/tile/defer", new { tile_id = tileId, reason, minutes });

    public async Task<CommandResponse?> StartBreakAsync(int breakMin, string? insertionMode = null)
    {
        Log($"[StartBreakAsync] Starting break: {breakMin} minutes");
        var result = await PostJsonAsync<object, CommandResponse>("/commands/break/start", new { break_min = breakMin, insertion_mode = insertionMode });
        Log($"[StartBreakAsync] Result: ok={result?.Ok}, error={result?.Error}");
        return result;
    }

    public Task<CommandResponse?> EndBreakAsync()
        => PostJsonAsync<object, CommandResponse>("/commands/break/end", new { });

    public Task<CommandResponse?> AttachMemoAsync(string? tileId, string text, string? memoKind = null)
        => PostJsonAsync<object, CommandResponse>("/commands/memo/attach", new { tile_id = tileId, text, memo_kind = memoKind });

    public Task<CommandResponse?> ExtendTileAsync(int extendMin)
        => PostJsonAsync<object, CommandResponse>("/commands/tile/extend", new { delta_min = extendMin });

    public Task<CommandResponse?> DeleteTileAsync(string tileId)
        => PostJsonAsync<object, CommandResponse>("/commands/tile/delete", new { tile_id = tileId });

    public Task<CommandResponse?> UpdateTileAsync(string tileId, CreateTileRequest request)
        => PostJsonAsync<object, CommandResponse>("/commands/tile/update", new
        {
            tile_id = tileId,
            title = request.Title,
            next_action = request.NextAction,
            done_definition = request.DoneDefinition,
            temporal = request.Temporal,
            objective = request.Objective,
            interruption = request.Interruption,
            automation = request.Automation,
            annotation = request.Annotation,
        });

    public async Task<RequestPromptResponse?> RequestPromptAsync(string tileId)
    {
        try
        {
            Log($"[RequestPromptAsync] Requesting prompt for tile: {tileId}");
            var response = await PostJsonAsync<object, RequestPromptResponse>("/commands/prompt/request", new { tile_id = tileId });
            Log($"[RequestPromptAsync] Result: ok={response?.Ok}, hasPrompt={response?.Prompt != null}, error={response?.Error}");
            return response;
        }
        catch (Exception ex)
        {
            Log($"[RequestPromptAsync] Exception: {ex.Message}");
            throw;
        }
    }

    public Task<CommandResponse?> RespondStartupRecoveryPromptAsync(
        string promptId,
        string tileId,
        string actionId,
        DateTimeOffset? stopAt = null)
    {
        var body = new RespondStartupRecoveryPromptRequest(
            PromptId: promptId,
            TileId: tileId,
            ActionId: actionId,
            StopAt: stopAt?.UtcDateTime.ToString("O"));
        return PostJsonAsync<RespondStartupRecoveryPromptRequest, CommandResponse>("/commands/prompt/respond-startup-recovery", body);
    }

    // Auth endpoints
    public async Task<JsonElement?> DebugTokenAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/debug/token");
            using var response = await SendWithAuthAsync(_httpClient, request);
            var body = await response.Content.ReadAsStringAsync();
            Log($"[DebugTokenAsync] Status={(int)response.StatusCode} body={body}");
            if (!response.IsSuccessStatusCode) return null;
            return JsonDocument.Parse(body).RootElement;
        }
        catch (Exception ex)
        {
            Log($"[DebugTokenAsync] Exception: {ex.Message}");
            return null;
        }
    }

    public async Task SignOutAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/signout");
        using var response = await SendWithAuthAsync(_httpClient, request);
        response.EnsureSuccessStatusCode();
    }

    public Task<TileQuotaResponse?> GetTileQuotaAsync()
        => GetJsonAsync<TileQuotaResponse>("/auth/tile-quota");

    // WithStatus variants for EventDrivenPoller connection detection
    public Task<ApiResult<ExecutionView>> GetExecutionViewWithStatusAsync()
        => GetJsonWithStatusAsync<ExecutionView>("/read/execution-view");

    public Task<ApiResult<TilesResponse>> GetTilesWithStatusAsync()
        => GetJsonWithStatusAsync<TilesResponse>("/read/tiles");

    public Task<ApiResult<PendingPromptResponse>> GetPendingPromptWithStatusAsync()
        => GetJsonWithStatusAsync<PendingPromptResponse>("/views/pending-prompt");

    public Task<ApiResult<TileQuotaResponse>> GetTileQuotaWithStatusAsync()
        => GetJsonWithStatusAsync<TileQuotaResponse>("/auth/tile-quota");

    public Task<IntegrationSettingsResponse?> GetIntegrationSettingsAsync()
        => GetJsonAsync<IntegrationSettingsResponse>("/auth/integrations/settings");

    public Task<IntegrationSettingsResponse?> UpdateGoogleCalendarIntegrationAsync(
        bool? connected = null,
        bool? canRead = null,
        bool? canWrite = null,
        string? accountEmail = null,
        string? selectedCalendarId = null,
        string? syncMode = null,
        string? readPolicy = null,
        string? writePolicy = null,
        List<string>? grantedScopes = null,
        string? lastSyncedAt = null)
    {
        var payload = new Dictionary<string, object?>();
        if (connected.HasValue) payload["connected"] = connected.Value;
        if (canRead.HasValue) payload["can_read"] = canRead.Value;
        if (canWrite.HasValue) payload["can_write"] = canWrite.Value;
        if (accountEmail is not null || (connected.HasValue && !connected.Value)) payload["account_email"] = accountEmail;
        if (selectedCalendarId is not null || (connected.HasValue && !connected.Value)) payload["selected_calendar_id"] = selectedCalendarId;
        if (!string.IsNullOrWhiteSpace(syncMode)) payload["sync_mode"] = syncMode;
        if (!string.IsNullOrWhiteSpace(readPolicy)) payload["read_policy"] = readPolicy;
        if (!string.IsNullOrWhiteSpace(writePolicy)) payload["write_policy"] = writePolicy;
        if (grantedScopes is not null) payload["granted_scopes"] = grantedScopes;
        if (lastSyncedAt is not null) payload["last_synced_at"] = lastSyncedAt;

        return PostJsonAsync<object, IntegrationSettingsResponse>("/auth/integrations/settings", new
        {
            google_calendar = payload,
        });
    }

    public Task<CalendarSyncPlanPreviewResponse?> GetCalendarSyncPlanPreviewAsync()
        => GetJsonAsync<CalendarSyncPlanPreviewResponse>("/auth/integrations/calendar/sync-plan");

    private static long ResolveDurationMinutes(string? startAt, string? endAt)
    {
        if (!DateTimeOffset.TryParse(startAt, out var start) || !DateTimeOffset.TryParse(endAt, out var end))
        {
            return 0;
        }

        var minutes = (long)(end - start).TotalMinutes;
        return minutes > 0 ? minutes : 0;
    }
}

public class IntegrationSettingsResponse
{
    [JsonPropertyName("google_calendar")]
    public GoogleCalendarIntegrationResponse GoogleCalendar { get; set; } = new();
}

public class GoogleCalendarIntegrationResponse
{
    [JsonPropertyName("connected")]
    public bool Connected { get; set; }

    [JsonPropertyName("can_read")]
    public bool CanRead { get; set; } = true;

    [JsonPropertyName("can_write")]
    public bool CanWrite { get; set; } = true;

    [JsonPropertyName("provider_status")]
    public string ProviderStatus { get; set; } = "disconnected";

    [JsonPropertyName("account_email")]
    public string? AccountEmail { get; set; }

    [JsonPropertyName("selected_calendar_id")]
    public string? SelectedCalendarId { get; set; }

    [JsonPropertyName("granted_scopes")]
    public List<string> GrantedScopes { get; set; } = [];

    [JsonPropertyName("sync_mode")]
    public string SyncMode { get; set; } = "push_only";

    [JsonPropertyName("read_policy")]
    public string ReadPolicy { get; set; } = "import_and_block_scheduling";

    [JsonPropertyName("write_policy")]
    public string WritePolicy { get; set; } = "tastile_owned_only";

    [JsonPropertyName("last_synced_at")]
    public string? LastSyncedAt { get; set; }

    [JsonPropertyName("last_full_sync_at")]
    public string? LastFullSyncAt { get; set; }
}

public class CalendarSyncPlanPreviewResponse
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "google_calendar";

    [JsonPropertyName("selected_calendar_id")]
    public string? SelectedCalendarId { get; set; }

    [JsonPropertyName("sync_mode")]
    public string SyncMode { get; set; } = "push_only";

    [JsonPropertyName("read_policy")]
    public string ReadPolicy { get; set; } = "import_and_block_scheduling";

    [JsonPropertyName("write_policy")]
    public string WritePolicy { get; set; } = "tastile_owned_only";
}

public class CalendarProjectionResponse
{
    [JsonPropertyName("view")]
    public string View { get; set; } = "day";

    [JsonPropertyName("range_start")]
    public string RangeStart { get; set; } = string.Empty;

    [JsonPropertyName("range_end")]
    public string RangeEnd { get; set; } = string.Empty;

    [JsonPropertyName("grid_start")]
    public string GridStart { get; set; } = string.Empty;

    [JsonPropertyName("grid_end")]
    public string GridEnd { get; set; } = string.Empty;

    [JsonPropertyName("blocks")]
    public List<CalendarProjectionBlockResponse> Blocks { get; set; } = [];

    [JsonPropertyName("all_day_spans")]
    public List<CalendarProjectionBlockResponse> AllDaySpans { get; set; } = [];
}

public class CalendarProjectionBlockResponse
{
    [JsonPropertyName("tile_id")]
    public string? TileId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "scheduled";

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("start_at")]
    public string StartAt { get; set; } = string.Empty;

    [JsonPropertyName("end_at")]
    public string EndAt { get; set; } = string.Empty;

    [JsonPropertyName("semantic_role")]
    public string? SemanticRole { get; set; }

    [JsonPropertyName("all_day")]
    public bool AllDay { get; set; }
}
