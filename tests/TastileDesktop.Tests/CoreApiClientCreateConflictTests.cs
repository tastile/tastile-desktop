using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class CoreApiClientCreateConflictTests
{
    [Fact]
    public async Task CreateTileAsync_ParsesConflictPromptFromConflictResponse()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/commands/tile/create")
            {
                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent("""
                    {
                      "ok": false,
                      "events": [],
                      "prompt": {
                        "kind": "create_conflict",
                        "title": "Fixed time conflict detected",
                        "options": [
                          { "id": "keep_overlap", "label": "Keep overlap" },
                          { "id": "auto_nearest", "label": "Move to nearest free slot" }
                        ]
                      },
                      "error": "fixed-time conflict detected"
                    }
                    """),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var response = await client.CreateTileAsync(new CreateTileRequest(
            Title: "Fixed",
            NextAction: null,
            DoneDefinition: null,
            Temporal: new CreateTileTemporalRequest(null, null, DateTimeOffset.UtcNow.ToString("O"), DateTimeOffset.UtcNow.AddHours(1).ToString("O"), null, null),
            Objective: null,
            Interruption: null,
            Automation: null,
            Annotation: null,
            ConflictResolution: null));

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.NotNull(response.Prompt);
        Assert.Equal("create_conflict", response.Prompt!.Kind);
        Assert.NotNull(response.Prompt.Options);
        Assert.Contains(response.Prompt.Options!, option => option.Id == "auto_nearest");
    }

    [Fact]
    public async Task CreateTileAsync_SendsConflictResolutionWhenSpecified()
    {
        string? capturedJson = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/commands/tile/create")
            {
                capturedJson = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""
                    { "ok": true, "events": [], "tile_id": "x" }
                    """),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.CreateTileAsync(new CreateTileRequest(
            Title: "Fixed",
            NextAction: null,
            DoneDefinition: null,
            Temporal: new CreateTileTemporalRequest(null, null, DateTimeOffset.UtcNow.ToString("O"), DateTimeOffset.UtcNow.AddHours(1).ToString("O"), null, null),
            Objective: null,
            Interruption: null,
            Automation: null,
            Annotation: null,
            ConflictResolution: "auto_nearest"));

        Assert.NotNull(capturedJson);
        using var json = JsonDocument.Parse(capturedJson!);
        Assert.Equal("auto_nearest", json.RootElement.GetProperty("conflict_resolution").GetString());
    }

    [Fact]
    public async Task CreateTileAsync_ParsesRecurringFixedCreateConflictPrompt()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/commands/tile/create")
            {
                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent("""
                    {
                      "ok": false,
                      "events": [],
                      "prompt": {
                        "kind": "create_conflict",
                        "title": "Fixed time conflict detected",
                        "options": [
                          { "id": "keep_overlap", "label": "Keep overlap" },
                          { "id": "auto_nearest", "label": "Move to nearest free slot" },
                          { "id": "auto_next_day", "label": "Move to next day" },
                          { "id": "manual_adjust", "label": "Adjust manually" }
                        ]
                      },
                      "error": "fixed-time conflict detected"
                    }
                    """),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var response = await client.CreateTileAsync(new CreateTileRequest(
            Title: "Recurring fixed",
            NextAction: null,
            DoneDefinition: null,
            Temporal: new CreateTileTemporalRequest(null, null, null, null, null, null),
            Objective: new CreateTileObjectiveRequest(
                ObjectiveMode: "recurring",
                TargetWorkMin: 60,
                TargetRestMin: null,
                DoneRule: "time_reached",
                Recurrence: new CreateTileRecurrenceRequest(
                    Generator: new CreateTileRecurrenceGeneratorRequest(1440, null),
                    Window: new CreateTileRecurrenceWindowRequest(540, 600),
                    Selector: new CreateTileRecurrenceSelectorRequest("freq=daily;interval=1"))),
            Interruption: null,
            Automation: null,
            Annotation: null,
            ConflictResolution: null));

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("create_conflict", response.Prompt?.Kind);
        Assert.Contains(response.Prompt!.Options!, option => option.Id == "keep_overlap");
        Assert.Contains(response.Prompt.Options!, option => option.Id == "manual_adjust");
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
