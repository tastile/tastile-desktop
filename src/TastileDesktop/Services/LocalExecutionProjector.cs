using System.Text.Json;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>Parsed EXECUTION sync entity (v1/14 §6-3 kind=3, ExecutionView state).</summary>
public sealed record LocalExecutionInfo(
    string ExecutionId,
    string? TileId,
    string? PlacementId,
    int State,
    long? Revision,
    string? UpdatedAt,
    int? OpenSegmentKind,
    string? OpenSegmentStart)
{
    /// <summary>State is ACTIVE or PAUSED — the execution still accepts commands.</summary>
    public bool IsLive => State is ExecutionStateValue.Active or ExecutionStateValue.Paused;

    public string StateLabel => State switch
    {
        ExecutionStateValue.Active => "active",
        ExecutionStateValue.Paused => "paused",
        ExecutionStateValue.FinishedNormal => "finished",
        ExecutionStateValue.FinishedVoid => "voided",
        _ => $"unknown({State})",
    };
}

/// <summary>Parsed TILE sync entity (`TileDetailView` state, v1/14 §6-3 kind=0).</summary>
public sealed record LocalTileInfo(
    string TileId,
    string? PlanId,
    string? Title,
    int? Kind,
    long? Revision)
{
    public bool HasExecutablePlan => !string.IsNullOrEmpty(PlanId);
}

/// <summary>Parsed PLACEMENT sync entity (EffectivePlacement view, kind=2).</summary>
public sealed record LocalPlacementInfo(
    string PlacementId,
    string? TileId,
    string? PlanId,
    bool Closed,
    string? SpanStart,
    string? SpanEnd,
    long? Revision);

/// <summary>
/// Read-side projection over the local SQLite read model for the
/// execution-control and editing surfaces (desktop NF04-D04/D05).
/// Entity `state` documents are the same JSON the v1 Read endpoints
/// return (v1/14 §6-2); parsing is defensive — unknown/missing fields
/// degrade to null rather than throwing, because the read model is a
/// cache, not a validated store.
/// </summary>
public sealed class LocalExecutionProjector
{
    private readonly LocalReadModelStore _store;

    public LocalExecutionProjector(LocalReadModelStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>All EXECUTION entities, newest update first.</summary>
    public IReadOnlyList<LocalExecutionInfo> GetExecutions()
        => _store.GetEntities(SyncEntityKind.Execution)
            .Select(ParseExecution)
            .Where(e => e is not null)
            .Cast<LocalExecutionInfo>()
            .OrderByDescending(e => e.UpdatedAt, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The single live execution (ACTIVE preferred over PAUSED, then
    /// newest `updated_at`). v1/10 §6 allows at most one running
    /// execution per placement; multiple live executions across
    /// placements are still possible, so "the" live execution is the
    /// most recently updated one — the same heuristic the UI needs for
    /// "what is the user looking at".
    /// </summary>
    public LocalExecutionInfo? GetLiveExecution()
        => GetExecutions().FirstOrDefault(e => e.IsLive);

    /// <summary>Live executions for one tile (any state segment).</summary>
    public IReadOnlyList<LocalExecutionInfo> GetExecutionsForTile(string tileId)
        => GetExecutions()
            .Where(e => string.Equals(e.TileId, tileId, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>All TILE entities keyed by tile id.</summary>
    public IReadOnlyList<LocalTileInfo> GetTiles()
        => _store.GetEntities(SyncEntityKind.Tile)
            .Select(ParseTile)
            .Where(t => t is not null)
            .Cast<LocalTileInfo>()
            .ToList();

    /// <summary>Tile detail for one id, or null when not synced locally.</summary>
    public LocalTileInfo? GetTile(string tileId)
        => GetTiles().FirstOrDefault(t =>
            string.Equals(t.TileId, tileId, StringComparison.OrdinalIgnoreCase));

    /// <summary>All PLACEMENT entities, newest span start first.</summary>
    public IReadOnlyList<LocalPlacementInfo> GetPlacements()
        => _store.GetEntities(SyncEntityKind.Placement)
            .Select(ParsePlacement)
            .Where(p => p is not null)
            .Cast<LocalPlacementInfo>()
            .OrderByDescending(p => p.SpanStart, StringComparer.Ordinal)
            .ToList();

    /// <summary>Open (non-closed) placements, optionally filtered by tile/plan.</summary>
    public IReadOnlyList<LocalPlacementInfo> GetOpenPlacements(string? tileId = null, string? planId = null)
        => GetPlacements()
            .Where(p => !p.Closed)
            .Where(p => tileId is null || string.Equals(p.TileId, tileId, StringComparison.OrdinalIgnoreCase))
            .Where(p => planId is null || string.Equals(p.PlanId, planId, StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static LocalExecutionInfo? ParseExecution(LocalSyncEntity entity)
    {
        var root = Parse(entity.StateJson);
        if (root.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return new LocalExecutionInfo(
            ExecutionId: ReadString(root, "id") ?? entity.EntityId,
            TileId: ReadString(root, "tile_id"),
            PlacementId: ReadString(root, "placement_id"),
            State: ReadInt(root, "state") ?? -1,
            Revision: ReadLong(root, "revision"),
            UpdatedAt: ReadString(root, "updated_at"),
            OpenSegmentKind: ReadInt(root, "open_segment_kind"),
            OpenSegmentStart: ReadString(root, "open_segment_start"));
    }

    private static LocalTileInfo? ParseTile(LocalSyncEntity entity)
    {
        var root = Parse(entity.StateJson);
        if (root.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        // TileDetailView: { base: { id, plan_id, title, kind, revision, ... }, labels, recurring, resolved_at }
        var baser = TryGetProperty(root, "base");
        var source = baser.ValueKind == JsonValueKind.Undefined ? root : baser;
        return new LocalTileInfo(
            TileId: ReadString(source, "id") ?? entity.EntityId,
            PlanId: ReadString(source, "plan_id"),
            Title: ReadString(source, "title"),
            Kind: ReadInt(source, "kind"),
            Revision: ReadLong(source, "revision"));
    }

    private static LocalPlacementInfo? ParsePlacement(LocalSyncEntity entity)
    {
        var root = Parse(entity.StateJson);
        if (root.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return new LocalPlacementInfo(
            PlacementId: ReadString(root, "id") ?? entity.EntityId,
            TileId: ReadString(root, "tile_id"),
            PlanId: ReadString(root, "plan_id"),
            Closed: ReadBool(root, "close") ?? ReadBool(root, "closed") ?? false,
            SpanStart: ReadString(root, "span_start"),
            SpanEnd: ReadString(root, "span_end"),
            Revision: ReadLong(root, "revision"));
    }

    /// <summary>Parses state JSON; returns an Undefined element on failure.</summary>
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

    /// <summary>Child object property, or Undefined when absent/not an object.</summary>
    private static JsonElement TryGetProperty(JsonElement element, string name)
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

    private static long? ReadLong(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var value))
        {
            return null;
        }
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var l) => l,
            JsonValueKind.String when long.TryParse(value.GetString(), out var l) => l,
            _ => null,
        };
    }

    private static bool? ReadBool(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean()
                : null;
}
