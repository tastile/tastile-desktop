namespace TastileDesktop.Models;

using System.Text.Json.Serialization;

public record TileView(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("lifecycle")] string Lifecycle,
    [property: JsonPropertyName("next_action")] string? NextAction,
    [property: JsonPropertyName("done_definition")] string? DoneDefinition,
    [property: JsonPropertyName("worked_minutes")] long WorkedMinutes,
    [property: JsonPropertyName("semantic_role")] string SemanticRole
);

public record TilesResponse(
    [property: JsonPropertyName("tiles")] List<TileView> Tiles
);

public record ActiveTileResponse(
    [property: JsonPropertyName("tile")] TileView? Tile,
    [property: JsonPropertyName("phase")] string Phase,
    [property: JsonPropertyName("phase_started_at")] string? PhaseStartedAt,
    [property: JsonPropertyName("phase_ends_at")] string? PhaseEndsAt
);

public record ExecutionResponse(
    [property: JsonPropertyName("active_tile_id")] string? ActiveTileId,
    [property: JsonPropertyName("phase_kind")] string PhaseKind,
    [property: JsonPropertyName("phase_started_at")] string? PhaseStartedAt,
    [property: JsonPropertyName("phase_ends_at")] string? PhaseEndsAt,
    [property: JsonPropertyName("tile_count")] int TileCount,
    [property: JsonPropertyName("event_count")] int EventCount
);

public record CommandResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("events")] List<string> Events,
    [property: JsonPropertyName("error")] string? Error
);
