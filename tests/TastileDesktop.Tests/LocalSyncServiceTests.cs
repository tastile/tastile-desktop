using System.Net;
using System.Net.Http;
using System.Text.Json;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class LocalSyncServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly LocalReadModelStore _store;

    public LocalSyncServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"tastile-sync-svc-{Guid.NewGuid():N}.db");
        _store = new LocalReadModelStore(_dbPath);
    }

    public void Dispose()
    {
        _store.Dispose();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static CoreApiClient ClientFor(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3140") });

    private static HttpResponseMessage Json(HttpStatusCode status, object payload)
        => new(status) { Content = new StringContent(JsonSerializer.Serialize(payload)) };

    [Fact]
    public async Task SyncOnce_NoCursor_RunsSnapshotThenDeltaToTail()
    {
        var requests = new List<string>();
        var handler = new StubHandler(request =>
        {
            requests.Add(request.RequestUri!.PathAndQuery);
            if (request.RequestUri.AbsolutePath == "/v1/sync/snapshot")
            {
                return request.RequestUri.Query.Contains("cursor=page2")
                    ? Json(HttpStatusCode.OK, new
                    {
                        snapshot_cursor = "snap-cursor",
                        entities = new object[]
                        {
                            new { entity_kind = 0, entity_id = "t1", state = new { b = 1 } },
                        },
                        next_cursor = (string?)null,
                    })
                    : Json(HttpStatusCode.OK, new
                    {
                        snapshot_cursor = "snap-cursor",
                        entities = new object[]
                        {
                            new { entity_kind = 2, entity_id = "p1", state = new { tile_id = "t1" } },
                        },
                        next_cursor = "page2",
                    });
            }

            // /v1/sync delta: first page advances, second is the tail.
            return request.RequestUri.Query.Contains("cursor=snap-cursor")
                ? Json(HttpStatusCode.OK, new
                {
                    changes = new object[]
                    {
                        new { change_kind = 0, entity_kind = 3, entity_id = "e1", occurred_at = "2026-11-24T00:00:00Z", state = new { x = 1 } },
                    },
                    next_cursor = "delta-c1",
                })
                : Json(HttpStatusCode.OK, new { changes = Array.Empty<object>(), next_cursor = "" });
        });

        var service = new LocalSyncService(ClientFor(handler), _store);
        var result = await service.SyncOnceAsync();

        Assert.True(result.Success, result.Detail);
        Assert.Equal("delta-c1", _store.SyncCursor);
        Assert.Equal(3, _store.EntityCount());
        Assert.NotNull(_store.GetEntityState(SyncEntityKind.Execution, "e1"));
        // Snapshot was paged (2 requests) then delta pulled twice.
        Assert.Equal(2, requests.Count(r => r.StartsWith("/v1/sync/snapshot", StringComparison.Ordinal)));
        Assert.Equal(2, requests.Count(r => r.StartsWith("/v1/sync?", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task SyncOnce_CursorExpired_ResnapshotsOnce()
    {
        _store.BeginSnapshot();
        _store.StageSnapshotEntities(new[]
        {
            new SyncEntity(SyncEntityKind.Tile, "stale", JsonDocument.Parse("""{"base":{"title":"Stale"}}""").RootElement.Clone()),
        });
        _store.CommitSnapshot("old-cursor");

        var deltaCalls = 0;
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/sync/snapshot")
            {
                return Json(HttpStatusCode.OK, new
                {
                    snapshot_cursor = "fresh-cursor",
                    entities = Array.Empty<object>(),
                    next_cursor = (string?)null,
                });
            }

            deltaCalls++;
            return deltaCalls == 1
                ? new HttpResponseMessage(HttpStatusCode.Gone)
                : Json(HttpStatusCode.OK, new { changes = Array.Empty<object>(), next_cursor = "" });
        });

        var service = new LocalSyncService(ClientFor(handler), _store);
        var result = await service.SyncOnceAsync();

        Assert.True(result.Success, result.Detail);
        Assert.Equal("fresh-cursor", _store.SyncCursor);
        // Entities retained through resnapshot? Snapshot was empty → cleared.
        Assert.Equal(0, _store.EntityCount());
        Assert.Equal(2, deltaCalls);
    }

    [Fact]
    public async Task SyncOnce_ServerError_ReturnsFailed()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = new LocalSyncService(ClientFor(handler), _store);

        var result = await service.SyncOnceAsync();

        Assert.Equal(SyncOutcome.Failed, result.Outcome);
        Assert.Null(_store.SyncCursor);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
