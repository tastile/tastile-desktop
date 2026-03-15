namespace TastileDesktop.Models;

using System.Text.Json.Serialization;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

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

// Timeline segment (computed client-side from daemon events)
public record TimelineSegment
{
    public required string Kind { get; init; }       // "work" | "break" | "idle"
    public required string? TileTitle { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }          // null = ongoing
    public bool IsActive => EndedAt == null;

    public string TimeText => StartedAt.ToLocalTime().ToString("HH:mm");
    public string DurationText
    {
        get
        {
            var end = EndedAt ?? DateTime.UtcNow;
            var min = (int)(end - StartedAt).TotalMinutes;
            return min < 1 ? "<1m" : $"{min}m";
        }
    }
    public string StatusText => IsActive ? "▸ now" : DurationText;
    public string DisplayTitle => Kind switch
    {
        "work" => TileTitle ?? "Working",
        "break" => "Break",
        _ => "Idle"
    };

    public Windows.UI.Color BadgeColor => Kind switch
    {
        "work" => Windows.UI.Color.FromArgb(255, 0, 120, 212),
        "break" => Windows.UI.Color.FromArgb(255, 16, 124, 16),
        _ => Windows.UI.Color.FromArgb(255, 128, 128, 128),
    };
    public SolidColorBrush StatusForeground => IsActive
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 124, 16))
        : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
}
