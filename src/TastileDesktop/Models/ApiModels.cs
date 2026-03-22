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
    [property: JsonPropertyName("labels")] List<string>? Labels,
    [property: JsonPropertyName("objective_mode")] string? ObjectiveMode,
    [property: JsonPropertyName("target_work_min")] int? TargetWorkMin,
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
    [property: JsonPropertyName("severity")] string? Severity,
    [property: JsonPropertyName("tile_id")] string? TileId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("why")] string Why,
    [property: JsonPropertyName("suggested_minutes")] int? SuggestedMinutes,
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
    [property: JsonPropertyName("semantic_role")] string? SemanticRole,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("started_at")] string StartedAt,
    [property: JsonPropertyName("ended_at")] string? EndedAt,
    [property: JsonPropertyName("duration_min")] long DurationMin,
    [property: JsonPropertyName("is_active")] bool IsActive
);

public record CreateTileRequest(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("next_action")] string? NextAction,
    [property: JsonPropertyName("done_definition")] string? DoneDefinition,
    [property: JsonPropertyName("temporal")] CreateTileTemporalRequest? Temporal,
    [property: JsonPropertyName("objective")] CreateTileObjectiveRequest? Objective,
    [property: JsonPropertyName("interruption")] CreateTileInterruptionRequest? Interruption,
    [property: JsonPropertyName("automation")] CreateTileAutomationRequest? Automation,
    [property: JsonPropertyName("annotation")] CreateTileAnnotationRequest? Annotation
);

public record CreateTileTemporalRequest(
    [property: JsonPropertyName("release_at")] string? ReleaseAt,
    [property: JsonPropertyName("due_at")] string? DueAt,
    [property: JsonPropertyName("fixed_start")] string? FixedStart,
    [property: JsonPropertyName("fixed_end")] string? FixedEnd,
    [property: JsonPropertyName("active_start")] string? ActiveStart,
    [property: JsonPropertyName("active_end")] string? ActiveEnd
);

public record CreateTileObjectiveRequest(
    [property: JsonPropertyName("objective_mode")] string ObjectiveMode,
    [property: JsonPropertyName("target_work_min")] int? TargetWorkMin,
    [property: JsonPropertyName("target_rest_min")] int? TargetRestMin,
    [property: JsonPropertyName("done_rule")] string? DoneRule,
    [property: JsonPropertyName("recurrence")] CreateTileRecurrenceRequest? Recurrence
);

public record CreateTileRecurrenceRequest(
    [property: JsonPropertyName("generator")] CreateTileRecurrenceGeneratorRequest Generator,
    [property: JsonPropertyName("window")] CreateTileRecurrenceWindowRequest Window,
    [property: JsonPropertyName("selector")] CreateTileRecurrenceSelectorRequest Selector
);

public record CreateTileRecurrenceGeneratorRequest(
    [property: JsonPropertyName("step_min")] int StepMin,
    [property: JsonPropertyName("anchor_epoch_min")] long? AnchorEpochMin
);

public record CreateTileRecurrenceWindowRequest(
    [property: JsonPropertyName("start_offset_min")] int StartOffsetMin,
    [property: JsonPropertyName("end_offset_min")] int EndOffsetMin
);

public record CreateTileRecurrenceSelectorRequest(
    [property: JsonPropertyName("expression")] string? Expression
);

public record CreateTileInterruptionRequest(
    [property: JsonPropertyName("interrupt_penalty")] int InterruptPenalty,
    [property: JsonPropertyName("resume_penalty")] int ResumePenalty,
    [property: JsonPropertyName("break_splits_work")] bool BreakSplitsWork,
    [property: JsonPropertyName("external_interrupt_only")] bool ExternalInterruptOnly
);

public record CreateTileAutomationRequest(
    [property: JsonPropertyName("prompt_on_start")] bool PromptOnStart,
    [property: JsonPropertyName("prompt_on_end")] bool PromptOnEnd,
    [property: JsonPropertyName("auto_start_allowed")] bool AutoStartAllowed,
    [property: JsonPropertyName("auto_end_allowed")] bool AutoEndAllowed
);

public record CreateTileAnnotationRequest(
    [property: JsonPropertyName("semantic_role")] string SemanticRole,
    [property: JsonPropertyName("labels")] List<string> Labels,
    [property: JsonPropertyName("timed_labels")] List<CreateTileTimedLabelRequest> TimedLabels
);

public record CreateTileTimedLabelRequest(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("start_at")] string? StartAt,
    [property: JsonPropertyName("end_at")] string? EndAt
);
