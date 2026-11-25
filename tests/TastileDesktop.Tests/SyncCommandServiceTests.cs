using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class SyncCommandServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _outboxDbPath;
    private readonly LocalReadModelStore _store;
    private readonly CommandOutboxStore _outboxStore;
    private readonly CommandOutbox _outbox;

    private static readonly DateTimeOffset FixedNow = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    public SyncCommandServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"tastile-cmd-svc-{Guid.NewGuid():N}.db");
        _outboxDbPath = Path.Combine(Path.GetTempPath(), $"tastile-cmd-outbox-{Guid.NewGuid():N}.db");
        _store = new LocalReadModelStore(_dbPath);
        _outboxStore = new CommandOutboxStore(_outboxDbPath);
        _outbox = new CommandOutbox(_outboxStore);
    }

    public void Dispose()
    {
        _outboxStore.Dispose();
        _store.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _dbPath, _outboxDbPath })
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                var full = path + suffix;
                if (File.Exists(full))
                {
                    File.Delete(full);
                }
            }
        }
    }

    private static CoreApiClient ClientFor(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3140") });

    private static JsonElement State(string json)
        => JsonDocument.Parse(json).RootElement.Clone();

    private void SeedEntities(params SyncEntity[] entities)
    {
        _store.BeginSnapshot();
        _store.StageSnapshotEntities(entities);
        _store.CommitSnapshot("cursor-1");
    }

    private static SyncEntity Entity(int kind, string id, string stateJson)
        => new(kind, id, State(stateJson));

    private static string CommandOkResponse(string aggregateId)
        => $$"""{"command_id":"019ef8d5-354a-7bd2-b22a-b4bd372ea0d1","received_at":"2026-11-24T12:00:01Z","accepted_at":"2026-11-24T12:00:02Z","aggregate":{"kind":2,"id":"{{aggregateId}}"},"revision":3,"result":0,"pending":[]}""";

    private sealed record SentRequest(string Method, string Path, string Body);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public readonly List<SentRequest> Requests = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new SentRequest(request.Method.Method, request.RequestUri!.AbsolutePath, body));
            return responder(request);
        }
    }

    private SyncCommandService ServiceFor(CoreApiClient api)
        => new(api, _outbox, _store, sync: null, clock: () => FixedNow);

    [Fact]
    public async Task PauseExecution_EnqueuesWireEnvelope_AndConfirms()
    {
        SeedEntities(Entity(SyncEntityKind.Execution, "exec-1",
            """{"id":"exec-1","tile_id":"tile-1","placement_id":"place-1","state":0,"revision":3,"updated_at":"2026-11-24T11:00:00Z"}"""));

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(CommandOkResponse("exec-1")),
        });
        var service = ServiceFor(ClientFor(handler));

        var result = await service.PauseExecutionAsync();

        Assert.True(result.Accepted);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("POST", sent.Method);
        Assert.Equal("/v1/executions/exec-1/pause", sent.Path);

        using var doc = JsonDocument.Parse(sent.Body);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("expected_revision", out _));
        Assert.True(root.TryGetProperty("idempotency_key", out var key));
        Assert.True(root.TryGetProperty("observed_at", out var observed));
        Assert.True(root.TryGetProperty("occurred_at", out _));
        Assert.True(root.TryGetProperty("payload", out var payload));
        Assert.Equal(JsonValueKind.Null, payload.ValueKind);
        Assert.Equal("2026-11-24T12:00:00.0000000+00:00", observed.GetString());

        // The same idempotency key is stored in the outbox row.
        var record = Assert.Single(_outboxStore.GetByState(OutboxCommandState.Confirmed));
        Assert.Equal(key.GetString(), record.IdempotencyKey);
        Assert.Equal(SyncCommandKind.ExecutionPause, record.Kind);
        Assert.Equal("2026-11-24T12:00:00.0000000+00:00", record.ObservedAt);
    }

    [Fact]
    public async Task FinishExecution_SendsKindAndNote_InPayload()
    {
        SeedEntities(Entity(SyncEntityKind.Execution, "exec-9",
            """{"id":"exec-9","tile_id":"tile-1","state":1,"revision":5}"""));

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(CommandOkResponse("exec-9")),
        });
        var service = ServiceFor(ClientFor(handler));

        var result = await service.FinishExecutionAsync(ExecutionFinishKindValue.Void, "gave up");

        Assert.True(result.Accepted);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("/v1/executions/exec-9/finish", sent.Path);
        using var doc = JsonDocument.Parse(sent.Body);
        var payload = doc.RootElement.GetProperty("payload");
        Assert.Equal(1, payload.GetProperty("kind").GetInt32());
        Assert.Equal("gave up", payload.GetProperty("note").GetString());
    }

    [Fact]
    public async Task ResumeExecution_NoLiveExecution_IsRejectedWithoutSend()
    {
        SeedEntities(Entity(SyncEntityKind.Execution, "exec-1",
            """{"id":"exec-1","state":2,"finished_at":"2026-11-24T10:00:00Z"}"""));

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = ServiceFor(ClientFor(handler));

        var result = await service.ResumeExecutionAsync();

        Assert.False(result.Accepted);
        Assert.Empty(handler.Requests);
        Assert.Equal(0, _outboxStore.PendingCount());
    }

    [Fact]
    public async Task StartTileExecution_ChainsExecutionStart_WithSameObservedAt()
    {
        SeedEntities(
            Entity(SyncEntityKind.Tile, "tile-1",
                """{"base":{"id":"tile-1","plan_id":"plan-1","title":"Task","kind":1,"revision":2},"labels":[],"resolved_at":"2026-11-24T11:00:00Z"}"""),
            Entity(SyncEntityKind.Placement, "place-x",
                """{"id":"place-x","tile_id":"tile-1","plan_id":"plan-1","close":false,"span_start":"2026-11-24T13:00:00Z","span_end":"2026-11-24T14:00:00Z"}"""));

        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/tiles/tile-1/start")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(CommandOkResponse("place-new")),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CommandOkResponse("exec-new")),
            };
        });
        var service = ServiceFor(ClientFor(handler));

        var result = await service.StartTileExecutionAsync("tile-1");

        Assert.True(result.Accepted);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("/v1/tiles/tile-1/start", handler.Requests[0].Path);
        Assert.Equal("/v1/placements/place-new/executions", handler.Requests[1].Path);

        // Both commands carry the user's action time as observed_at.
        using var first = JsonDocument.Parse(handler.Requests[0].Body);
        using var second = JsonDocument.Parse(handler.Requests[1].Body);
        var observed = first.RootElement.GetProperty("observed_at").GetString();
        Assert.Equal(observed, second.RootElement.GetProperty("observed_at").GetString());
        Assert.Equal("2026-11-24T12:00:00.0000000+00:00", observed);

        // StartTile payload shape: manual source + baseline span.
        var payload = first.RootElement.GetProperty("payload");
        Assert.Equal("tile-1", payload.GetProperty("tile_id").GetString());
        Assert.Equal("plan-1", payload.GetProperty("plan_id").GetString());
        Assert.Equal(0, payload.GetProperty("source").GetInt32());
        Assert.Equal("place-new", second.RootElement.GetProperty("payload").GetProperty("placement_id").GetString());

        Assert.Equal(2, _outboxStore.CountByState(OutboxCommandState.Confirmed));
    }

    [Fact]
    public async Task StartTileExecution_Conflict_IsSurfaced_NotResolved()
    {
        SeedEntities(Entity(SyncEntityKind.Tile, "tile-1",
            """{"base":{"id":"tile-1","plan_id":"plan-1","title":"T","kind":1}}"""));

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("""{"kind":5,"message":"scheduled task requires exactly one unfinished live segment","current_revision":null,"violations":[]}"""),
        });
        var service = ServiceFor(ClientFor(handler));

        var result = await service.StartTileExecutionAsync("tile-1");

        Assert.True(result.Accepted); // queued locally
        Assert.Equal(1, result.Flush!.Conflicts);
        var conflict = Assert.Single(service.ConflictCommands);
        Assert.Equal(SyncCommandKind.TileStart, conflict.Kind);
        Assert.Contains("unfinished live segment", conflict.LastError);
        // No chained execution.start was spawned for a conflicted start.
        Assert.DoesNotContain(service.UnresolvedCommands, c => c.Kind == SyncCommandKind.ExecutionStart);
    }

    [Fact]
    public async Task Flush_ConflictThenRequeue_ResolvesOnRetry()
    {
        SeedEntities(Entity(SyncEntityKind.Execution, "exec-1",
            """{"id":"exec-1","state":0}"""));

        var calls = 0;
        var handler = new StubHandler(_ =>
        {
            calls++;
            return calls == 1
                ? new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent("""{"kind":2,"message":"stale revision","current_revision":9,"violations":[]}"""),
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(CommandOkResponse("exec-1")),
                };
        });
        var service = ServiceFor(ClientFor(handler));

        await service.PauseExecutionAsync();
        Assert.Single(service.ConflictCommands);

        var seq = service.ConflictCommands[0].Seq;
        service.RequeueCommand(seq);
        var flush = await service.FlushAsync();

        Assert.Equal(1, flush.Confirmed);
        Assert.Empty(service.ConflictCommands);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Envelope_UsesSnakeCaseWireFields()
    {
        var envelope = SyncCommandEnvelope.Create(
            new StartExecutionCommandPayload("place-1"),
            "key-1",
            "2026-11-24T12:00:00Z");
        var json = envelope.ToEnvelopeJson();

        Assert.Contains("\"expected_revision\"", json);
        Assert.Contains("\"idempotency_key\"", json);
        Assert.Contains("\"observed_at\"", json);
        Assert.Contains("\"occurred_at\"", json);
        Assert.Contains("\"payload\"", json);
        Assert.DoesNotContain("expectedRevision", json);
        Assert.DoesNotContain("idempotencyKey", json);
        Assert.DoesNotContain("observedAt", json);
    }

    [Fact]
    public void TryExtractAggregateId_ReadsCommandResponse()
    {
        Assert.Equal("abc", SyncCommandService.TryExtractAggregateId(
            """{"aggregate":{"kind":2,"id":"abc"},"result":0}"""));
        Assert.Null(SyncCommandService.TryExtractAggregateId("""{"aggregate":null}"""));
        Assert.Null(SyncCommandService.TryExtractAggregateId("not json"));
        Assert.Null(SyncCommandService.TryExtractAggregateId(null));
    }
}
