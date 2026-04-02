using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class CoreApiClientStartupRecoveryTests
{
    [Fact]
    public async Task RespondStartupRecoveryPromptAsync_SendsRequiredFields()
    {
        string? capturedPath = null;
        string? capturedBody = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "ok": true, "events": [], "tile_id": null }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.RespondStartupRecoveryPromptAsync(
            promptId: "p-1",
            tileId: "t-1",
            actionId: "CONFIRM_CONTINUE");

        Assert.Equal("/commands/prompt/respond-startup-recovery", capturedPath);
        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        Assert.Equal("p-1", root.GetProperty("prompt_id").GetString());
        Assert.Equal("t-1", root.GetProperty("tile_id").GetString());
        Assert.Equal("CONFIRM_CONTINUE", root.GetProperty("action_id").GetString());
        Assert.True(root.TryGetProperty("stop_at", out var stopAtProp));
        Assert.Equal(JsonValueKind.Null, stopAtProp.ValueKind);
    }

    [Fact]
    public async Task RespondStartupRecoveryPromptAsync_SerializesStopAtAsUtcIso8601()
    {
        string? capturedBody = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "ok": true, "events": [], "tile_id": null }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var stopAt = new DateTimeOffset(2026, 4, 2, 9, 15, 0, TimeSpan.FromHours(9));
        _ = await client.RespondStartupRecoveryPromptAsync(
            promptId: "p-2",
            tileId: "t-2",
            actionId: "CONFIRM_STOP_AT",
            stopAt: stopAt);

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        Assert.Equal("2026-04-02T00:15:00.0000000Z", root.GetProperty("stop_at").GetString());
    }

    [Fact]
    public async Task CompleteTileAsync_SendsTileIdWhenProvided()
    {
        string? capturedPath = null;
        string? capturedBody = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "ok": true, "events": [], "tile_id": null }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.CompleteTileAsync(tileId: "tile-123");

        Assert.Equal("/commands/tile/complete", capturedPath);
        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        Assert.Equal("tile-123", root.GetProperty("tile_id").GetString());
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
