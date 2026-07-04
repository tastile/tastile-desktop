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
            getAccessToken: () => Task.FromResult<string?>("access-token-1"));

        _ = await client.GetTilesAsync();

        Assert.Equal("Bearer access-token-1", capturedAuthorization);
    }

    [Fact]
    public async Task GetTilesAsync_RefreshesOnceAfterUnauthorized()
    {
        var seenAuthorization = new List<string?>();
        var attempts = 0;
        string currentToken = "expired-access-token";
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
                currentToken = "fresh-access-token";
                return Task.FromResult<AuthSession?>(new AuthSession(
                    IdToken: "fresh-id-token-must-not-leak",
                    AccessToken: "fresh-access-token",
                    RefreshToken: "refresh-token",
                    Sub: "user-1",
                    Email: "user@example.com",
                    ExpiresAt: DateTimeOffset.UtcNow.AddHours(1)));
            });

        var response = await client.GetTilesAsync();

        Assert.NotNull(response);
        Assert.Equal(2, attempts);
        // The 401 retry path must use the OAuth2 access token, never the
        // Cognito id_token (PROJECT-TRUTH §Authentication).
        Assert.Equal(["Bearer expired-access-token", "Bearer fresh-access-token"], seenAuthorization);
        Assert.DoesNotContain(seenAuthorization, header => header?.Contains("id-token-must-not-leak", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task UpdateTileAsync_ThrowsNotSupportedOnV1()
    {
        // v1 UpdateTilePayload uses (tile_id, title, description, color, icon,
        // external_id); the existing CreateTileRequest carries v0-shaped
        // fields that don't map. Surface the gap explicitly instead of
        // posting a body the server will reject with a confusing 400.
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
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

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => client.UpdateTileAsync("tile-1", request));
        Assert.Contains("UpdateTileAsync", ex.Message);
    }

    [Fact]
    public async Task StreamStateEventsAsync_NotSupportedOnV1()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (var _ in client.StreamStateEventsAsync())
            {
                // should never produce values
            }
        });
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
        Assert.Equal("/v1/calendar/year?anchor=2026-04-08T00%3A00%3A00.0000000Z&tz_offset=32400", capturedPathAndQuery);
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
