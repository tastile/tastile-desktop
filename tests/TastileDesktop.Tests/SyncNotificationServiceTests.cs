using System.Text.Json;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

/// <summary>
/// NF04-D07: sync-delta-driven OS notifications — schedule change /
/// cancel / execution finish / session decisions, freshness gating for
/// restart replay, and cross-restart dedupe.
/// </summary>
public sealed class SyncNotificationServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly LocalReadModelStore _store;
    private readonly List<SyncToastMessage> _toasts = new();
    private readonly DateTimeOffset _now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    public SyncNotificationServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"tastile-notify-{Guid.NewGuid():N}.db");
        _store = new LocalReadModelStore(_dbPath);
    }

    public void Dispose()
    {
        _store.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private SyncNotificationService CreateService()
    {
        // The notifier only uses sync.Store + the DeltaApplied event, so a
        // client that is never called is fine.
        var api = new CoreApiClient(new HttpClient { BaseAddress = new Uri("http://localhost:3140") });
        var sync = new LocalSyncService(api, _store);
        var service = new SyncNotificationService(sync, utcNow: () => _now);
        service.ToastRequested += (_, toast) => _toasts.Add(toast);
        return service;
    }

    private static JsonElement ToState(object value)
        => JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone();

    private static AppliedSyncChange Change(
        int entityKind,
        string entityId,
        object? state,
        string occurredAt,
        int changeKind = SyncChangeKind.Upsert,
        string? previousStateJson = null)
        => new(
            new SyncChange(
                ChangeKind: changeKind,
                EntityKind: entityKind,
                EntityId: entityId,
                OccurredAt: occurredAt,
                State: state is null ? null : ToState(state)),
            previousStateJson);

    private static object PlacementState(
        string tileId = "tile-1",
        string spanStart = "2026-11-25T10:00:00Z",
        string spanEnd = "2026-11-25T11:00:00Z",
        bool close = false,
        bool detach = false)
        => new
        {
            id = "p1",
            tile_id = tileId,
            plan_id = (string?)null,
            revision = 1L,
            span_start = spanStart,
            span_end = spanEnd,
            close,
            detach,
            created_at = "2026-11-25T09:00:00Z",
            updated_at = "2026-11-25T09:30:00Z",
        };

    private static string JsonOf(object value) => JsonSerializer.Serialize(value);

    /// <summary>Writes a TILE entity into the live read model (title lookup source).</summary>
    private void SeedTile(string tileId = "tile-1", string title = "Write report")
        => _store.ApplyDeltaPage(new[]
        {
            new SyncChange(
                ChangeKind: SyncChangeKind.Upsert,
                EntityKind: SyncEntityKind.Tile,
                EntityId: tileId,
                OccurredAt: "2026-11-25T08:00:00Z",
                State: ToState(new { @base = new { id = tileId, title } })),
        }, "seed-cursor");

    [Fact]
    public void PlacementSpanChange_ProducesSingleMovedToast()
    {
        SeedTile();
        var service = CreateService();
        var previous = JsonOf(PlacementState());
        var page = new SyncDeltaAppliedEventArgs(new[]
        {
            Change(
                SyncEntityKind.Placement, "p1",
                PlacementState(spanStart: "2026-11-25T14:00:00Z", spanEnd: "2026-11-25T15:00:00Z"),
                "2026-11-25T11:59:00Z",
                previousStateJson: previous),
        });

        service.ProcessDeltaPage(page);

        var toast = Assert.Single(_toasts);
        Assert.Equal("Write report", toast.Title);
        Assert.Contains("Moved", toast.Body);
        Assert.Equal("open", toast.ActionId);
    }

    [Fact]
    public void SameChangeDeliveredTwice_ProducesOneToast()
    {
        SeedTile();
        var service = CreateService();
        var previous = JsonOf(PlacementState());
        var moved = PlacementState(spanStart: "2026-11-25T14:00:00Z", spanEnd: "2026-11-25T15:00:00Z");
        var page = new SyncDeltaAppliedEventArgs(new[]
        {
            Change(SyncEntityKind.Placement, "p1", moved, "2026-11-25T11:59:00Z", previousStateJson: previous),
        });

        // Tail re-delivery / SSE+poller overlap: identical change twice.
        service.ProcessDeltaPage(page);
        service.ProcessDeltaPage(page);

        Assert.Single(_toasts);
    }

    [Fact]
    public void DedupeSurvivesServiceRestart()
    {
        SeedTile();
        var first = CreateService();
        var previous = JsonOf(PlacementState());
        var page = new SyncDeltaAppliedEventArgs(new[]
        {
            Change(
                SyncEntityKind.Placement, "p1",
                PlacementState(spanStart: "2026-11-25T14:00:00Z"),
                "2026-11-25T11:59:00Z",
                previousStateJson: previous),
        });
        first.ProcessDeltaPage(page);
        Assert.Single(_toasts);

        // Simulate app restart: a brand-new service over the same store
        // must not re-toast the same underlying change.
        var second = CreateService();
        second.ProcessDeltaPage(page);
        Assert.Single(_toasts);
    }

    [Fact]
    public void StaleOccurredAt_IsSuppressed()
    {
        SeedTile();
        var service = CreateService();
        var page = new SyncDeltaAppliedEventArgs(new[]
        {
            // 3 hours old — the app was closed when this change happened
            // and it is outside the freshness window: no stale replay.
            Change(
                SyncEntityKind.Placement, "p1",
                PlacementState(spanStart: "2026-11-25T14:00:00Z"),
                "2026-11-25T09:00:00Z",
                previousStateJson: JsonOf(PlacementState())),
        });

        service.ProcessDeltaPage(page);

        Assert.Empty(_toasts);
    }

    [Fact]
    public void PlacementClose_ProducesCancelledToast()
    {
        SeedTile();
        var service = CreateService();
        var page = new SyncDeltaAppliedEventArgs(new[]
        {
            Change(
                SyncEntityKind.Placement, "p1",
                PlacementState(close: true),
                "2026-11-25T11:59:00Z",
                previousStateJson: JsonOf(PlacementState())),
        });

        service.ProcessDeltaPage(page);

        var toast = Assert.Single(_toasts);
        Assert.Contains("cancelled", toast.Body);
    }

    [Fact]
    public void PlacementTombstone_ProducesCancelledToast()
    {
        SeedTile();
        var service = CreateService();
        var page = new SyncDeltaAppliedEventArgs(new[]
        {
            Change(
                SyncEntityKind.Placement, "p1", null,
                "2026-11-25T11:59:00Z",
                changeKind: SyncChangeKind.Tombstone,
                previousStateJson: JsonOf(PlacementState())),
        });

        service.ProcessDeltaPage(page);

        Assert.Single(_toasts);
    }

    [Fact]
    public void NewPlacement_ProducesScheduledToast()
    {
        SeedTile();
        var service = CreateService();
        var page = new SyncDeltaAppliedEventArgs(new[]
        {
            Change(SyncEntityKind.Placement, "p1", PlacementState(), "2026-11-25T11:59:00Z"),
        });

        service.ProcessDeltaPage(page);

        var toast = Assert.Single(_toasts);
        Assert.Contains("Scheduled", toast.Body);
    }

    [Fact]
    public void RevisionOnlyChange_ProducesNoToast()
    {
        SeedTile();
        var service = CreateService();
        var previous = JsonOf(PlacementState());
        var bumped = new
        {
            id = "p1",
            tile_id = "tile-1",
            revision = 2L,
            span_start = "2026-11-25T10:00:00Z",
            span_end = "2026-11-25T11:00:00Z",
            close = false,
            detach = false,
        };
        var page = new SyncDeltaAppliedEventArgs(new[]
        {
            Change(SyncEntityKind.Placement, "p1", bumped, "2026-11-25T11:59:00Z", previousStateJson: previous),
        });

        service.ProcessDeltaPage(page);

        Assert.Empty(_toasts);
    }

    [Fact]
    public void ExecutionFinishVoid_ProducesCancelledToast()
    {
        SeedTile();
        var service = CreateService();
        var previous = JsonOf(new { id = "e1", tile_id = "tile-1", state = 0, revision = 1L });
        var page = new SyncDeltaAppliedEventArgs(new[]
        {
            Change(
                SyncEntityKind.Execution, "e1",
                new { id = "e1", tile_id = "tile-1", state = ExecutionStateValue.FinishedVoid, revision = 2L },
                "2026-11-25T11:59:00Z",
                previousStateJson: previous),
        });

        service.ProcessDeltaPage(page);

        var toast = Assert.Single(_toasts);
        Assert.Contains("cancelled", toast.Body);
    }

    [Fact]
    public void ExecutionAlreadyFinished_DoesNotReToast()
    {
        SeedTile();
        var service = CreateService();
        var finished = JsonOf(new { id = "e1", tile_id = "tile-1", state = ExecutionStateValue.FinishedNormal, revision = 2L });
        var page = new SyncDeltaAppliedEventArgs(new[]
        {
            Change(
                SyncEntityKind.Execution, "e1",
                new { id = "e1", tile_id = "tile-1", state = ExecutionStateValue.FinishedNormal, revision = 3L },
                "2026-11-25T11:59:30Z",
                previousStateJson: finished),
        });

        service.ProcessDeltaPage(page);

        Assert.Empty(_toasts);
    }

    [Fact]
    public void NewOpenSession_ProducesDecisionToast()
    {
        var service = CreateService();
        var page = new SyncDeltaAppliedEventArgs(new[]
        {
            Change(
                SyncEntityKind.Session, "sess-1",
                new { id = "sess-1", state = 0, revision = 1, created_at = "2026-11-25T11:00:00Z" },
                "2026-11-25T11:59:00Z"),
        });

        service.ProcessDeltaPage(page);

        var toast = Assert.Single(_toasts);
        Assert.Contains("decision", toast.Body);
    }

    [Fact]
    public void TileAndSourceTileDeltas_NeverToast()
    {
        var service = CreateService();
        var page = new SyncDeltaAppliedEventArgs(new[]
        {
            Change(SyncEntityKind.Tile, "t1", new { @base = new { title = "X" } }, "2026-11-25T11:59:00Z"),
            Change(SyncEntityKind.SourceTile, "s1", new { source = new { } }, "2026-11-25T11:59:00Z"),
        });

        service.ProcessDeltaPage(page);

        Assert.Empty(_toasts);
    }

    [Fact]
    public void DeltaAppliedEvent_IsRaisedByLocalSyncService()
    {
        // Integration: a real delta pull through LocalSyncService must
        // surface AppliedSyncChange entries with previous states.
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/sync/snapshot")
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        snapshot_cursor = "snap",
                        entities = Array.Empty<object>(),
                        next_cursor = (string?)null,
                    })),
                };
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    changes = new[]
                    {
                        new
                        {
                            change_kind = 0,
                            entity_kind = SyncEntityKind.Placement,
                            entity_id = "p1",
                            occurred_at = "2026-11-25T11:59:00Z",
                            state = PlacementState(),
                        },
                    },
                    next_cursor = (string?)null,
                })),
            };
        });
        var sync = new LocalSyncService(
            new CoreApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3140") }),
            _store);
        SyncDeltaAppliedEventArgs? seen = null;
        sync.DeltaApplied += (_, e) => seen = e;

        var result = sync.SyncOnceAsync().GetAwaiter().GetResult();

        Assert.True(result.Success, result.Detail);
        Assert.NotNull(seen);
        var applied = Assert.Single(seen!.Changes);
        Assert.Equal("p1", applied.Change.EntityId);
        Assert.Null(applied.PreviousStateJson);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_respond(request));
    }
}
