using TastileDesktop.Models;

namespace TastileDesktop.Services;

public static class TileHashResolver
{
    public static string Build(TilesResponse current)
    {
        if (current?.Tiles == null)
        {
            return string.Empty;
        }

        return string.Join(",", current.Tiles.Select(t =>
            $"{t.Id}:{t.Lifecycle}:{t.ResumeNote}:{t.WorkedMinutes}:{t.BreakMinutes}:{t.ProjectedNextStartAt}:{t.Temporal?.FixedStart}:{t.Temporal?.FixedEnd}:{t.Temporal?.ActiveStart}:{t.Temporal?.ActiveEnd}"));
    }
}
