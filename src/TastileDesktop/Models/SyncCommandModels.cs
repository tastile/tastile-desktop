using System.Text.Json;
using System.Text.Json.Serialization;

namespace TastileDesktop.Models;

// Sync command wire models (tastile-core v1/14 §1, §1-1, §2.5;
// desktop NF04-D04/D05). Every mutating call is a CommandRequest<T>
// envelope: expected_revision / idempotency_key / observed_at /
// occurred_at / payload. The wire field names are snake_case — they
// mirror the Rust `domain::command::CommandRequest<T>` serde shape
// (verified against crates-v1 api handlers and the tastile-web
// `toWireCommandRequest` translation layer).
//
// `observed_at` is the client-declared operation time (v1/14 §1-1):
// for Execution commands it bounds the observed segment interval
// (§1-1-1); for all commands the server stores it as
// `client_observed_at` and never uses it for ordering or conflict
// resolution. `occurred_at` is the deprecated alias — kept as null so
// legacy-tolerant servers keep accepting the envelope.

/// <summary>Logical command kinds recorded in the outbox `kind` column.</summary>
public static class SyncCommandKind
{
    public const string TileCreate = "tile.create";
    public const string TileUpdate = "tile.update";
    public const string TileArchive = "tile.archive";
    public const string TileLifecycle = "tile.lifecycle";
    public const string TileStart = "tile.start";
    public const string ExecutionStart = "execution.start";
    public const string ExecutionPause = "execution.pause";
    public const string ExecutionResume = "execution.resume";
    public const string ExecutionFinish = "execution.finish";
}

/// <summary>
/// Wire form of `domain::command::CommandRequest<T>` (v1/14 §1).
/// `Payload` stays a <see cref="JsonElement"/> so the persisted outbox
/// row and the wire body can never diverge (same rule as
/// `CoreApiClient.SendCommandAsync`).
/// </summary>
public sealed record SyncCommandEnvelope(
    [property: JsonPropertyName("expected_revision")] long? ExpectedRevision,
    [property: JsonPropertyName("idempotency_key")] string IdempotencyKey,
    [property: JsonPropertyName("observed_at")] string? ObservedAt,
    [property: JsonPropertyName("occurred_at")] string? OccurredAt,
    [property: JsonPropertyName("payload")] JsonElement? Payload)
{
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Serializes the envelope exactly once; the resulting text is what
    /// gets persisted in the outbox and later replayed verbatim.
    /// </summary>
    public string ToEnvelopeJson()
        => JsonSerializer.Serialize(this, WireOptions);

    /// <summary>
    /// Builds the wire envelope for one command. `payload` may be any
    /// DTO below or <see langword="null"/> for unit-payload commands
    /// (pause/resume send `payload: null` per v1 `CommandRequest<()>`).
    /// </summary>
    public static SyncCommandEnvelope Create(
        object? payload,
        string idempotencyKey,
        string? observedAt,
        long? expectedRevision = null)
    {
        JsonElement? element = payload switch
        {
            null => null,
            JsonElement json => json,
            _ => JsonSerializer.SerializeToElement(payload),
        };
        return new SyncCommandEnvelope(
            ExpectedRevision: expectedRevision,
            IdempotencyKey: idempotencyKey,
            ObservedAt: observedAt,
            OccurredAt: null,
            Payload: element);
    }
}

// ---------------------------------------------------------------
// Command payload DTOs. Field names mirror the Rust payload structs
// in crates-v1/domain/src/command.rs (snake_case on the wire).
// ---------------------------------------------------------------

/// <summary>`TileKind` numeric registry (v1/14 §2.7, domain constants.rs).</summary>
public static class TileKindValue
{
    public const int Recurring = 0;
    public const int Placement = 1;
    public const int Execution = 2;
    public const int Source = 3;
}

/// <summary>`PlanRole` numeric registry (v1/14 §2.7).</summary>
public static class PlanRoleValue
{
    public const int Executable = 0;
    public const int Label = 1;
}

/// <summary>`PlacementSource` numeric registry (domain constants.rs).</summary>
public static class PlacementSourceValue
{
    public const int Manual = 0;
    public const int Recurring = 1;
    public const int Flow = 2;
    public const int Import = 3;
    public const int Source = 4;
}

/// <summary>`ExecutionState` numeric registry (v1/02 §Execution).</summary>
public static class ExecutionStateValue
{
    public const int Active = 0;
    public const int Paused = 1;
    public const int FinishedNormal = 2;
    public const int FinishedVoid = 3;
}

/// <summary>
/// `ExecutionFinishKind` wire values. The finish kind shares the
/// ExecutionState numeric space per domain constants.rs: 0 maps to
/// FINISHED_NORMAL, 1 maps to FINISHED_VOID (verified against
/// tastile-web `finishExecutionCommand` wire mapping).
/// </summary>
public static class ExecutionFinishKindValue
{
    public const int Normal = 0;
    public const int Void = 1;
}

