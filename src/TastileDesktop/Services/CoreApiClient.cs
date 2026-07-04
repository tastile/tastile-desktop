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
    /// On 401, attempts a token refresh once and retries using the OAuth2
    /// access token (never the Cognito id_token, which is not valid v1 auth).
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

        // 401 → serialize refresh attempts to avoid Cognito revocation race.
        // Reuse the *same* access token model the v1 API speaks; the Cognito
        // id_token is never sent as a v1 bearer (PROJECT-TRUTH §Authentication).
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
            retryWithRefresh.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
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

    private static NotSupportedException NotSupportedOnV1(string operation)
        => new($"Operation '{operation}' has no v1 API equivalent and is not supported by the desktop v1 client.");

    // Wraps a typed payload in the v1 CommandEnvelope per v1/14 §1.
    // Every POST/DELETE that maps to a v1 CommandKind handler requires
    // this envelope; only `idempotency_key` is mandatory, the rest is
    // sent as null when the caller has nothing to assert.
    private static CommandEnvelope<TPayload> WrapEnvelope<TPayload>(TPayload payload)
        => new(
            ExpectedRevision: null,
            IdempotencyKey: Guid.NewGuid(),
            OccurredAt: null,
            Payload: payload);

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

    private async Task<TResponse?> DeleteJsonAsync<TResponse>(string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        using var response = await SendWithAuthAsync(_httpClient, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Log($"[DeleteJsonAsync] {path} => {(int)response.StatusCode} {response.StatusCode} body={body}");
        }
        return await ReadCommandResponseAsync<TResponse>(response, cancellationToken);
    }

    // DELETE-with-body variant.  v1 archive_tile requires a
    // CommandEnvelope<ArchiveTilePayload> body; axum tolerates a
    // request body on DELETE.
    private async Task<TResponse?> DeleteJsonAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, path)
        {
            Content = JsonContent.Create(body),
        };
        using var response = await SendWithAuthAsync(_httpClient, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var bodyText = await response.Content.ReadAsStringAsync(cancellationToken);
            Log($"[DeleteJsonAsync] {path} => {(int)response.StatusCode} {response.StatusCode} body={bodyText}");
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
            using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/health");
            using var response = await SendWithAuthAsync(_httpClient, request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Read endpoints (v1)
    public Task<TilesResponse?> GetTilesAsync()
        => GetJsonAsync<TilesResponse>("/v1/tiles");

    public Task<TilesResponse?> GetTilesAsync(string viewMode, string? lifecycle = null, int? limit = null, string? search = null)
    {
        // v1 list_tiles only honors `owner_ids` and `limit`. `view_mode`,
        // `lifecycle`, and `search` are v0-only filters and are ignored on
        // the v1 endpoint; callers that still pass them will get the
        // default owner-scoped page. Caller follow-up tracked in WORK-LOG.
        var queryParams = new List<string>();
        if (limit.HasValue) queryParams.Add($"limit={limit.Value}");
        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        return GetJsonAsync<TilesResponse>($"/v1/tiles{query}");
    }

    public Task<ExecutionView?> GetExecutionViewAsync()
        => throw NotSupportedOnV1("GetExecutionViewAsync (no execution_id)");

    public Task<TileView?> GetTileByIdAsync(string tileId)
        => GetJsonAsync<TileView>($"/v1/tiles/{tileId}");

    public Task<EditableTileView?> GetEditableTileByIdAsync(string tileId)
        => GetJsonAsync<EditableTileView>($"/v1/tiles/{tileId}/editable");

    public Task<TilesInProgressResponse?> GetTilesInProgressAsync()
        => throw NotSupportedOnV1("GetTilesInProgressAsync");

    public Task<ActiveTileResponse?> GetActiveTileAsync()
        => GetJsonAsync<ActiveTileResponse>("/v1/active-tile");

    public Task<ExecutionResponse?> GetExecutionAsync()
        => throw NotSupportedOnV1("GetExecutionAsync (no execution_id)");

    public Task<PendingPromptResponse?> GetPendingPromptAsync()
        => GetJsonAsync<PendingPromptResponse>("/v1/prompts/pending");

    public Task<TimelineTodayResponse?> GetTodayTimelineAsync()
        => GetJsonAsync<TimelineTodayResponse>("/v1/timeline/today");

    public Task<CalendarProjectionResponse?> GetCalendarProjectionAsync(string viewPath, DateTimeOffset anchorLocal)
    {
        // Calendar viewport paths come from CalendarViewportResolver; the
        // desktop ViewPath is built on top of the v1 calendar surface
        // (e.g. "/v1/calendar/year"), so no further rewriting is needed.
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
        // v1 has no equivalent SSE state stream; the previous /read/events/state
        // endpoint was a v0 daemon surface that has been removed. Fail loudly
        // so any SSE caller surfaces the regression.
        throw NotSupportedOnV1("StreamStateEventsAsync");
        // unreachable; satisfies the iterator signature
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    // Returns raw JSON because Event uses serde tagged enum
    public async Task<JsonElement?> GetEventsRawAsync()
    {
        try
        {
            var json = await _httpClient.GetStringAsync("/v1/debug/events");
            return JsonDocument.Parse(json).RootElement;
        }
        catch
        {
            return null;
        }
    }

    // Command endpoints (v1)
    //
    // The v1 server validates every `POST /v1/...` command against
    // a `CommandRequest<TPayload>` envelope (idempotency_key +
    // expected_revision + payload).  Only the payload field is
    // call-shape-specific; the surrounding envelope is generated by
    // `WrapEnvelope`.  Where the existing desktop DTO cannot produce
    // a safe v1 payload (the v0 fields don't line up with v1's typed
    // payloads), the method throws `NotSupportedException` so the
    // regression is explicit.

    public Task<CommandResponse?> CreateTileAsync(string title, string? nextAction = null, string? doneDefinition = null)
        => throw NotSupportedOnV1(
            "CreateTileAsync: v1 CreateTilePayload requires typed TileKind + PlanRole (i16) and the desktop form's free-text fields don't map. " +
            "Use a CreateTileRequest whose UI collects v1-shaped fields before re-introducing this call.");

    public Task<CommandResponse?> CreateTileAsync(CreateTileRequest request)
        => throw NotSupportedOnV1(
            "CreateTileAsync(CreateTileRequest): v1 CreateTilePayload requires typed TileKind + PlanRole (i16); the existing free-form CreateTileRequest carries v0-shaped fields. " +
            "A v1-shaped DTO must be introduced at the UI boundary first.");

    public Task<CommandResponse?> StartTileAsync(string tileId)
        => throw NotSupportedOnV1(
            $"StartTileAsync: v1 StartTilePayload requires plan_id + PlacementSource + PlacementSourceRef + PlacementBaseline; a tile_id alone cannot satisfy '{tileId}'.");

    public Task<CommandResponse?> CompleteTileAsync(string? tileId = null, string? nextTileId = null, string? scope = null)
        => throw NotSupportedOnV1(
            "CompleteTileAsync: v1 SetTileLifecyclePayload (the handler behind /complete) requires a numeric state (0=active,1=deferred,2=completed); the desktop's (tile_id, next_tile_id, scope) free-form shape doesn't map. " +
            "Introduce a v1 lifecycle DTO at the UI boundary.");

    public Task<CommandResponse?> DeferTileAsync(string tileId, string? reason = null, int? minutes = null)
        => throw NotSupportedOnV1(
            $"DeferTileAsync: v1 SetTileLifecyclePayload requires a numeric state (1=deferred) and a deferred_until timestamp; the desktop's (reason, minutes) shape doesn't map for tile '{tileId}'.");

    public Task<CommandResponse?> StartBreakAsync(int breakMin, string? insertionMode = null)
        => throw NotSupportedOnV1("StartBreakAsync (breaks are not modeled in v1)");

    public Task<CommandResponse?> EndBreakAsync()
        => throw NotSupportedOnV1("EndBreakAsync (breaks are not modeled in v1)");

    public Task<CommandResponse?> AttachMemoAsync(string? tileId, string text, string? memoKind = null)
    {
        if (string.IsNullOrEmpty(tileId))
        {
            throw NotSupportedOnV1("AttachMemoAsync requires a tile_id on v1");
        }
        if (memoKind is not null)
        {
            // v1 AttachMemoPayload has no memo_kind field; raising
            // here prevents silently dropping the kind from the wire.
            throw NotSupportedOnV1(
                $"AttachMemoAsync: v1 AttachMemoPayload has no 'memo_kind' field (provided '{memoKind}'). Drop memo_kind or extend the v1 payload type.");
        }
        var envelope = WrapEnvelope(new AttachMemoV1Payload(TileId: tileId!, Body: text));
        return PostJsonAsync<CommandEnvelope<AttachMemoV1Payload>, CommandResponse>(
            $"/v1/tiles/{tileId}/memos", envelope);
    }

    public Task<CommandResponse?> ExtendTileAsync(int extendMin)
        => throw NotSupportedOnV1("ExtendTileAsync (no tile_id parameter and no v1 extend endpoint in this scope)");

    // DeleteTileAsync → archive_tile.  v1 archive_tile accepts a
    // CommandEnvelope<ArchiveTilePayload> body ({ tile_id }).
    public Task<CommandResponse?> DeleteTileAsync(string tileId)
    {
        var envelope = WrapEnvelope(new ArchiveTileV1Payload(TileId: tileId));
        return DeleteJsonAsync<CommandEnvelope<ArchiveTileV1Payload>, CommandResponse>(
            $"/v1/tiles/{tileId}", envelope);
    }

    public Task<CommandResponse?> UpdateTileAsync(string tileId, CreateTileRequest request)
        => throw NotSupportedOnV1(
            $"UpdateTileAsync: v1 UpdateTilePayload uses (title, description, color, icon, external_id) — the desktop's CreateTileRequest carries v0-shaped fields (next_action, done_definition, temporal, objective, ...). tile_id='{tileId}'.");

    public Task<RequestPromptResponse?> RequestPromptAsync(string tileId)
        => throw NotSupportedOnV1(
            $"RequestPromptAsync: v1 create_prompt expects a free-form {{ kind, payload }} envelope, not a tile-only body (tile_id='{tileId}'). " +
            "Introduce a v1-shaped prompt request DTO before re-introducing this call.");

    // startup-recovery is intentionally free-form on the server (the
    // handler accepts raw serde_json::Value) so we do NOT wrap the
    // existing RespondStartupRecoveryPromptRequest in a v1 envelope.
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
        return PostJsonAsync<RespondStartupRecoveryPromptRequest, CommandResponse>("/v1/prompts/startup-recovery", body);
    }

    // Auth endpoints (v1)
    public async Task<JsonElement?> DebugTokenAsync()
        => throw NotSupportedOnV1("DebugTokenAsync");

    public async Task SignOutAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/signout");
        using var response = await SendWithAuthAsync(_httpClient, request);
        response.EnsureSuccessStatusCode();
    }

    public Task<TileQuotaResponse?> GetTileQuotaAsync()
        => GetJsonAsync<TileQuotaResponse>("/v1/quota/tiles");

    // WithStatus variants for EventDrivenPoller connection detection
    public Task<ApiResult<ExecutionView>> GetExecutionViewWithStatusAsync()
        => throw NotSupportedOnV1("GetExecutionViewWithStatusAsync (no execution_id)");

    public Task<ApiResult<TilesResponse>> GetTilesWithStatusAsync()
        => GetJsonWithStatusAsync<TilesResponse>("/v1/tiles");

    public Task<ApiResult<PendingPromptResponse>> GetPendingPromptWithStatusAsync()
        => GetJsonWithStatusAsync<PendingPromptResponse>("/v1/prompts/pending");

    public Task<ApiResult<TileQuotaResponse>> GetTileQuotaWithStatusAsync()
        => GetJsonWithStatusAsync<TileQuotaResponse>("/v1/quota/tiles");

    // Google Calendar integration: excluded from the current v1 recovery phase
    // per PROJECT-TRUTH. The desktop surfaces a clear "not supported" result
    // instead of silently calling a v0 /auth/integrations/* endpoint.
    public Task<IntegrationSettingsResponse?> GetIntegrationSettingsAsync()
        => throw NotSupportedOnV1("GetIntegrationSettingsAsync (Google Calendar excluded from v1 recovery)");

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
        => throw NotSupportedOnV1("UpdateGoogleCalendarIntegrationAsync (Google Calendar excluded from v1 recovery)");

    public Task<CalendarSyncPlanPreviewResponse?> GetCalendarSyncPlanPreviewAsync()
        => throw NotSupportedOnV1("GetCalendarSyncPlanPreviewAsync (Google Calendar excluded from v1 recovery)");

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