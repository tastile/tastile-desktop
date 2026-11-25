using System.Globalization;
using System.Text.Json;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>One OS-notification candidate produced from a sync delta.</summary>
public sealed record SyncToastMessage(
    string Title,
    string Body,
    string? ActionId = null,
    string? ActionLabel = null);

/// <summary>
/// Maps committed sync deltas (<see cref="LocalSyncService.DeltaApplied"/>)
/// onto OS toast notifications (desktop NF04-D07): schedule changes,
/// cancellations, and cross-device execution/session updates.
///
/// Correctness rules:
/// - Driven ONLY by delta pages. Snapshots never raise the event, so a
///   first sync, offline catch-up snapshot, or cursor-expired resnapshot
///   cannot replay history as toasts.
/// - Restart / stale replay: a change is only eligible when its
///   <c>occurred_at</c> is within <see cref="MaxNotificationAge"/> of now.
///   Deltas describing changes from before the app was (re)started are
///   typically older than that window while the app was closed, so they
///   are suppressed instead of replayed as stale toasts.
/// - Duplicates: every toast carries a dedupe key
///   (entity kind + id + event kind + resulting signature) persisted in
///   the read-model meta table (bounded FIFO). Tail re-delivery, SSE/poller
///   overlap, reconnect replay, and restart cursor replay of the same
///   underlying change resolve to the same key and produce at most one
///   toast per app lifetime — and across restarts while the key survives.
/// - Change/cancel semantics come from diffing the pre-apply state
///   (<see cref="AppliedSyncChange.PreviousStateJson"/>) against the new
///   state, so revision-only bumps and idempotent re-delivery never toast.
/// </summary>
public sealed class SyncNotificationService : IDisposable
{
    /// <summary>Meta key holding the persisted dedupe-key ring.</summary>
    public const string NotifiedKeysMetaKey = "sync_notified_keys";

    /// <summary>Upper bound on retained dedupe keys (oldest evicted first).</summary>
    public const int MaxNotifiedKeys = 512;

    /// <summary>
    /// Default freshness window: deltas whose <c>occurred_at</c> is older
    /// than this are treated as stale and never toasted. Injectable for
    /// tests and future settings.
    /// </summary>
    public static readonly TimeSpan DefaultMaxNotificationAge = TimeSpan.FromHours(1);

    /// <summary>Tolerated future skew on <c>occurred_at</c> (server clock ahead of client).</summary>
    private static readonly TimeSpan FutureSkewTolerance = TimeSpan.FromMinutes(5);

    private readonly LocalSyncService _sync;
    private readonly LocalReadModelStore _store;
    private readonly TimeSpan _maxNotificationAge;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly object _gate = new();
    private readonly HashSet<string> _notifiedKeys = new(StringComparer.Ordinal);
    private readonly Queue<string> _notifiedOrder = new();
    private bool _started;
    private bool _keysLoaded;

