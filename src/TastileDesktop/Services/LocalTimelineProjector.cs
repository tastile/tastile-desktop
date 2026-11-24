using System.Text.Json;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// Projects the local SQLite read model (NF04-D02) into the same
/// <see cref="TimelineTodayResponse"/> shape the online timeline path
/// produces, so offline launches still render schedule/actuals.
///
/// Source entities (v1/14 §6-3):
/// - PLACEMENT state = PlacementView JSON (`tile_id`, `span_start`,
///   `span_end`, `close`, `detach`, `id`). Placements with `close` or
///   `detach` set are excluded — they are no longer on the schedule.
/// - TILE state = TileDetailView JSON (`base.title`) for display names.
/// - EXECUTION state = ExecutionView JSON (`state`, `open_segment_start`,
///   `placement_id`, `effective_placement_id`); an execution with an open
///   segment marks its placement item `is_active` (actuals in progress).
///
/// The result is marked `local_snapshot`/`data_as_of` so the UI can show
/// the data's age instead of presenting it as live (NF04-D02 acceptance).
/// </summary>
public static class LocalTimelineProjector
{
    public static TimelineTodayResponse? Project(LocalReadModelStore store, DateTimeOffset anchorLocal)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (store.EntityCount() == 0)
        {
            return null;
        }

        var entities = store.GetEntities();

        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var activePlacementIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var placements = new List<PlacementRow>();

        foreach (var entity in entities)
        {
            switch (entity.EntityKind)
            {
                case SyncEntityKind.Tile:
                    if (TryReadTitle(entity.StateJson, out var title))
                    {
                        titles[entity.EntityId] = title;
                    }
                    break;

                case SyncEntityKind.Placement:
                    if (TryReadPlacement(entity.StateJson, out var placement))
                    {
                        placements.Add(placement);
                    }
                    break;

                case SyncEntityKind.Execution:
                    if (TryReadActivePlacementId(entity.StateJson, out var placementId))
                    {
                        activePlacementIds.Add(placementId);
                    }
                    break;
            }
        }

        var dayStartLocal = anchorLocal.Date;
        var dayEndLocal = dayStartLocal.AddDays(1);
        var dayStartUtc = new DateTimeOffset(dayStartLocal, anchorLocal.Offset).UtcDateTime;
        var dayEndUtc = new DateTimeOffset(dayEndLocal, anchorLocal.Offset).UtcDateTime;

        var items = placements
            .Where(p => p.StartUtc < dayEndUtc && p.EndUtc > dayStartUtc)
            .OrderBy(p => p.StartUtc)
            .Select(p => new TimelineItemView(
                Kind: "scheduled",
                TileId: p.TileId,
                SemanticRole: null,
                Title: titles.TryGetValue(p.TileId, out var t) ? t : p.TileId,
                StartedAt: p.StartRaw,
                EndedAt: p.EndRaw,
                DurationMin: Math.Max(0, (long)(p.EndUtc - p.StartUtc).TotalMinutes),
                IsActive: activePlacementIds.Contains(p.Id)))
            .ToList();

        var dataAsOf = store.LastSyncAt;
        return new TimelineTodayResponse(
            Items: items,
            RangeStart: dayStartUtc.ToString("O"),
            RangeEnd: dayEndUtc.ToString("O"),
            LocalSnapshot: true,
            DataAsOf: dataAsOf?.UtcDateTime.ToString("O"));
    }

    private sealed record PlacementRow(
        string Id,
        string TileId,
        DateTime StartUtc,
        DateTime EndUtc,
        string StartRaw,
        string EndRaw);

    private static bool TryReadTitle(string stateJson, out string title)
    {
        title = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(stateJson);
            var root = document.RootElement;
            // TileDetailView: { base: TileView { id, title, ... }, ... }
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("base", out var basis)
                && basis.ValueKind == JsonValueKind.Object
                && basis.TryGetProperty("title", out var titleElement)
                && titleElement.ValueKind == JsonValueKind.String)
            {
                title = titleElement.GetString() ?? string.Empty;
                return title.Length > 0;
            }
        }
        catch (JsonException)
        {
        }
        return false;
    }

    private static bool TryReadPlacement(string stateJson, out PlacementRow row)
    {
        row = null!;
        try
        {
            using var document = JsonDocument.Parse(stateJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (TryGetBool(root, "close", out var close) && close)
            {
                return false;
            }
            if (TryGetBool(root, "detach", out var detach) && detach)
            {
                return false;
            }
            if (!TryGetString(root, "id", out var id)
                || !TryGetString(root, "tile_id", out var tileId)
                || !TryGetString(root, "span_start", out var startRaw)
                || !TryGetString(root, "span_end", out var endRaw))
            {
                return false;
            }
            if (!DateTimeOffset.TryParse(startRaw, out var start)
                || !DateTimeOffset.TryParse(endRaw, out var end))
            {
                return false;
            }

            row = new PlacementRow(id, tileId, start.UtcDateTime, end.UtcDateTime, startRaw, endRaw);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadActivePlacementId(string stateJson, out string placementId)
    {
        placementId = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(stateJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // Only executions with an open segment represent live actuals.
            if (!TryGetString(root, "open_segment_start", out var segmentStart)
                || string.IsNullOrEmpty(segmentStart))
            {
                return false;
            }

            // ExecutionView carries both the planned placement and the
            // effective (resolved) placement; match either.
            if (TryGetString(root, "placement_id", out var planned) && !string.IsNullOrEmpty(planned))
            {
                placementId = planned;
                return true;
            }
            if (TryGetString(root, "effective_placement_id", out var effective) && !string.IsNullOrEmpty(effective))
            {
                placementId = effective;
                return true;
            }
        }
        catch (JsonException)
        {
        }
        return false;
    }

    private static bool TryGetString(JsonElement element, string property, out string value)
    {
        value = string.Empty;
        if (element.TryGetProperty(property, out var propertyElement)
            && propertyElement.ValueKind == JsonValueKind.String)
        {
            value = propertyElement.GetString() ?? string.Empty;
            return value.Length > 0;
        }
        return false;
    }

    private static bool TryGetBool(JsonElement element, string property, out bool value)
    {
        value = false;
        if (element.TryGetProperty(property, out var propertyElement)
            && propertyElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = propertyElement.GetBoolean();
            return true;
        }
        return false;
    }
}
