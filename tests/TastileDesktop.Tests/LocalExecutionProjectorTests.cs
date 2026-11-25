using System.Text.Json;
using Microsoft.Data.Sqlite;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class LocalExecutionProjectorTests : IDisposable
{
    private readonly string _dbPath;
    private readonly LocalReadModelStore _store;
    private readonly LocalExecutionProjector _projector;

    public LocalExecutionProjectorTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"tastile-proj-{Guid.NewGuid():N}.db");
        _store = new LocalReadModelStore(_dbPath);
        _projector = new LocalExecutionProjector(_store);
    }

    public void Dispose()
    {
        _store.Dispose();
        SqliteTestPools.Clear(_dbPath);
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static JsonElement State(string json)
        => JsonDocument.Parse(json).RootElement.Clone();

    private void Seed(params SyncEntity[] entities)
    {
        _store.BeginSnapshot();
        _store.StageSnapshotEntities(entities);
        _store.CommitSnapshot("cursor-1");
    }

    private static SyncEntity Entity(int kind, string id, string stateJson)
        => new(kind, id, State(stateJson));

    [Fact]
    public void GetLiveExecution_PrefersActive_ThenPaused_ThenNewest()
    {
        Seed(
            Entity(SyncEntityKind.Execution, "e-paused",
                """{"id":"e-paused","tile_id":"t1","state":1,"updated_at":"2026-11-24T10:00:00Z"}"""),
            Entity(SyncEntityKind.Execution, "e-done",
                """{"id":"e-done","tile_id":"t2","state":2,"updated_at":"2026-11-24T11:00:00Z"}"""),
            Entity(SyncEntityKind.Execution, "e-active",
                """{"id":"e-active","tile_id":"t3","placement_id":"p3","state":0,"revision":4,"updated_at":"2026-11-24T09:00:00Z"}"""));

        var live = _projector.GetLiveExecution();

        Assert.NotNull(live);
        Assert.Equal("e-active", live.ExecutionId);
        Assert.Equal("p3", live.PlacementId);
        Assert.True(live.IsLive);
        Assert.Equal("active", live.StateLabel);
        Assert.Equal(4, live.Revision);
    }

    [Fact]
    public void GetLiveExecution_FallsBackToPaused_WhenNoActive()
    {
        Seed(
            Entity(SyncEntityKind.Execution, "e-paused",
                """{"id":"e-paused","state":1,"updated_at":"2026-11-24T10:00:00Z"}"""),
            Entity(SyncEntityKind.Execution, "e-done",
                """{"id":"e-done","state":3,"updated_at":"2026-11-24T11:00:00Z"}"""));

        var live = _projector.GetLiveExecution();

        Assert.NotNull(live);
        Assert.Equal("e-paused", live.ExecutionId);
        Assert.Equal("paused", live.StateLabel);
    }

    [Fact]
    public void GetLiveExecution_ReturnsNull_WhenOnlyFinished()
    {
        Seed(Entity(SyncEntityKind.Execution, "e-done",
            """{"id":"e-done","state":2}"""));

        Assert.Null(_projector.GetLiveExecution());
    }

    [Fact]
    public void GetTile_ExtractsPlanIdFromDetailBase()
    {
        Seed(Entity(SyncEntityKind.Tile, "tile-1",
            """{"base":{"id":"tile-1","plan_id":"plan-9","title":"My task","kind":1,"revision":7},"labels":[],"recurring":null,"resolved_at":"2026-11-24T10:00:00Z"}"""));

        var tile = _projector.GetTile("tile-1");

        Assert.NotNull(tile);
        Assert.Equal("plan-9", tile.PlanId);
        Assert.Equal("My task", tile.Title);
        Assert.True(tile.HasExecutablePlan);
        Assert.Equal(7, tile.Revision);
    }

    [Fact]
    public void GetOpenPlacements_FiltersClosed_AndMatchesTile()
    {
        Seed(
            Entity(SyncEntityKind.Placement, "p-open",
                """{"id":"p-open","tile_id":"t1","plan_id":"plan-1","close":false,"span_start":"2026-11-24T13:00:00Z","span_end":"2026-11-24T14:00:00Z","revision":2}"""),
            Entity(SyncEntityKind.Placement, "p-closed",
                """{"id":"p-closed","tile_id":"t1","plan_id":"plan-1","close":true,"closed_at":"2026-11-24T12:00:00Z"}"""),
            Entity(SyncEntityKind.Placement, "p-other",
                """{"id":"p-other","tile_id":"t2","plan_id":"plan-2","close":false}"""));

        var open = _projector.GetOpenPlacements(tileId: "t1");

        var placement = Assert.Single(open);
        Assert.Equal("p-open", placement.PlacementId);
        Assert.False(placement.Closed);
    }

    [Fact]
    public void MalformedState_DoesNotThrow()
    {
        _store.BeginSnapshot();
        _store.StageSnapshotEntities(new[]
        {
            // Non-object state document (scalar JSON).
            new SyncEntity(SyncEntityKind.Execution, "scalar", State("42")),
            // Object missing every expected field.
            new SyncEntity(SyncEntityKind.Execution, "missing", State("""{"state":"weird"}""")),
        });
        _store.CommitSnapshot("cursor-1");

        // Both degrade to default-ish records rather than crashing the
        // projection; neither counts as a live execution.
        var executions = _projector.GetExecutions();
        Assert.Equal(2, executions.Count);
        Assert.All(executions, e => Assert.False(e.IsLive));
        Assert.Null(_projector.GetLiveExecution());
    }
}
