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
    public void BuildRows_ShowsOverflowAsMoreLabel()
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
        Assert.Contains("Task 1", dayCell.Line1);
        Assert.Contains("Task 2", dayCell.Line2);
        Assert.Contains("Task 3", dayCell.Line3);
        Assert.Equal("+2 more", dayCell.OverflowText);
    }
}
