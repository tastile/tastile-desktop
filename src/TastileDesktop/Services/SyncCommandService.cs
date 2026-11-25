using System.Text.Json;
using System.Text.Json.Serialization;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>Outcome of one user-initiated command submission.</summary>
public sealed record CommandSubmitResult(
    bool Accepted,
    string? Message,
    long? Seq,
    OutboxFlushResult? Flush)
{
    public static CommandSubmitResult Rejected(string message)
        => new(false, message, null, null);
}

/// <summary>
/// Routes user mutations through the persistent command outbox
/// (desktop NF04-D04/D05 on top of NF04-D06, v1/14 §1-1).
///
/// Every operation is persisted BEFORE any network attempt with its
/// idempotency key and client-observed time, then replayed in enqueue
/// order. Conflict-class rejections (STALE_REVISION /
/// IDEMPOTENCY_KEY_REUSED / CONFLICT / BLOCKED) stay visible via
/// <see cref="ConflictCommands"/> — never auto-resolved, never
/// silently dropped.
///
/// Execution start is a two-command chain (`StartTile` resolves the
/// placement, then `StartExecution` on it — v1/14 §14.5 core#277).
/// The second command's path needs the placement id from the first
/// response, so <see cref="FlushAsync"/> spawns the follow-up
/// `execution.start` when a `tile.start` confirms, reusing the original
/// `observed_at` so the observed segment interval (§1-1-1) reflects the
/// user's action time rather than the replay time.
/// </summary>
public sealed class SyncCommandService
{
    /// <summary>Fallback start window when the tile carries no duration hint (web parity: 25 min).</summary>
    public static readonly TimeSpan DefaultStartDuration = TimeSpan.FromMinutes(25);

    private readonly CoreApiClient _api;
    private readonly CommandOutbox _outbox;
    private readonly LocalReadModelStore _store;
    private readonly LocalSyncService? _sync;
    private readonly LocalExecutionProjector _projector;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<DateTimeOffset> _clock;

    public SyncCommandService(
        CoreApiClient api,
        CommandOutbox outbox,
        LocalReadModelStore store,
        LocalSyncService? sync = null,
        Func<DateTimeOffset>? clock = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sync = sync;
        _projector = new LocalExecutionProjector(store);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public CommandOutbox Outbox => _outbox;
    public LocalExecutionProjector Projector => _projector;

    /// <summary>Commands still awaiting a server outcome, in replay order.</summary>
    public IReadOnlyList<PendingCommandRecord> PendingCommands => _outbox.GetPending();

    /// <summary>Commands the server rejected with a conflict-class error.</summary>
    public IReadOnlyList<PendingCommandRecord> ConflictCommands => _outbox.GetConflicts();

    /// <summary>Permanently failed commands (non-conflict rejections).</summary>
    public IReadOnlyList<PendingCommandRecord> FailedCommands
        => _outbox.Store.GetByState(OutboxCommandState.Failed);

    /// <summary>Commands needing user attention: pending + conflict + failed.</summary>
    public IReadOnlyList<PendingCommandRecord> UnresolvedCommands
        => _outbox.GetAll()
            .Where(c => c.State is OutboxCommandState.Pending
                or OutboxCommandState.Conflict
                or OutboxCommandState.Failed)
            .ToList();

    /// <summary>Returns a conflict/failed command to pending for another replay pass.</summary>
    public void RequeueCommand(long seq) => _outbox.Requeue(seq);

    // ---------------------------------------------------------------
    // Execution control (NF04-D05)
    // ---------------------------------------------------------------

    /// <summary>
    /// Starts an execution for `tileId`: enqueues `tile.start`
    /// (StartTile — resolves or creates the placement) and lets the
    /// flush chain spawn `execution.start` on the returned placement.
    /// Both commands carry the same `observed_at` = the moment the user
    /// asked to start (v1/14 §1-1-1).
    /// </summary>
    public async Task<CommandSubmitResult> StartTileExecutionAsync(
        string tileId,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tileId))
        {
            return CommandSubmitResult.Rejected("tile id is required");
        }

        var tile = _projector.GetTile(tileId);
        if (tile?.PlanId is null)
        {
            // The tile may simply not be synced yet — pull once before
            // giving up so a fresh install can still start work.
            if (_sync is not null)
            {
                try
                {
                    await _sync.SyncOnceAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Offline: fall through to the not-found result; the
                    // command would be rejected by the server anyway.
                }
                tile = _projector.GetTile(tileId);
            }
        }

