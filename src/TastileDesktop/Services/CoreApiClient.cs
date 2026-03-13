namespace TastileDesktop.Services;

using System.Net.Http;
using System.Net.Http.Json;
using TastileDesktop.Models;

public class CoreApiClient
{
    private readonly HttpClient _httpClient;

    public CoreApiClient(string baseUrl = "http://localhost:3140")
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
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

    public async Task<CommandResponse?> AttachMemoAsync(string? tileId, string text)
        => await PostCommandAsync("/commands/memo/attach", new { tile_id = tileId, text });

    private async Task<CommandResponse?> PostCommandAsync<T>(string path, T body)
    {
        var response = await _httpClient.PostAsJsonAsync(path, body);
        return await response.Content.ReadFromJsonAsync<CommandResponse>();
    }
}
