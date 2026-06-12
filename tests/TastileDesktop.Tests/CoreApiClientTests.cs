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
    public async Task GetTilesAsync_AttachesBearerToken()
    {
        string? capturedAuthorization = null;
        var client = new CoreApiClient(
            new HttpClient(new StubHandler(request =>
            {
                capturedAuthorization = request.Headers.Authorization?.ToString();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{ "tiles": [] }"""),
                };
            }))
            {
                BaseAddress = new Uri("http://localhost:3140"),
            },
            getAccessToken: () => Task.FromResult<string?>("id-token-1"));

        _ = await client.GetTilesAsync();

        Assert.Equal("Bearer id-token-1", capturedAuthorization);
    }

    [Fact]
    public async Task GetTilesAsync_RefreshesOnceAfterUnauthorized()
    {
        var seenAuthorization = new List<string?>();
        var attempts = 0;
        string currentToken = "expired-id-token";
        var client = new CoreApiClient(
            new HttpClient(new StubHandler(request =>
            {
                attempts++;
                seenAuthorization.Add(request.Headers.Authorization?.ToString());
                return attempts == 1
                    ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{ "tiles": [] }"""),
                    };
            }))
            {
                BaseAddress = new Uri("http://localhost:3140"),
            },
            getAccessToken: () => Task.FromResult<string?>(currentToken),
            refreshTokens: () =>
            {
                currentToken = "fresh-id-token";
                return Task.FromResult<AuthSession?>(new AuthSession(
                    IdToken: "fresh-id-token",
                    AccessToken: "fresh-access-token",
                    RefreshToken: "refresh-token",
                    Sub: "user-1",
                    Email: "user@example.com",
                    ExpiresAt: DateTimeOffset.UtcNow.AddHours(1)));
            });

        var response = await client.GetTilesAsync();

        Assert.NotNull(response);
        Assert.Equal(2, attempts);
        Assert.Equal(["Bearer expired-id-token", "Bearer fresh-id-token"], seenAuthorization);
    }

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

    [Fact]
    public async Task GetTimelineForViewportAsync_UsesYearEndpoint_ForYearRange()
    {
        string? capturedPathAndQuery = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedPathAndQuery = request.RequestUri?.PathAndQuery;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "view": "year",
                  "range_start": "2026-01-01T00:00:00Z",
                  "range_end": "2027-01-01T00:00:00Z",
                  "grid_start": "2026-01-01T00:00:00Z",
                  "grid_end": "2027-01-01T00:00:00Z",
                  "blocks": [
                    {
                      "tile_id": "tile-1",
                      "title": "Planning",
                      "start_at": "2026-04-08T01:00:00Z",
                      "end_at": "2026-04-08T02:00:00Z",
                      "semantic_role": "work",
                      "all_day": false,
                      "ownership": "tastile_owned",
                      "editable": true,
                      "source_label": "tastile"
                    }
                  ],
                  "all_day_spans": [],
                  "overflow_counters": {},
                  "month_summaries": []
                }
                """),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var viewport = new TimelineViewportSettings(
            ScaleUnit: TimelineScaleUnit.Month,
            RangeMode: TimelineRangeMode.Year1,
            AnchorLocal: new DateTimeOffset(2026, 4, 8, 9, 0, 0, TimeSpan.FromHours(9)));
        var timeline = await client.GetTimelineForViewportAsync(viewport);

        Assert.NotNull(timeline);
        Assert.Equal("/views/calendar/year?anchor=2026-04-08T00%3A00%3A00.0000000Z&tz_offset=32400", capturedPathAndQuery);
        Assert.Single(timeline!.Items);
        Assert.Equal("tile-1", timeline.Items[0].TileId);
    }

    [Fact]
    public async Task GetTimelineForViewportAsync_WeekView_ExcludesAllDaySpans()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "view": "week",
                  "range_start": "2026-04-06T00:00:00Z",
                  "range_end": "2026-04-13T00:00:00Z",
                  "grid_start": "2026-04-06T00:00:00Z",
                  "grid_end": "2026-04-13T00:00:00Z",
                  "blocks": [
                    {
                      "tile_id": "tile-foreground",
                      "title": "Foreground card",
                      "start_at": "2026-04-08T01:00:00Z",
                      "end_at": "2026-04-08T02:00:00Z",
                      "semantic_role": "work",
                      "all_day": false
                    }
                  ],
                  "all_day_spans": [
                    {
                      "tile_id": "tile-background",
                      "title": "All day background",
                      "start_at": "2026-04-08T00:00:00Z",
                      "end_at": "2026-04-09T00:00:00Z",
                      "semantic_role": "work",
                      "all_day": true
                    }
                  ]
                }
                """),
            }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var viewport = new TimelineViewportSettings(
            ScaleUnit: TimelineScaleUnit.Week,
            RangeMode: TimelineRangeMode.Week1,
            AnchorLocal: new DateTimeOffset(2026, 4, 8, 9, 0, 0, TimeSpan.FromHours(9)));

        var timeline = await client.GetTimelineForViewportAsync(viewport);

        Assert.NotNull(timeline);
        Assert.Single(timeline!.Items);
        Assert.Equal("tile-foreground", timeline.Items[0].TileId);
        Assert.DoesNotContain(timeline.Items, item => item.TileId == "tile-background");
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
