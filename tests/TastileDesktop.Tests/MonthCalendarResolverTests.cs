using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class MonthCalendarResolverTests
{
    [Fact]
    public void BuildRows_ReturnsSixWeeksWithSevenCells()
    {
        var rows = MonthCalendarResolver.BuildRows(
            [
                new TimelineItemView(
                    Kind: "scheduled",
                    TileId: "tile-1",
                    SemanticRole: "work",
                    Title: "Focus",
                    StartedAt: "2026-04-08T01:00:00Z",
                    EndedAt: "2026-04-08T02:00:00Z",
                    DurationMin: 60,
                    IsActive: false),
            ],
            new DateTimeOffset(2026, 4, 8, 9, 0, 0, TimeSpan.FromHours(9)));

        Assert.Equal(6, rows.Count);
        Assert.All(rows, row => Assert.Equal(7, row.Cells.Count));
        Assert.Equal("30", rows[0].Cells[0].DayNumber);
        Assert.Equal("10", rows[5].Cells[6].DayNumber);
    }

    [Fact]
    public void BuildRows_ExposesAllDayEntriesWithoutOverflowLimit()
    {
        var items = Enumerable.Range(1, 5)
            .Select(index => new TimelineItemView(
                Kind: "scheduled",
                TileId: $"tile-{index}",
                SemanticRole: "work",
                Title: $"Task {index}",
                StartedAt: $"2026-04-08T0{index}:00:00Z",
                EndedAt: $"2026-04-08T0{index}:30:00Z",
                DurationMin: 30,
                IsActive: false))
            .ToList();

        var rows = MonthCalendarResolver.BuildRows(
            items,
            new DateTimeOffset(2026, 4, 8, 9, 0, 0, TimeSpan.FromHours(9)));

        var dayCell = rows.SelectMany(row => row.Cells).First(cell => cell.DayNumber == "8" && cell.IsCurrentMonth);
        Assert.Equal(5, dayCell.Entries.Count);
        Assert.Equal("Task 1", dayCell.Entries[0].Title);
        Assert.Equal("30m", dayCell.Entries[0].DurationLabel);
        Assert.Equal("\uE73E", dayCell.Entries[0].StatusIconGlyph);
        Assert.Equal(string.Empty, dayCell.OverflowText);
    }

    [Fact]
    public void BuildWeekTimelineColumns_ReturnsSevenColumns_WhenThereAreNoItems()
    {
        var columns = MonthCalendarResolver.BuildWeekTimelineColumns(
            [],
            new DateTimeOffset(2026, 4, 8, 9, 0, 0, TimeSpan.FromHours(9)),
            120d);

        Assert.Equal(7, columns.Count);
        Assert.Equal(["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"], columns.Select(column => column.DayLabel));
        Assert.All(columns, column => Assert.Empty(column.Blocks));
    }

    [Fact]
    public void BuildWeekTimelineColumns_UsesDurationFallback_WhenEndedAtIsMissing()
    {
        var columns = MonthCalendarResolver.BuildWeekTimelineColumns(
            [
                new TimelineItemView(
                    Kind: "scheduled",
                    TileId: "tile-1",
                    SemanticRole: "work",
                    Title: "Duration fallback",
                    StartedAt: "2026-04-08T01:00:00Z",
                    EndedAt: null,
                    DurationMin: 60,
                    IsActive: false),
            ],
            new DateTimeOffset(2026, 4, 8, 9, 0, 0, TimeSpan.FromHours(9)),
            120d);

        var wed = columns.Single(column => column.DayLabel == "Wed");
        Assert.Single(wed.Blocks);
        Assert.Equal("60m", wed.Blocks[0].DurationLabel);
    }

    [Fact]
    public void BuildWeekTimelineColumns_UsesLocalOverlapGroupLaneCount_InsteadOfDayWideMax()
    {
        var columns = MonthCalendarResolver.BuildWeekTimelineColumns(
            [
                new TimelineItemView(
                    Kind: "scheduled",
                    TileId: "tile-a",
                    SemanticRole: "work",
                    Title: "Overlap A",
                    StartedAt: "2026-04-08T09:00:00+09:00",
                    EndedAt: "2026-04-08T10:00:00+09:00",
                    DurationMin: 60,
                    IsActive: false),
                new TimelineItemView(
                    Kind: "scheduled",
                    TileId: "tile-b",
                    SemanticRole: "work",
                    Title: "Overlap B",
                    StartedAt: "2026-04-08T09:30:00+09:00",
                    EndedAt: "2026-04-08T10:30:00+09:00",
                    DurationMin: 60,
                    IsActive: false),
                new TimelineItemView(
                    Kind: "scheduled",
                    TileId: "tile-c",
                    SemanticRole: "work",
                    Title: "Solo Later",
                    StartedAt: "2026-04-08T14:00:00+09:00",
                    EndedAt: "2026-04-08T15:00:00+09:00",
                    DurationMin: 60,
                    IsActive: false),
            ],
            new DateTimeOffset(2026, 4, 8, 9, 0, 0, TimeSpan.FromHours(9)),
            120d);

        var allBlocks = columns.SelectMany(column => column.Blocks).ToList();
        var overlapA = allBlocks.Single(block => block.Title == "Overlap A");
        var overlapB = allBlocks.Single(block => block.Title == "Overlap B");
        var soloLater = allBlocks.Single(block => block.Title == "Solo Later");

        Assert.Equal(2, overlapA.TotalLanes);
        Assert.Equal(2, overlapB.TotalLanes);
        Assert.Equal(1, soloLater.TotalLanes);
        Assert.Equal(0, soloLater.Lane);
    }
}
