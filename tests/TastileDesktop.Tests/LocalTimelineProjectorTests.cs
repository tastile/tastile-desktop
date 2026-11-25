using System.Text.Json;
using Microsoft.Data.Sqlite;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class LocalTimelineProjectorTests : IDisposable
{
    private readonly string _dbPath;
    private readonly LocalReadModelStore _store;

    public LocalTimelineProjectorTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"tastile-proj-{Guid.NewGuid():N}.db");
        _store = new LocalReadModelStore(_dbPath);
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

    private void Seed(params SyncEntity[] entities)
    {
        _store.BeginSnapshot();
        _store.StageSnapshotEntities(entities);
        _store.CommitSnapshot("c0");
    }

    private static SyncEntity Entity(int kind, string id, string stateJson)
        => new(kind, id, JsonDocument.Parse(stateJson).RootElement.Clone());

    // Anchor: 2026-11-24 (Tuesday) local noon, UTC+9.
    private static readonly DateTimeOffset Anchor = new(2026, 11, 24, 12, 0, 0, TimeSpan.FromHours(9));

    [Fact]
    public void Project_EmptyStore_ReturnsNull()
    {
        Assert.Null(LocalTimelineProjector.Project(_store, Anchor));
    }

    [Fact]
    public void Project_PlacementsOverlappingDay_BecomeTimelineItems()
    {
        Seed(
            Entity(SyncEntityKind.Tile, "t1", """{"base":{"id":"t1","title":"Write spec"}}"""),
            Entity(SyncEntityKind.Placement, "p1", """{"id":"p1","tile_id":"t1","span_start":"2026-11-23T16:00:00Z","span_end":"2026-11-23T17:00:00Z","close":false,"detach":false}"""),
            Entity(SyncEntityKind.Placement, "p2", """{"id":"p2","tile_id":"t1","span_start":"2026-11-25T16:00:00Z","span_end":"2026-11-25T17:00:00Z","close":false,"detach":false}"""),
            Entity(SyncEntityKind.Placement, "p3", """{"id":"p3","tile_id":"t1","span_start":"2026-11-24T01:00:00Z","span_end":"2026-11-24T02:00:00Z","close":true,"detach":false}"""));

        var result = LocalTimelineProjector.Project(_store, Anchor);

        Assert.NotNull(result);
        Assert.True(result!.LocalSnapshot);
        Assert.NotNull(result.DataAsOf);
        var item = Assert.Single(result.Items);
        Assert.Equal("t1", item.TileId);
        Assert.Equal("Write spec", item.Title);
        Assert.Equal("2026-11-23T16:00:00Z", item.StartedAt);
        Assert.False(item.IsActive);
    }

    [Fact]
    public void Project_ClosedAndDetachedPlacements_AreExcluded()
    {
        Seed(
            Entity(SyncEntityKind.Tile, "t1", """{"base":{"title":"T"}}"""),
            Entity(SyncEntityKind.Placement, "p1", """{"id":"p1","tile_id":"t1","span_start":"2026-11-24T01:00:00Z","span_end":"2026-11-24T02:00:00Z","close":false,"detach":true}"""),
            Entity(SyncEntityKind.Placement, "p2", """{"id":"p2","tile_id":"t1","span_start":"2026-11-24T03:00:00Z","span_end":"2026-11-24T04:00:00Z","close":true,"detach":false}"""));

        var result = LocalTimelineProjector.Project(_store, Anchor);

        Assert.NotNull(result);
        Assert.Empty(result!.Items);
    }

    [Fact]
    public void Project_OpenExecution_MarksPlacementActive()
    {
        Seed(
            Entity(SyncEntityKind.Tile, "t1", """{"base":{"title":"Deep work"}}"""),
            Entity(SyncEntityKind.Placement, "p1", """{"id":"p1","tile_id":"t1","span_start":"2026-11-24T01:00:00Z","span_end":"2026-11-24T03:00:00Z","close":false,"detach":false}"""),
            Entity(SyncEntityKind.Execution, "e1", """{"id":"e1","state":0,"open_segment_start":"2026-11-24T01:05:00Z","placement_id":"p1","effective_placement_id":null}"""));

        var result = LocalTimelineProjector.Project(_store, Anchor);

        var item = Assert.Single(result!.Items);
        Assert.True(item.IsActive);
    }

    [Fact]
    public void Project_FinishedExecution_DoesNotMarkActive()
    {
        Seed(
            Entity(SyncEntityKind.Tile, "t1", """{"base":{"title":"T"}}"""),
            Entity(SyncEntityKind.Placement, "p1", """{"id":"p1","tile_id":"t1","span_start":"2026-11-24T01:00:00Z","span_end":"2026-11-24T03:00:00Z","close":false,"detach":false}"""),
            Entity(SyncEntityKind.Execution, "e1", """{"id":"e1","state":2,"open_segment_start":null,"placement_id":"p1"}"""));

        var result = LocalTimelineProjector.Project(_store, Anchor);

        Assert.False(Assert.Single(result!.Items).IsActive);
    }
}
