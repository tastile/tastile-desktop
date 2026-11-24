using Microsoft.Data.Sqlite;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class CommandOutboxTests : IDisposable
{
    private readonly string _dbPath;
    private readonly CommandOutboxStore _store;
    private readonly CommandOutbox _outbox;

    public CommandOutboxTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"tastile-outbox-{Guid.NewGuid():N}.db");
        _store = new CommandOutboxStore(_dbPath);
        _outbox = new CommandOutbox(_store);
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

    private static OutboxEnqueueRequest Request(string key, string kind = "execution.start")
        => new(kind, "POST", $"/v1/test/{key}", "{\"idempotency_key\":\"" + key + "\",\"payload\":{}}", key, "2026-11-24T00:00:00Z");

    [Fact]
    public void Enqueue_PersistsInOrder_AndDedupsByIdempotencyKey()
    {
        var seq1 = _outbox.Enqueue(Request("k1"));
        var seq2 = _outbox.Enqueue(Request("k2"));
        var dup = _outbox.Enqueue(Request("k1"));

        Assert.NotNull(seq1);
        Assert.NotNull(seq2);
        Assert.Null(dup);
        Assert.Equal(2, _store.PendingCount());
        Assert.Equal(["k1", "k2"], _store.GetPending().Select(r => r.IdempotencyKey).ToArray());
    }

    [Fact]
    public async Task FlushAsync_ConfirmsSuccessfulCommands()
    {
        _outbox.Enqueue(Request("k1"));
        _outbox.Enqueue(Request("k2"));

        var result = await _outbox.FlushAsync((cmd, _) => Task.FromResult(
            new CommandTransportResult(200, """{"command_id":"c1","accepted_at":"2026-11-24T01:00:00Z","result":0}""", true)));

        Assert.Equal(2, result.Confirmed);
        Assert.Equal(0, result.Remaining);
        var records = _store.GetByState(OutboxCommandState.Confirmed);
        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Equal("2026-11-24T01:00:00Z", r.AcceptedAt));
    }

    [Fact]
    public async Task FlushAsync_ConflictKinds_AreSurfacedAndFlushContinues()
    {
        _outbox.Enqueue(Request("k1"));
        _outbox.Enqueue(Request("k2"));
        _outbox.Enqueue(Request("k3"));

        var bodies = new Queue<string>(new[]
        {
            """{"kind":2,"message":"stale"}""",   // STALE_REVISION → conflict
            """{"kind":5,"message":"conflict"}""", // CONFLICT → conflict
            """{"kind":0,"message":"bad"}""",      // VALIDATION → failed
        });

        var result = await _outbox.FlushAsync((_, _) => Task.FromResult(
            new CommandTransportResult(409, bodies.Dequeue(), false)));

        Assert.Equal(0, result.Confirmed);
        Assert.Equal(2, result.Conflicts);
        Assert.Equal(1, result.Failed);
        Assert.Equal(0, result.Remaining);
        Assert.Equal(2, _store.CountByState(OutboxCommandState.Conflict));
        Assert.Equal(1, _store.CountByState(OutboxCommandState.Failed));
    }

    [Fact]
    public async Task FlushAsync_RetryableError_StaysPendingAndStops()
    {
        _outbox.Enqueue(Request("k1"));
        _outbox.Enqueue(Request("k2"));

        var calls = 0;
        var result = await _outbox.FlushAsync((_, _) =>
        {
            calls++;
            return Task.FromResult(new CommandTransportResult(503, """{"kind":7,"message":"retry"}""", false));
        });

        Assert.Equal(1, calls); // second command must not overtake the retryable failure
        Assert.Equal(2, result.Remaining);
        Assert.NotNull(result.StopReason);
        Assert.Equal(1, _store.GetPending().First().AttemptCount);
    }

    [Fact]
    public async Task FlushAsync_TransportException_StaysPendingAndStops()
    {
        _outbox.Enqueue(Request("k1"));
        _outbox.Enqueue(Request("k2"));

        var result = await _outbox.FlushAsync((_, _) => throw new HttpRequestException("offline"));

        Assert.Equal(2, result.Remaining);
        Assert.Equal(2, _store.PendingCount());
    }

    [Fact]
    public void Requeue_ReturnsConflictToPending()
    {
        _outbox.Enqueue(Request("k1"));
        var seq = _store.GetPending().Single().Seq;
        _store.MarkConflict(seq, "STALE_REVISION");

        _outbox.Requeue(seq);

        var record = _store.GetBySeq(seq)!;
        Assert.Equal(OutboxCommandState.Pending, record.State);
        Assert.Null(record.LastError);
    }
}
