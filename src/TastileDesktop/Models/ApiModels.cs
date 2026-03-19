namespace TastileDesktop.Models;

using System.Text.Json.Serialization;

public record TileView(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("lifecycle")] string Lifecycle,
    [property: JsonPropertyName("next_action")] string? NextAction,
    [property: JsonPropertyName("done_definition")] string? DoneDefinition,
    [property: JsonPropertyName("worked_minutes")] long WorkedMinutes,
    [property: JsonPropertyName("break_minutes")] long BreakMinutes,
    [property: JsonPropertyName("semantic_role")] string SemanticRole,
    [property: JsonPropertyName("resume_note")] string? ResumeNote
);

public record TilesResponse(
    [property: JsonPropertyName("tiles")] List<TileView> Tiles
);

public record ActiveTileResponse(
    [property: JsonPropertyName("tile")] TileView? Tile,
    [property: JsonPropertyName("phase")] string Phase,
    [property: JsonPropertyName("phase_started_at")] string? PhaseStartedAt,
    [property: JsonPropertyName("phase_ends_at")] string? PhaseEndsAt,
    [property: JsonPropertyName("resume_note")] string? ResumeNote,
    [property: JsonPropertyName("next_visible_action")] string? NextVisibleAction
);

public record ExecutionResponse(
    [property: JsonPropertyName("active_tile_id")] string? ActiveTileId,
    [property: JsonPropertyName("phase_kind")] string PhaseKind,
    [property: JsonPropertyName("phase_started_at")] string? PhaseStartedAt,
    [property: JsonPropertyName("phase_ends_at")] string? PhaseEndsAt,
    [property: JsonPropertyName("pending_prompt_id")] string? PendingPromptId,
    [property: JsonPropertyName("tile_count")] int TileCount,
    [property: JsonPropertyName("event_count")] int EventCount
);

public record CommandResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("events")] List<string> Events,
    [property: JsonPropertyName("tile_id")] string? TileId,
    [property: JsonPropertyName("error")] string? Error
);

public record PendingPromptResponse(
    [property: JsonPropertyName("prompt")] PromptView? Prompt
);

public record PromptView(
    [property: JsonPropertyName("prompt_id")] string PromptId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("tile_id")] string? TileId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("why")] string Why,
    [property: JsonPropertyName("actions")] List<PromptActionView> Actions,
    [property: JsonPropertyName("expires_at")] string? ExpiresAt,
    [property: JsonPropertyName("stale")] bool Stale
);

public record PromptActionView(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label
);

public record TimelineTodayResponse(
    [property: JsonPropertyName("items")] List<TimelineItemView> Items
);

public record TimelineItemView(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("tile_id")] string? TileId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("started_at")] string StartedAt,
    [property: JsonPropertyName("ended_at")] string? EndedAt,
    [property: JsonPropertyName("duration_min")] long DurationMin,
    [property: JsonPropertyName("is_active")] bool IsActive
);