/// <summary>`SetTileLifecyclePayload.state` numeric registry (v1 command.rs).</summary>
public static class TileLifecycleStateValue
{
    public const int Active = 0;
    public const int Deferred = 1;
    public const int Completed = 2;
}

/// <summary>`TileMetaMin` wire shape (`{tags, notes, project_id}`, v1/14 §2.5).</summary>
public sealed record TileMetaMinWire(
    [property: JsonPropertyName("tags")] List<string> Tags,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("project_id")] string? ProjectId);

/// <summary>
/// `CreateTilePayload` (POST /v1/tiles). `kind = 0` (RECURRING) is
/// rejected server-side with 410 — recurring creation goes through
/// `/v1/source-tiles` and is out of scope for the desktop slice.
/// `frame_rule` must be absent for every accepted kind, so it is not
/// modeled here.
/// </summary>
public sealed record CreateTileCommandPayload(
    [property: JsonPropertyName("kind")] int Kind,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("color")] string? Color,
    [property: JsonPropertyName("icon")] string? Icon,
    [property: JsonPropertyName("external_id")] string? ExternalId,
    [property: JsonPropertyName("plan_role")] int PlanRole,
    [property: JsonPropertyName("meta_min")] TileMetaMinWire? MetaMin);

/// <summary>
/// `UpdateTilePayload` (POST /v1/tiles/{id}/update). The Rust fields are
/// `Option&lt;Option&lt;T&gt;&gt;` for clear-vs-unchanged semantics; this
/// DTO only carries "set" values — a null property is omitted from the
/// payload so the server leaves the field unchanged (clearing fields is
/// deferred; see issue notes).
/// </summary>
public sealed record UpdateTileCommandPayload(
    [property: JsonPropertyName("tile_id")] string TileId,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("color")] string? Color,
    [property: JsonPropertyName("icon")] string? Icon,
    [property: JsonPropertyName("external_id")] string? ExternalId)
{
    public static readonly JsonSerializerOptions SerializeOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>`ArchiveTilePayload` (DELETE /v1/tiles/{id}).</summary>
public sealed record ArchiveTileCommandPayload(
    [property: JsonPropertyName("tile_id")] string TileId);

/// <summary>`SetTileLifecyclePayload` (POST /v1/tiles/{id}/complete|defer).</summary>
public sealed record SetTileLifecycleCommandPayload(
    [property: JsonPropertyName("tile_id")] string TileId,
    [property: JsonPropertyName("state")] int State,
    [property: JsonPropertyName("deferred_until")] string? DeferredUntil,
    [property: JsonPropertyName("completed_at")] string? CompletedAt,
    [property: JsonPropertyName("bump_extend")] bool BumpExtend);

/// <summary>Wire `Span` (`{ start, end }` RFC 3339 instants).</summary>
public sealed record SpanWire(
    [property: JsonPropertyName("start")] string Start,
    [property: JsonPropertyName("end")] string End);

/// <summary>Wire `PlacementBaseline` (`{ span, inside }`).</summary>
public sealed record PlacementBaselineWire(
    [property: JsonPropertyName("span")] SpanWire Span,
    [property: JsonPropertyName("inside")] object? Inside);

/// <summary>
/// `StartTilePayload` (POST /v1/tiles/{id}/start). `source_ref` is the
/// all-empty `PlacementSourceRef` used for manual starts — every field
/// is optional on the Rust side, so `{}` deserializes to all-None.
/// </summary>
public sealed record StartTileCommandPayload(
    [property: JsonPropertyName("tile_id")] string TileId,
    [property: JsonPropertyName("plan_id")] string PlanId,
    [property: JsonPropertyName("source")] int Source,
    [property: JsonPropertyName("source_ref")] JsonElement SourceRef,
    [property: JsonPropertyName("baseline")] PlacementBaselineWire Baseline)
{
    /// <summary>Empty `PlacementSourceRef` — all producer pointers null.</summary>
    public static readonly JsonElement EmptySourceRef
        = JsonDocument.Parse("{}").RootElement.Clone();
}

/// <summary>`StartExecutionPayload` (POST /v1/placements/{id}/executions).</summary>
public sealed record StartExecutionCommandPayload(
    [property: JsonPropertyName("placement_id")] string PlacementId);

/// <summary>`FinishExecutionBody` (POST /v1/executions/{id}/finish payload).</summary>
public sealed record FinishExecutionCommandPayload(
    [property: JsonPropertyName("kind")] int Kind,
    [property: JsonPropertyName("note")] string? Note);
