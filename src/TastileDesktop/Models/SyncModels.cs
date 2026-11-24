using System.Text.Json;
using System.Text.Json.Serialization;

namespace TastileDesktop.Models;

// Sync API v2 wire models (tastile-core v1/14 §6, core#558/#559).
// Field names and numeric constants mirror the OpenAPI contract
// (tastile-openapi v4.0.0); `state` stays an opaque JsonElement because
// it is the entity kind's Read-view document, not a sync-only shape.

/// <summary>v1/14 §6-3 SyncEntityKind — append-only numeric registry.</summary>
public static class SyncEntityKind
{
    public const int Tile = 0;
    public const int SourceTile = 1;
    public const int Placement = 2;
    public const int Execution = 3;
    public const int Session = 4;
}

/// <summary>v1/14 §6-4 SyncChangeKind (2 values).</summary>
public static class SyncChangeKind
{
    public const int Upsert = 0;
    public const int Tombstone = 1;
}

/// <summary>One snapshot entity (`GET /v1/sync/snapshot`).</summary>
public sealed record SyncEntity(
    [property: JsonPropertyName("entity_kind")] int EntityKind,
    [property: JsonPropertyName("entity_id")] string EntityId,
    [property: JsonPropertyName("state")] JsonElement? State);

/// <summary>One delta entry (`GET /v1/sync`).</summary>
public sealed record SyncChange(
    [property: JsonPropertyName("change_kind")] int ChangeKind,
    [property: JsonPropertyName("entity_kind")] int EntityKind,
    [property: JsonPropertyName("entity_id")] string EntityId,
    [property: JsonPropertyName("occurred_at")] string OccurredAt,
    [property: JsonPropertyName("state")] JsonElement? State);

/// <summary>`GET /v1/sync` page. `next_cursor` is empty when no events remain.</summary>
public sealed record SyncResponse(
    [property: JsonPropertyName("changes")] List<SyncChange> Changes,
    [property: JsonPropertyName("next_cursor")] string NextCursor);

/// <summary>`GET /v1/sync/snapshot` page.</summary>
public sealed record SyncSnapshotResponse(
    [property: JsonPropertyName("snapshot_cursor")] string SnapshotCursor,
    [property: JsonPropertyName("entities")] List<SyncEntity> Entities,
    [property: JsonPropertyName("next_cursor")] string? NextCursor);
