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

    [Fact]
    public void Resolve_DayScale_ExpandsCanvasForShortBlocks_ToKeepReadableDensity()
    {
        var now = DateTimeOffset.Parse("2026-04-02T09:00:00+09:00");
        var items = new List<TimelineItemView>
        {
            new(
                Kind: "scheduled",
                TileId: "tile-short",
                SemanticRole: "work",
                Title: "Short task",
                StartedAt: "2026-04-02T09:00:00+09:00",
                EndedAt: "2026-04-02T09:05:00+09:00",
                DurationMin: 5,
                IsActive: false),
        };

        var layout = AbsoluteTimelineResolver.Resolve(
            items,
            now,
            new TimelineViewportSettings(
                ScaleUnit: TimelineScaleUnit.Day,
                RangeMode: TimelineRangeMode.Day24,
                AnchorLocal: now));

        Assert.True(layout.CanvasHeight > (24 * 120));
    }

    [Fact]
    public void Resolve_WeekScale_CanExpandToFourWeeks()
    {
        var now = DateTimeOffset.Parse("2026-04-02T09:00:00+09:00");
        var layout = AbsoluteTimelineResolver.Resolve(
            [],
            now,
            new TimelineViewportSettings(
                ScaleUnit: TimelineScaleUnit.Week,
                RangeMode: TimelineRangeMode.Week4,
                AnchorLocal: now));

        Assert.Equal(TimeSpan.FromDays(28), layout.WindowEnd - layout.WindowStart);
    }

    [Fact]
    public void Resolve_WeekScale_CanExpandToTwoWeeks()
    {
        var now = DateTimeOffset.Parse("2026-04-02T09:00:00+09:00");
        var layout = AbsoluteTimelineResolver.Resolve(
            [],
            now,
            new TimelineViewportSettings(
                ScaleUnit: TimelineScaleUnit.Week,
                RangeMode: TimelineRangeMode.Week2,
                AnchorLocal: now));

        Assert.Equal(TimeSpan.FromDays(14), layout.WindowEnd - layout.WindowStart);
    }

    [Fact]
    public void Resolve_MonthScale_CanExpandToSixMonths()
    {
        var now = DateTimeOffset.Parse("2026-04-02T09:00:00+09:00");
        var expectedStart = new DateTimeOffset(new DateTime(now.Year, now.Month, 1, 0, 0, 0), now.Offset);
        var layout = AbsoluteTimelineResolver.Resolve(
            [],
            now,
            new TimelineViewportSettings(
                ScaleUnit: TimelineScaleUnit.Month,
                RangeMode: TimelineRangeMode.Month6,
                AnchorLocal: now));

        Assert.Equal(expectedStart, layout.WindowStart);
        Assert.Equal(expectedStart.AddMonths(6), layout.WindowEnd);
    }
}
