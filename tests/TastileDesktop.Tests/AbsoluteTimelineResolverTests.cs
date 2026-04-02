using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class AbsoluteTimelineResolverTests
{
    [Fact]
    public void Resolve_AssignsSeparateLanes_ForOverlappingTimelineItems()
    {
        var now = DateTimeOffset.Parse("2026-04-02T09:00:00+09:00");
        var items = new List<TimelineItemView>
        {
            new(
                Kind: "scheduled",
                TileId: "tile-a",
                SemanticRole: "work",
                Title: "Recurring fixed A",
                StartedAt: "2026-04-02T09:00:00+09:00",
                EndedAt: "2026-04-02T10:00:00+09:00",
                DurationMin: 60,
                IsActive: false),
            new(
                Kind: "scheduled",
                TileId: "tile-b",
                SemanticRole: "work",
                Title: "Recurring fixed B",
                StartedAt: "2026-04-02T09:00:00+09:00",
                EndedAt: "2026-04-02T10:00:00+09:00",
                DurationMin: 60,
                IsActive: false),
        };

        var layout = AbsoluteTimelineResolver.Resolve(
            items,
            now,
            new TimelineViewportSettings(
                ScaleUnit: TimelineScaleUnit.Day,
                RangeMode: TimelineRangeMode.Day24,
                AnchorLocal: now));

        Assert.Equal(2, layout.Blocks.Count);
        Assert.All(layout.Blocks, block => Assert.Equal(2, block.TotalLanes));

        var tileALanes = layout.Blocks
            .Where(block => string.Equals(block.TileId, "tile-a", StringComparison.Ordinal))
            .Select(block => block.Lane)
            .Distinct()
            .ToArray();
        var tileBLanes = layout.Blocks
            .Where(block => string.Equals(block.TileId, "tile-b", StringComparison.Ordinal))
            .Select(block => block.Lane)
            .Distinct()
            .ToArray();

        Assert.NotEmpty(tileALanes);
        Assert.NotEmpty(tileBLanes);
        Assert.DoesNotContain(tileALanes, lane => tileBLanes.Contains(lane));
    }
}
