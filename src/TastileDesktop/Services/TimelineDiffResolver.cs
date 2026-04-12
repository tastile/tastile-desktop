using TastileDesktop.Models;

namespace TastileDesktop.Services;

public static class TimelineDiffResolver
{
    public static bool HasTimelineChanged(TimelineTodayResponse? oldTimeline, TimelineTodayResponse? currentTimeline)
    {
        var oldHash = BuildHash(oldTimeline);
        var currentHash = BuildHash(currentTimeline);
        return !string.Equals(oldHash, currentHash, StringComparison.Ordinal);
    }

    private static string? BuildHash(TimelineTodayResponse? timeline)
    {
        if (timeline == null)
        {
            return null;
        }

        var items = string.Join(
            ",",
            timeline.Items.Select(item =>
                $"{item.Kind}:{item.TileId}:{item.SemanticRole}:{item.Title}:{item.StartedAt}:{item.EndedAt}:{item.IsActive}:{item.DurationMin}"));
        return $"{timeline.RangeStart}|{timeline.RangeEnd}|{items}";
    }
}