        if (tile?.PlanId is null)
        {
            return CommandSubmitResult.Rejected(
                $"tile {tileId} is not in the local read model (no synced plan_id)");
        }

        var observedAt = FormatInstant(_clock());
        var start = _clock();
        var span = new PlacementBaselineWire(
            Span: new SpanWire(
                Start: FormatInstant(start),
                End: FormatInstant(start + (duration ?? DefaultStartDuration))),
            Inside: null);

        var payload = new StartTileCommandPayload(
            TileId: tileId,
            PlanId: tile.PlanId,
            Source: PlacementSourceValue.Manual,
            SourceRef: StartTileCommandPayload.EmptySourceRef,
            Baseline: span);

        var seq = EnqueueCore(
            SyncCommandKind.TileStart,
            "POST",
            $"/v1/tiles/{tileId}/start",
            payload,
            observedAt);
        if (seq is null)
        {
            return CommandSubmitResult.Rejected("command already queued (idempotency dedup)");
        }

        var flush = await FlushAsync(cancellationToken).ConfigureAwait(false);
        return new CommandSubmitResult(true, null, seq, flush);
    }

    /// <summary>Pauses the live execution (`POST /v1/executions/{id}/pause`, unit payload).</summary>
    public Task<CommandSubmitResult> PauseExecutionAsync(CancellationToken cancellationToken = default)
        => SubmitLiveExecutionCommandAsync(SyncCommandKind.ExecutionPause, "pause", null, cancellationToken);

    /// <summary>Resumes the live execution (`POST /v1/executions/{id}/resume`, unit payload).</summary>
    public Task<CommandSubmitResult> ResumeExecutionAsync(CancellationToken cancellationToken = default)
        => SubmitLiveExecutionCommandAsync(SyncCommandKind.ExecutionResume, "resume", null, cancellationToken);

    /// <summary>
    /// Finishes the live execution. `kind` is the wire
    /// `ExecutionFinishKind` (0 = normal, 1 = void).
    /// </summary>
    public Task<CommandSubmitResult> FinishExecutionAsync(
        int kind = ExecutionFinishKindValue.Normal,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new FinishExecutionCommandPayload(Kind: kind, Note: note);
        return SubmitLiveExecutionCommandAsync(SyncCommandKind.ExecutionFinish, "finish", payload, cancellationToken);
    }

    private async Task<CommandSubmitResult> SubmitLiveExecutionCommandAsync(
        string kind,
        string action,
        object? payload,
        CancellationToken cancellationToken)
    {
        var execution = _projector.GetLiveExecution();
        if (execution is null)
        {
            return CommandSubmitResult.Rejected("no live execution in the local read model");
        }

        var observedAt = FormatInstant(_clock());
        var seq = EnqueueCore(
            kind,
            "POST",
            $"/v1/executions/{execution.ExecutionId}/{action}",
            payload,
            observedAt);
        if (seq is null)
        {
            return CommandSubmitResult.Rejected("command already queued (idempotency dedup)");
        }

        var flush = await FlushAsync(cancellationToken).ConfigureAwait(false);
        return new CommandSubmitResult(true, null, seq, flush);
    }

    // ---------------------------------------------------------------
    // Tile editing (NF04-D04 slice: create / update / archive / lifecycle)
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a placement tile (`POST /v1/tiles`, kind=PLACEMENT,
    /// plan_role=EXECUTABLE). `tags`/`notes` map to `meta_min`; a
    /// `projectId` must be a real subject UUID — display names are not
    /// accepted. Recurring creation (`kind=0`) is rejected server-side
    /// (410) and goes through `/v1/source-tiles` — out of scope here.
    /// </summary>
    public async Task<CommandSubmitResult> CreateTileAsync(
        string title,
        string? description = null,
        string? color = null,
        string? icon = null,
        string? externalId = null,
        IReadOnlyList<string>? tags = null,
        string? notes = null,
        string? projectId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return CommandSubmitResult.Rejected("title is required");
        }

        TileMetaMinWire? metaMin = null;
        if (tags is { Count: > 0 } || !string.IsNullOrWhiteSpace(notes) || !string.IsNullOrWhiteSpace(projectId))
        {
            metaMin = new TileMetaMinWire(
                Tags: tags?.ToList() ?? [],
                Notes: string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                ProjectId: string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim());
        }

        var payload = new CreateTileCommandPayload(
            Kind: TileKindValue.Placement,
            Title: title.Trim(),
            Description: description,
            Color: color,
            Icon: icon,
            ExternalId: externalId,
            PlanRole: PlanRoleValue.Executable,
            MetaMin: metaMin);

        return await SubmitSimpleAsync(
            SyncCommandKind.TileCreate, "POST", "/v1/tiles", payload, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Updates tile fields (`POST /v1/tiles/{id}/update`). Only non-null fields are sent.</summary>
    public async Task<CommandSubmitResult> UpdateTileAsync(
        string tileId,
        string? title = null,
        string? description = null,
        string? color = null,
        string? icon = null,
        string? externalId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tileId))
        {
            return CommandSubmitResult.Rejected("tile id is required");
        }
        if (title is null && description is null && color is null && icon is null && externalId is null)
        {
            return CommandSubmitResult.Rejected("no fields to update");
        }

        // Option<Option<T>> semantics: omitting a key leaves the field
        // unchanged, so serialize with nulls dropped.
        var dto = new UpdateTileCommandPayload(tileId, title, description, color, icon, externalId);
        var element = JsonSerializer.SerializeToElement(dto, UpdateTileCommandPayload.SerializeOptions);
        return await SubmitSimpleAsync(
            SyncCommandKind.TileUpdate, "POST", $"/v1/tiles/{tileId}/update", element, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Archives a tile (`DELETE /v1/tiles/{id}` with command envelope body).</summary>
    public Task<CommandSubmitResult> ArchiveTileAsync(string tileId, CancellationToken cancellationToken = default)
        => SubmitSimpleAsync(
            SyncCommandKind.TileArchive,
            "DELETE",
            $"/v1/tiles/{tileId}",
            new ArchiveTileCommandPayload(tileId),
            cancellationToken);

    /// <summary>
    /// Sets tile lifecycle (`POST /v1/tiles/{id}/complete` state=2 or
    /// `/defer` state=1). `deferredUntil` is an RFC 3339 instant only
    /// meaningful for defer; `completedAt` is stamped server-side.
    /// </summary>
    public Task<CommandSubmitResult> SetTileLifecycleAsync(
        string tileId,
        int state,
        string? deferredUntil = null,
        CancellationToken cancellationToken = default)
    {
        var action = state == TileLifecycleStateValue.Deferred ? "defer" : "complete";
        var payload = new SetTileLifecycleCommandPayload(
            TileId: tileId,
            State: state,
            DeferredUntil: deferredUntil,
            CompletedAt: null,
            BumpExtend: false);
        return SubmitSimpleAsync(
            SyncCommandKind.TileLifecycle, "POST", $"/v1/tiles/{tileId}/{action}", payload, cancellationToken);
    }

    /// <summary>Marks a tile completed (`state=2`).</summary>
    public Task<CommandSubmitResult> CompleteTileAsync(string tileId, CancellationToken cancellationToken = default)
        => SetTileLifecycleAsync(tileId, TileLifecycleStateValue.Completed, cancellationToken: cancellationToken);

    /// <summary>
    /// Defers a tile (`state=1`). `deferredUntil` is an optional RFC 3339
    /// instant; `deferMinutes` is a convenience that computes it from now.
    /// </summary>
    public Task<CommandSubmitResult> DeferTileAsync(
        string tileId,
        int? deferMinutes = null,
        CancellationToken cancellationToken = default)
    {
        string? deferredUntil = deferMinutes is > 0
            ? FormatInstant(_clock().AddMinutes(deferMinutes.Value))
            : null;
        return SetTileLifecycleAsync(tileId, TileLifecycleStateValue.Deferred, deferredUntil, cancellationToken);
    }

    /// <summary>
    /// Tile id of the live execution, when the local read model knows
    /// one — the target for "complete the current tile" actions.
    /// </summary>
    public string? ActiveTileId => _projector.GetLiveExecution()?.TileId;

    private async Task<CommandSubmitResult> SubmitSimpleAsync(
        string kind,
        string method,
        string path,
        object? payload,
        CancellationToken cancellationToken)
    {
        var observedAt = FormatInstant(_clock());
        var seq = EnqueueCore(kind, method, path, payload, observedAt);
        if (seq is null)
        {
            return CommandSubmitResult.Rejected("command already queued (idempotency dedup)");
        }

        var flush = await FlushAsync(cancellationToken).ConfigureAwait(false);
        return new CommandSubmitResult(true, null, seq, flush);
    }

    // ---------------------------------------------------------------
    // Outbox plumbing
    // ---------------------------------------------------------------

    /// <summary>
    /// Persists the command envelope and returns the outbox seq, or null
    /// when the idempotency key deduplicates against an existing row.
    /// The same key goes into the wire envelope and the outbox row so a
    /// local dedup hit can never diverge from server-side dedup.
    /// </summary>
    private long? EnqueueCore(
        string kind,
        string method,
        string path,
        object? payload,
        string observedAt,
        string? idempotencyKey = null,
        long? expectedRevision = null)
    {
        var key = idempotencyKey ?? Guid.NewGuid().ToString();
        var envelope = SyncCommandEnvelope.Create(payload, key, observedAt, expectedRevision);
        return _outbox.Enqueue(new OutboxEnqueueRequest(
            Kind: kind,
            HttpMethod: method,
            Path: path,
            EnvelopeJson: envelope.ToEnvelopeJson(),
            IdempotencyKey: key,
            ObservedAt: observedAt));
    }

    /// <summary>
    /// Replays pending commands in order, then spawns chained
    /// `execution.start` commands for confirmed `tile.start` rows and
    /// flushes again (bounded). A post-flush sync refreshes the local
    /// read model so the UI reflects committed state.
    /// </summary>
    public async Task<OutboxFlushResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new OutboxFlushResult(0, 0, 0, _outbox.Store.PendingCount(), "flush already in flight");
        }

        try
        {
            var confirmed = 0;
            var conflicts = 0;
            var failed = 0;
            string? stopReason = null;

            for (var round = 0; round < 8; round++)
            {
                var result = await _outbox
                    .FlushAsync(SendAsync, cancellationToken)
                    .ConfigureAwait(false);
                confirmed += result.Confirmed;
                conflicts += result.Conflicts;
                failed += result.Failed;
                stopReason = result.StopReason ?? stopReason;

                var spawned = SpawnExecutionStarts();
                // A retryable/transport failure leaves a pending head that
                // blocks in-order replay; chained commands wait for the
                // next flush pass rather than spinning here.
                if (spawned == 0 || result.StopReason is not null)
                {
                    break;
                }
            }

            if (confirmed > 0 && _sync is not null)
            {
                try
                {
                    await _sync.SyncOnceAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort: the next sync trigger catches up.
                }
            }

            return new OutboxFlushResult(
                confirmed, conflicts, failed, _outbox.Store.PendingCount(), stopReason);
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task<CommandTransportResult> SendAsync(PendingCommandRecord command, CancellationToken cancellationToken)
        => _api.SendCommandAsync(command.HttpMethod, command.Path, command.EnvelopeJson, cancellationToken);

    /// <summary>
    /// For every confirmed `tile.start` that has not spawned its
    /// `execution.start` yet, enqueue the follow-up using the placement
    /// id from the stored command response. The spawn marker lives in
    /// the read-model meta table so a crash between confirm and spawn
    /// cannot double-enqueue, and a restart resumes unfinished chains.
    /// </summary>
    private int SpawnExecutionStarts()
    {
        var spawned = 0;
        foreach (var record in _outbox.Store.GetByState(OutboxCommandState.Confirmed))
        {
            if (!string.Equals(record.Kind, SyncCommandKind.TileStart, StringComparison.Ordinal))
            {
                continue;
            }

            var metaKey = $"exec_chain:{record.Seq}";
            if (_store.GetMeta(metaKey) is not null)
            {
                continue;
            }

            var placementId = TryExtractAggregateId(record.ResponseJson);
            if (placementId is null)
            {
                _store.SetMeta(metaKey, "no-aggregate");
                continue;
            }

            var payload = new StartExecutionCommandPayload(PlacementId: placementId);
            var seq = EnqueueCore(
                SyncCommandKind.ExecutionStart,
                "POST",
                $"/v1/placements/{placementId}/executions",
                payload,
                record.ObservedAt);
            _store.SetMeta(metaKey, seq is null ? "deduped" : "spawned");
            spawned++;
        }
        return spawned;
    }

    /// <summary>Extracts `aggregate.id` (UUID string) from a stored CommandResponse body.</summary>
    internal static string? TryExtractAggregateId(string? responseJson)
    {
        if (string.IsNullOrEmpty(responseJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("aggregate", out var aggregate)
                || aggregate.ValueKind != JsonValueKind.Object
                || !aggregate.TryGetProperty("id", out var id)
                || id.ValueKind != JsonValueKind.String)
            {
                return null;
            }
            var value = id.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string FormatInstant(DateTimeOffset instant)
        => instant.ToUniversalTime().ToString("O");
}