    public SyncNotificationService(
        LocalSyncService sync,
        TimeSpan? maxNotificationAge = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        _store = sync.Store;
        _maxNotificationAge = maxNotificationAge ?? DefaultMaxNotificationAge;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Raised (on the sync worker thread) for each toast to display.</summary>
    public event EventHandler<SyncToastMessage>? ToastRequested;

    /// <summary>Subscribes to delta events. Idempotent.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }
            _started = true;
        }
        _sync.DeltaApplied += OnDeltaApplied;
    }

    public void Dispose()
    {
        _sync.DeltaApplied -= OnDeltaApplied;
    }

    private void OnDeltaApplied(object? sender, SyncDeltaAppliedEventArgs e)
        => ProcessDeltaPage(e);

    /// <summary>
    /// Processes one committed delta page. Public so tests and manual
    /// replay tooling can drive it without HTTP; the event path calls
    /// this same method.
    /// </summary>
    public void ProcessDeltaPage(SyncDeltaAppliedEventArgs e)
    {
        foreach (var applied in e.Changes)
        {
            try
            {
                ProcessChange(applied);
            }
            catch (Exception ex)
            {
                // A malformed entity state must not break the sync loop or
                // suppress later changes in the same page.
                System.Diagnostics.Debug.WriteLine(
                    $"[SyncNotificationService] change {applied.Change.EntityKind}:{applied.Change.EntityId} failed: {ex.Message}");
            }
        }
    }

    private void ProcessChange(AppliedSyncChange applied)
    {
        var change = applied.Change;
        if (!IsFresh(change.OccurredAt))
        {
            return;
        }

        // TILE / SOURCE_TILE deltas are supporting state only — they never
        // produce their own toast (avoids double-notifying a schedule
        // change that also bumps the tile revision).
        switch (change.EntityKind)
        {
            case SyncEntityKind.Placement:
                ProcessPlacement(applied);
                break;
            case SyncEntityKind.Execution:
                ProcessExecution(applied);
                break;
            case SyncEntityKind.Session:
                ProcessSession(applied);
                break;
        }
    }

    // ---------------------------------------------------------------
    // PLACEMENT (v1/14 §6-3 kind=2, PlacementRead view)
    // ---------------------------------------------------------------

    private void ProcessPlacement(AppliedSyncChange applied)
    {
        var change = applied.Change;
        var isTombstone = change.ChangeKind == SyncChangeKind.Tombstone
            || change.State is null
            || change.State.Value.ValueKind == JsonValueKind.Null;

        var next = isTombstone ? null : ParsePlacement(change.State!.Value);
        var previous = ParsePlacementJson(applied.PreviousStateJson);

        SyncToastMessage? toast = null;
        string? dedupeKey = null;

        // Transitions require a previously-open local state; an entity that
        // first appears already closed/detached is history, not a change.
        var becameClosed = next is { Close: true }
            && previous is { Close: false };
        var becameDetached = next is { Detach: true }
            && previous is { Detach: false };
        if (isTombstone || becameClosed || becameDetached)
        {
            // Entity removed from the read model, closed, or detached:
            // the schedule entry is cancelled either way.
            var source = next ?? previous;
            if (source is not null)
            {
                toast = new SyncToastMessage(
                    Title: TileTitleOrFallback(source.TileId, "Schedule updated"),
                    Body: "Placement was cancelled.",
                    ActionId: "open",
                    ActionLabel: "Open");
                dedupeKey = $"p|{change.EntityId}|cancelled";
            }
        }
        else if (next is not null)
        {
            if (next.Close || next.Detach)
            {
                // Already closed/detached before this delta: history edit,
                // not a user-facing schedule change.
                return;
            }

            if (previous is null)
            {
                toast = new SyncToastMessage(
                    Title: TileTitleOrFallback(next.TileId, "New schedule"),
                    Body: FormatSpan(next.SpanStart, next.SpanEnd) is { } span
                        ? $"Scheduled: {span}"
                        : "New placement scheduled.",
                    ActionId: "open",
                    ActionLabel: "Open");
                dedupeKey = $"p|{change.EntityId}|scheduled|{next.SpanStart}|{next.SpanEnd}";
            }
            else if (!previous.Close && !previous.Detach
                && (!string.Equals(previous.SpanStart, next.SpanStart, StringComparison.Ordinal)
                    || !string.Equals(previous.SpanEnd, next.SpanEnd, StringComparison.Ordinal)))
            {
                toast = new SyncToastMessage(
                    Title: TileTitleOrFallback(next.TileId, "Schedule changed"),
                    Body: FormatSpan(next.SpanStart, next.SpanEnd) is { } span
                        ? $"Moved to {span}"
                        : "Placement time changed.",
                    ActionId: "open",
                    ActionLabel: "Open");
                dedupeKey = $"p|{change.EntityId}|moved|{next.SpanStart}|{next.SpanEnd}";
            }
        }

        if (toast is null || dedupeKey is null)
        {
            return;
        }

        EmitIfNew(dedupeKey, toast);
    }

    // ---------------------------------------------------------------
    // EXECUTION (kind=3, ExecutionView state)
    // ---------------------------------------------------------------

    private void ProcessExecution(AppliedSyncChange applied)
    {
        var change = applied.Change;
        var isTombstone = change.ChangeKind == SyncChangeKind.Tombstone
            || change.State is null
            || change.State.Value.ValueKind == JsonValueKind.Null;
        if (isTombstone)
        {
            return;
        }

        var next = ParseExecution(change.State!.Value);
        var previous = ParseExecutionJson(applied.PreviousStateJson);
        if (next is null)
        {
            return;
        }

        // Only transitions INTO a finished state notify. Start/pause/resume
        // echoes of the user's own commands would be noise, and the finish
        // states are terminal so each execution toasts at most once.
        SyncToastMessage? toast = null;
        string? dedupeKey = null;
        if (next.State == ExecutionStateValue.FinishedNormal
            && previous?.State != ExecutionStateValue.FinishedNormal)
        {
            toast = new SyncToastMessage(
                Title: TileTitleOrFallback(next.TileId, "Execution finished"),
                Body: "Marked as done.",
                ActionId: "open",
                ActionLabel: "Open");
            dedupeKey = $"e|{change.EntityId}|finished-normal";
        }
        else if (next.State == ExecutionStateValue.FinishedVoid
            && previous?.State != ExecutionStateValue.FinishedVoid)
        {
            toast = new SyncToastMessage(
                Title: TileTitleOrFallback(next.TileId, "Execution cancelled"),
                Body: "Execution was voided.",
                ActionId: "open",
                ActionLabel: "Open");
            dedupeKey = $"e|{change.EntityId}|finished-void";
        }

        if (toast is null || dedupeKey is null)
        {
            return;
        }

        EmitIfNew(dedupeKey, toast);
    }

    // ---------------------------------------------------------------
    // SESSION (kind=4, DecisionSessionRow view; state 0 = OPEN)
    // ---------------------------------------------------------------

    private void ProcessSession(AppliedSyncChange applied)
    {
        var change = applied.Change;
        var isTombstone = change.ChangeKind == SyncChangeKind.Tombstone
            || change.State is null
            || change.State.Value.ValueKind == JsonValueKind.Null;

        var nextState = isTombstone ? (int?)null : ReadInt(change.State!.Value, "state");
        var previousState = ReadIntJson(applied.PreviousStateJson, "state");

        SyncToastMessage? toast = null;
        string? dedupeKey = null;

        if (nextState == 0 && previousState != 0)
        {
            // A decision session opened (or re-opened): the schedule may
            // need user input. Sessions carry no title — keep it generic.
            toast = new SyncToastMessage(
                Title: "Tastile",
                Body: "A schedule decision is waiting for input.",
                ActionId: "open",
                ActionLabel: "Open");
            dedupeKey = $"s|{change.EntityId}|open";
        }
        else if (nextState is > 0 && previousState == 0)
        {
            toast = new SyncToastMessage(
                Title: "Tastile",
                Body: "A schedule decision was resolved.",
                ActionId: "open",
                ActionLabel: "Open");
            dedupeKey = $"s|{change.EntityId}|closed-{nextState}";
        }

        if (toast is null || dedupeKey is null)
        {
            return;
        }

        EmitIfNew(dedupeKey, toast);
    }

    // ---------------------------------------------------------------
    // Freshness / dedupe / emission
    // ---------------------------------------------------------------

    private bool IsFresh(string? occurredAt)
    {
        if (string.IsNullOrWhiteSpace(occurredAt))
        {
            // Contract guarantees an Instant; treat unparseable as fresh
            // rather than silently dropping a real change.
            return true;
        }

        if (!DateTimeOffset.TryParse(
                occurredAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var occurred))
        {
            return true;
        }

        var age = _utcNow() - occurred;
        return age <= _maxNotificationAge && age >= -FutureSkewTolerance;
    }

    private void EmitIfNew(string dedupeKey, SyncToastMessage toast)
    {
        lock (_gate)
        {
            EnsureKeysLoaded();
            if (!_notifiedKeys.Add(dedupeKey))
            {
                return;
            }

            _notifiedOrder.Enqueue(dedupeKey);
            while (_notifiedOrder.Count > MaxNotifiedKeys)
            {
                _notifiedKeys.Remove(_notifiedOrder.Dequeue());
            }
            PersistKeys();
        }

        try
        {
            ToastRequested?.Invoke(this, toast);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SyncNotificationService] toast dispatch failed: {ex.Message}");
        }
    }

    private void EnsureKeysLoaded()
    {
        if (_keysLoaded)
        {
            return;
        }
        _keysLoaded = true;

        var raw = _store.GetMeta(NotifiedKeysMetaKey);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String
                    && element.GetString() is { } key)
                {
                    if (_notifiedKeys.Add(key))
                    {
                        _notifiedOrder.Enqueue(key);
                    }
                }
            }
            while (_notifiedOrder.Count > MaxNotifiedKeys)
            {
                _notifiedKeys.Remove(_notifiedOrder.Dequeue());
            }
        }
        catch (JsonException)
        {
            // Corrupt meta is not fatal: worst case is one duplicate toast.
        }
    }

    private void PersistKeys()
    {
        try
        {
            var json = JsonSerializer.Serialize(_notifiedOrder.ToArray());
            _store.SetMeta(NotifiedKeysMetaKey, json);
        }
        catch (Exception ex)
        {
            // Persistence failure degrades dedupe to in-memory only.
            System.Diagnostics.Debug.WriteLine(
                $"[SyncNotificationService] dedupe persist failed: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------
    // Title lookup + state parsing (defensive; read model is a cache)
    // ---------------------------------------------------------------

    private string TileTitleOrFallback(string? tileId, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(tileId))
        {
            var state = _store.GetEntityState(SyncEntityKind.Tile, tileId);
            if (state is { } element)
            {
                // TileDetailView: { base: { title, ... }, ... }
                var baser = TryGetObject(element, "base");
                var source = baser.ValueKind == JsonValueKind.Object ? baser : element;
                if (ReadString(source, "title") is { Length: > 0 } title)
                {
                    return title;
                }
            }
        }
        return fallback;
    }

    private sealed record PlacementSnapshot(
        string? TileId,
        string? SpanStart,
        string? SpanEnd,
        bool Close,
        bool Detach);

    private sealed record ExecutionSnapshot(
        string? TileId,
        int State);

    private static PlacementSnapshot? ParsePlacementJson(string? json)
        => json is null ? null : ParsePlacementElement(Parse(json));

    private static PlacementSnapshot? ParsePlacement(JsonElement element)
        => element.ValueKind == JsonValueKind.Undefined ? null : ParsePlacementElement(element);

    private static PlacementSnapshot ParsePlacementElement(JsonElement element)
        => new(
            TileId: ReadString(element, "tile_id"),
            SpanStart: ReadString(element, "span_start"),
            SpanEnd: ReadString(element, "span_end"),
            Close: ReadBool(element, "close") ?? ReadBool(element, "closed") ?? false,
            Detach: ReadBool(element, "detach") ?? false);

    private static ExecutionSnapshot? ParseExecutionJson(string? json)
        => json is null ? null : ParseExecutionElement(Parse(json));

    private static ExecutionSnapshot? ParseExecution(JsonElement element)
        => element.ValueKind == JsonValueKind.Undefined ? null : ParseExecutionElement(element);

    private static ExecutionSnapshot ParseExecutionElement(JsonElement element)
        => new(
            TileId: ReadString(element, "tile_id"),
            State: ReadInt(element, "state") ?? -1);

    private static string? FormatSpan(string? spanStart, string? spanEnd)
    {
        if (!DateTimeOffset.TryParse(spanStart, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start))
        {
            return null;
        }
        var local = start.ToLocalTime();
        if (DateTimeOffset.TryParse(spanEnd, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var end))
        {
            return $"{local:MMM d HH:mm}–{end.ToLocalTime():HH:mm}";
        }
        return $"{local:MMM d HH:mm}";
    }

    private static JsonElement Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static JsonElement TryGetObject(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Object
                ? value
                : default;

    private static string? ReadString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    private static int? ReadInt(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var value))
        {
            return null;
        }
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var i) => i,
            JsonValueKind.String when int.TryParse(value.GetString(), out var i) => i,
            _ => null,
        };
    }

    private static int? ReadIntJson(string? json, string name)
        => json is null ? null : ReadInt(Parse(json), name);

    private static bool? ReadBool(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean()
                : null;
}
