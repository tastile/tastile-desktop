using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class CoreApiClientTests
{
    [Fact]
    public async Task UpdateTileAsync_PostsToUpdateEndpoint_WithSameTileId()
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

        var request = new CreateTileRequest(
            Title: "Updated title",
            NextAction: "Updated action",
            DoneDefinition: "Updated done",
            Temporal: null,
            Objective: null,
            Interruption: null,
            Automation: null,
            Annotation: null,
            ConflictResolution: null);

        _ = await client.UpdateTileAsync("tile-1", request);

        Assert.Equal("/commands/tile/update", capturedPath);
        Assert.NotNull(capturedBody);
        using var json = JsonDocument.Parse(capturedBody!);
        var root = json.RootElement;
        Assert.Equal("tile-1", root.GetProperty("tile_id").GetString());
        Assert.Equal("Updated title", root.GetProperty("title").GetString());
        Assert.Equal("Updated action", root.GetProperty("next_action").GetString());
        Assert.Equal("Updated done", root.GetProperty("done_definition").GetString());
    }

    [Fact]
    public async Task StreamStateEventsAsync_YieldsDataLines()
    {
        var sse = "event: state_changed\n" +
                  "data: state_changed\n\n" +
                  "event: state_changed\n" +
                  "data: another\n\n";
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse),
            }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var received = new List<string>();
        await foreach (var message in client.StreamStateEventsAsync())
        {
            received.Add(message);
        }

        Assert.Equal(["state_changed", "another"], received);
    }

    [Fact]
    public async Task StreamStateEventsAsync_SupportsProjectedStartPayload()
    {
        var sse = "event: state_changed\n" +
                  "data: {\"reason\":\"projection_only\"}\n\n";
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse),
            }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var received = new List<string>();
        await foreach (var message in client.StreamStateEventsAsync())
        {
            received.Add(message);
        }

        Assert.Single(received);
        Assert.Contains("projection_only", received[0], StringComparison.Ordinal);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
