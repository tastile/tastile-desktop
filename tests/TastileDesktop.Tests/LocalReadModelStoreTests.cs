using System.Text.Json;
using Microsoft.Data.Sqlite;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class LocalReadModelStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly LocalReadModelStore _store;

    public LocalReadModelStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"tastile-sync-test-{Guid.NewGuid():N}.db");
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

    private static SyncEntity Entity(int kind, string id, string stateJson)
        => new(kind, id, JsonDocument.Parse(stateJson).RootElement.Clone());

    private static SyncChange Change(int kind, string id, string? stateJson, int changeKind = SyncChangeKind.Upsert)
        => new(
            changeKind,
            kind,
            id,
            "2026-11-24T00:00:00Z",
            stateJson is null ? null : JsonDocument.Parse(stateJson).RootElement.Clone());

    [Fact]
    public void CommitSnapshot_ReplacesEntitiesAndSeedsCursor()
    {
        _store.BeginSnapshot();
        _store.StageSnapshotEntities(new[]
        {
            Entity(SyncEntityKind.Tile, "tile-1", """{"base":{"title":"Task A"}}"""),
            Entity(SyncEntityKind.Placement, "pl-1", """{"id":"pl-1","tile_id":"tile-1","span_start":"2026-11-24T01:00:00Z","span_end":"2026-11-24T02:00:00Z"}"""),
        });

        // Staging is not visible before commit.
        Assert.Equal(0, _store.EntityCount());

        _store.CommitSnapshot("cursor-snap-1");

        Assert.Equal(2, _store.EntityCount());
        Assert.Equal("cursor-snap-1", _store.SyncCursor);
        Assert.NotNull(_store.LastSyncAt);
        var state = _store.GetEntityState(SyncEntityKind.Tile, "tile-1");
        Assert.NotNull(state);
        Assert.Equal("Task A", state!.Value.GetProperty("base").GetProperty("title").GetString());
    }

    [Fact]
    public void CommitSnapshot_SecondWalkReplacesStaleEntities()
    {
        _store.BeginSnapshot();
        _store.StageSnapshotEntities(new[] { Entity(SyncEntityKind.Tile, "old", """{"base":{"title":"Old"}}""") });
        _store.CommitSnapshot("c1");

        _store.BeginSnapshot();
        _store.StageSnapshotEntities(new[] { Entity(SyncEntityKind.Tile, "new", """{"base":{"title":"New"}}""") });
        _store.CommitSnapshot("c2");

        Assert.Equal(1, _store.EntityCount());
        Assert.Null(_store.GetEntityState(SyncEntityKind.Tile, "old"));
        Assert.NotNull(_store.GetEntityState(SyncEntityKind.Tile, "new"));
        Assert.Equal("c2", _store.SyncCursor);
    }

    [Fact]
    public void ApplyDeltaPage_UpsertsAndAdvancesCursorInOrder()
    {
        _store.CommitSnapshot("c0");

        _store.ApplyDeltaPage(new[]
        {
            Change(SyncEntityKind.Tile, "t1", """{"base":{"title":"V1"}}"""),
            Change(SyncEntityKind.Tile, "t1", """{"base":{"title":"V2"}}"""),
        }, "c1");

        Assert.Equal("c1", _store.SyncCursor);
        var state = _store.GetEntityState(SyncEntityKind.Tile, "t1");
        Assert.Equal("V2", state!.Value.GetProperty("base").GetProperty("title").GetString());
    }

    [Fact]
    public void ApplyDeltaPage_TombstoneDeletesEntity()
    {
        _store.BeginSnapshot();
        _store.StageSnapshotEntities(new[] { Entity(SyncEntityKind.Tile, "t1", """{"base":{"title":"X"}}""") });
        _store.CommitSnapshot("c0");

        _store.ApplyDeltaPage(new[] { Change(SyncEntityKind.Tile, "t1", null, SyncChangeKind.Tombstone) }, "c1");

        Assert.Equal(0, _store.EntityCount());
        Assert.Equal("c1", _store.SyncCursor);
    }

    [Fact]
    public void ApplyDeltaPage_EmptyNextCursorKeepsPersistedCursor()
    {
        _store.CommitSnapshot("c0");
        _store.ApplyDeltaPage(Array.Empty<SyncChange>(), "");

        // Tail page carries no cursor; the last non-empty cursor stays.
        Assert.Equal("c0", _store.SyncCursor);
    }

    [Fact]
    public void ResetSyncCursor_KeepsEntitiesForOfflineReads()
    {
        _store.BeginSnapshot();
        _store.StageSnapshotEntities(new[] { Entity(SyncEntityKind.Tile, "t1", """{"base":{"title":"X"}}""") });
        _store.CommitSnapshot("c0");

        _store.ResetSyncCursor();

        Assert.Null(_store.SyncCursor);
        Assert.Equal(1, _store.EntityCount());
    }

    [Fact]
    public void ClearAll_WipesEverything()
    {
        _store.BeginSnapshot();
        _store.StageSnapshotEntities(new[] { Entity(SyncEntityKind.Tile, "t1", """{"base":{"title":"X"}}""") });
        _store.CommitSnapshot("c0");

        _store.ClearAll();

        Assert.Equal(0, _store.EntityCount());
        Assert.Null(_store.SyncCursor);
        Assert.Null(_store.LastSyncAt);
    }
}
