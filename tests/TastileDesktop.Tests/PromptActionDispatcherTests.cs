using System.Net;
using System.Text.Json;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class PromptActionDispatcherTests
{
    [Fact]
    public async Task ExecuteAsync_StartBreakParallel_UsesProvidedBreakMinutes()
    {
        string? capturedPath = null;
        string? capturedBody = null;
        var api = CreateApiClient(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "ok": true, "events": [] }"""),
            };
        });

        var result = await PromptActionDispatcher.ExecuteAsync(
            api,
            BuildPrompt("tile-1", "START_BREAK_PARALLEL"),
            requestedActionId: "START_BREAK_PARALLEL",
            stopAt: null,
            defaultBreakMinutes: 12);

        Assert.True(result.IsResolved);
        Assert.Null(result.Error);
        Assert.Equal("/commands/break/start", capturedPath);
        Assert.NotNull(capturedBody);
        using var json = JsonDocument.Parse(capturedBody!);
        Assert.Equal(12, json.RootElement.GetProperty("break_min").GetInt32());
        Assert.Equal("parallel", json.RootElement.GetProperty("insertion_mode").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_CompletePhase_UsesFallbackTileIdAndCompletesPhase()
    {
        string? capturedPath = null;
        string? capturedBody = null;
        var api = CreateApiClient(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "ok": true, "events": [] }"""),
            };
        });

        var result = await PromptActionDispatcher.ExecuteAsync(
            api,
            BuildPrompt(null, "COMPLETE_PHASE"),
            requestedActionId: "COMPLETE_PHASE",
            stopAt: null,
            fallbackTileId: "tile-fallback");

        Assert.True(result.IsResolved);
        Assert.Null(result.Error);
        Assert.Equal("/commands/tile/complete", capturedPath);
        Assert.NotNull(capturedBody);
        using var json = JsonDocument.Parse(capturedBody!);
        Assert.Equal("tile-fallback", json.RootElement.GetProperty("tile_id").GetString());
        Assert.Equal("phase", json.RootElement.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_StartupRecoveryAction_UsesStartupRecoveryEndpoint()
    {
        string? capturedPath = null;
        string? capturedBody = null;
        var api = CreateApiClient(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "ok": true, "events": [] }"""),
            };
        });

        var result = await PromptActionDispatcher.ExecuteAsync(
            api,
            BuildPrompt("tile-1", "CONFIRM_STOP_AT"),
            requestedActionId: "CONFIRM_STOP_AT",
            stopAt: null);

        Assert.True(result.IsResolved);
        Assert.Null(result.Error);
        Assert.Equal("/commands/prompt/respond-startup-recovery", capturedPath);
        Assert.NotNull(capturedBody);
        using var json = JsonDocument.Parse(capturedBody!);
        Assert.Equal("CONFIRM_STOP_AT", json.RootElement.GetProperty("action_id").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("stop_at").ValueKind);
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedAction_ReturnsErrorWithoutApiCall()
    {
        var requestCount = 0;
        var api = CreateApiClient(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "ok": true, "events": [] }"""),
            };
        });

        var result = await PromptActionDispatcher.ExecuteAsync(
            api,
            BuildPrompt("tile-1", "UNSUPPORTED_ACTION"),
            requestedActionId: "UNSUPPORTED_ACTION",
            stopAt: null);

        Assert.True(result.IsResolved);
        Assert.Equal("UNSUPPORTED_ACTION", result.ResolvedActionId);
        Assert.NotNull(result.Error);
        Assert.Contains("unsupported prompt action", result.Error!);
        Assert.Equal(0, requestCount);
    }

    private static PromptView BuildPrompt(string? tileId, params string[] actions)
    {
        return new PromptView(
            PromptId: "prompt-1",
            Kind: "end",
            Severity: "info",
            TileId: tileId,
            Title: "Prompt",
            Body: "Body",
            Why: string.Empty,
            SuggestedMinutes: null,
            Actions: actions.Select(action => new PromptActionView(action, action)).ToList(),
            CreatedAt: "2026-04-01T00:00:00Z",
            ExpiresAt: null,
            Stale: false);
    }

    private static CoreApiClient CreateApiClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        return new CoreApiClient(new HttpClient(new StubHandler(responder))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
