using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Services;

public sealed class MonthCalendarCell
{
    public DateTime Date { get; init; }
    public bool IsCurrentMonth { get; init; }
    public string DayNumber { get; init; } = string.Empty;
    public string Line1 { get; init; } = string.Empty;
    public string Line2 { get; init; } = string.Empty;
    public string Line3 { get; init; } = string.Empty;
    public string OverflowText { get; init; } = string.Empty;
}

public sealed class MonthCalendarRow
{
    public IReadOnlyList<MonthCalendarCell> Cells { get; init; } = [];
}

public sealed class YearCalendarMonth
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<MonthCalendarRow> Rows { get; init; } = [];
}

public sealed class WeekTimelineColumn
{
    public int DayOfWeekIndex { get; init; }
    public string DayLabel { get; init; } = string.Empty;
    public string DayNumber { get; init; } = string.Empty;
    public bool IsToday { get; init; }
    public IReadOnlyList<TimelineBlock> Blocks { get; init; } = [];
}

public static class MonthCalendarResolver
{
    public static IReadOnlyList<MonthCalendarRow> BuildRows(
        IReadOnlyList<TimelineItemView> items,
        DateTimeOffset anchorLocal)
    {
        var titlesByDate = BuildTitlesByDate(items);
        var monthStart = new DateTime(anchorLocal.Year, anchorLocal.Month, 1, 0, 0, 0, DateTimeKind.Local);
        return BuildRowsForMonth(titlesByDate, monthStart);
    }

    public static IReadOnlyList<MonthCalendarCell> BuildWeekRow(
        IReadOnlyList<TimelineItemView> items,
        DateTimeOffset anchorLocal)
    {
        var titlesByDate = BuildTitlesByDate(items);
        var anchorDate = anchorLocal.LocalDateTime.Date;
        var weekdayOffset = ((int)anchorDate.DayOfWeek + 6) % 7; // Monday-first
        var weekStart = anchorDate.AddDays(-weekdayOffset);
        var cells = new List<MonthCalendarCell>(7);
        for (var col = 0; col < 7; col++)
        {
            var date = weekStart.AddDays(col);
            var titles = titlesByDate.TryGetValue(date, out var list) ? list : [];
            cells.Add(CreateCell(date, true, titles));
        }

        return cells;
    }

    public static IReadOnlyList<WeekTimelineColumn> BuildWeekTimelineColumns(
    IReadOnlyList<TimelineItemView> items,
    DateTimeOffset anchorLocal,
    double hoursPerPixel)
{
    var todayLocal = DateTimeOffset.Now.ToLocalTime();
    var weekStart = GetWeekStart(anchorLocal);
    var columns = new List<WeekTimelineColumn>(7);

    for (int i = 0; i < 7; i++)
    {
        var dayDate = weekStart.AddDays(i);
        var dayItems = items
            .Where(item => IsItemOnDate(item, dayDate))
            .ToList();

        var blocks = ResolveDayBlocks(dayItems, dayDate, hoursPerPixel);

        columns.Add(new WeekTimelineColumn
        {
            DayOfWeekIndex = i,
            DayLabel = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }[i],
            DayNumber = $"{dayDate.Month}/{dayDate.Day}",
            IsToday = dayDate.Date == todayLocal.Date,
            Blocks = blocks
        });
    }

    return columns;
}

public static IReadOnlyList<IReadOnlyList<YearCalendarMonth>> BuildYearMonthRows(
        IReadOnlyList<TimelineItemView> items,
        DateTimeOffset anchorLocal)
    {
        var titlesByDate = BuildTitlesByDate(items);
        var months = BuildYearMonths(titlesByDate, anchorLocal).ToList();
        var rows = new List<IReadOnlyList<YearCalendarMonth>>(3);
        for (var index = 0; index < months.Count; index += 4)
        {
            rows.Add(months.Skip(index).Take(4).ToArray());
        }

        return rows;
    }

    private static IReadOnlyList<YearCalendarMonth> BuildYearMonths(
        Dictionary<DateTime, List<string>> titlesByDate,
        DateTimeOffset anchorLocal)
    {
        var year = anchorLocal.Year;
        var months = new List<YearCalendarMonth>(12);
        for (var month = 1; month <= 12; month++)
        {
            var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Local);
            months.Add(new YearCalendarMonth
            {
                Title = monthStart.ToString("yyyy MMM"),
                Rows = BuildRowsForMonth(titlesByDate, monthStart),
            });
        }

        return months;
    }

    private static IReadOnlyList<MonthCalendarRow> BuildRowsForMonth(
        Dictionary<DateTime, List<string>> titlesByDate,
        DateTime monthStart)
    {
        var weekdayOffset = ((int)monthStart.DayOfWeek + 6) % 7; // Monday-first
        var gridStart = monthStart.AddDays(-weekdayOffset);
        var rows = new List<MonthCalendarRow>(6);
        for (var row = 0; row < 6; row++)
        {
            var cells = new List<MonthCalendarCell>(7);
            for (var col = 0; col < 7; col++)
            {
                var date = gridStart.AddDays(row * 7 + col);
                var titles = titlesByDate.TryGetValue(date.Date, out var list) ? list : [];
                cells.Add(CreateCell(
                    date.Date,
                    date.Month == monthStart.Month && date.Year == monthStart.Year,
                    titles));
            }

            rows.Add(new MonthCalendarRow { Cells = cells });
        }

        return rows;
    }

    private static MonthCalendarCell CreateCell(DateTime date, bool isCurrentMonth, IReadOnlyList<string> titles)
        => new()
        {
            Date = date,
            IsCurrentMonth = isCurrentMonth,
            DayNumber = date.Day.ToString(),
            Line1 = titles.ElementAtOrDefault(0) ?? string.Empty,
            Line2 = titles.ElementAtOrDefault(1) ?? string.Empty,
            Line3 = titles.ElementAtOrDefault(2) ?? string.Empty,
            OverflowText = titles.Count > 3 ? $"+{titles.Count - 3} more" : string.Empty,
        };

    private static Dictionary<DateTime, List<string>> BuildTitlesByDate(IReadOnlyList<TimelineItemView> items)
    {
        var map = new Dictionary<DateTime, List<string>>();
        foreach (var item in items.OrderBy(value => value.StartedAt, StringComparer.Ordinal))
        {
            if (!DateTimeOffset.TryParse(item.StartedAt, out var start))
            {
                continue;
            }

            var end = DateTimeOffset.TryParse(item.EndedAt, out var parsedEnd)
                ? parsedEnd
                : start;
            if (end < start)
            {
                end = start;
            }

            var dayCursor = start.LocalDateTime.Date;
            var lastDay = end.LocalDateTime.Date;
            while (dayCursor <= lastDay)
            {
                if (!map.TryGetValue(dayCursor, out var titles))
                {
                    titles = [];
                    map[dayCursor] = titles;
                }
                titles.Add(item.Title);
                dayCursor = dayCursor.AddDays(1);
            }
        }

        return map;
    }

    private static bool IsItemOnDate(TimelineItemView item, DateTimeOffset targetDate)
    {
        if (string.IsNullOrWhiteSpace(item.StartedAt))
            return false;

        if (DateTimeOffset.TryParse(item.StartedAt, out var startAt))
        {
            return startAt.LocalDateTime.Date == targetDate.LocalDateTime.Date;
        }

        return false;
    }

    private static List<TimelineBlock> ResolveDayBlocks(
        List<TimelineItemView> dayItems,
        DateTimeOffset dayDate,
        double hoursPerPixel)
    {
        var blocks = new List<TimelineBlock>();
        var nowLocal = DateTimeOffset.Now.ToLocalTime();

        // Group overlapping items into lanes
        var lanes = new List<List<TimelineBlock>>();
        foreach (var item in dayItems)
        {
            if (string.IsNullOrWhiteSpace(item.StartedAt))
                continue;

            if (!DateTimeOffset.TryParse(item.StartedAt, out var startAt))
                continue;

            var endAt = ResolveEnd(item, startAt.ToLocalTime(), nowLocal);

            var startMinutes = (startAt.LocalDateTime - dayDate.LocalDateTime.Date).TotalMinutes;
            var endMinutes = (endAt.LocalDateTime - dayDate.LocalDateTime.Date).TotalMinutes;
            var durationMinutes = endAt - startAt;

            var top = startMinutes / 60.0 * hoursPerPixel;
            var height = durationMinutes.TotalMinutes / 60.0 * hoursPerPixel;

            // Find a lane that doesn't overlap
            int laneIndex = 0;
            for (; laneIndex < lanes.Count; laneIndex++)
            {
                var lane = lanes[laneIndex];
                var lastBlock = lane.LastOrDefault();
                if (lastBlock == null || lastBlock.Top + lastBlock.Height <= top)
                {
                    break;
                }
            }

            // Add new lane if needed
            while (laneIndex >= lanes.Count)
            {
                lanes.Add(new List<TimelineBlock>());
            }

            var block = new TimelineBlock
            {
                TileId = item.TileId,
                Title = item.Title,
                StartLabel = startAt.LocalDateTime.ToString("HH:mm"),
                EndLabel = endAt.LocalDateTime.ToString("HH:mm"),
                DurationLabel = $"{(int)durationMinutes.TotalMinutes}m",
                Kind = item.Kind ?? "task",
                IsActive = item.IsActive,
                IsDone = false, // Will be calculated based on end date
                Top = top,
                Height = Math.Max(24, height),
                Lane = laneIndex,
                TotalLanes = lanes.Count,
                IsFullWidth = false,
                IsBreak = string.Equals(item.Kind, "break", StringComparison.OrdinalIgnoreCase),
                IsLabelTile = string.Equals(item.SemanticRole, "label", StringComparison.OrdinalIgnoreCase),
            };

            lanes[laneIndex].Add(block);
            blocks.Add(block);
        }

        // Update lane counts after all items placed
        foreach (var block in blocks)
        {
            block.TotalLanes = lanes.Count;
        }

        return blocks;
    }

    private static DateTimeOffset ResolveEnd(TimelineItemView item, DateTimeOffset startLocal, DateTimeOffset nowLocal)
    {
        if (!string.IsNullOrWhiteSpace(item.EndedAt) && DateTimeOffset.TryParse(item.EndedAt, out var ended))
        {
            return ended.ToLocalTime();
        }

        if (item.DurationMin > 0)
        {
            return startLocal.AddMinutes(item.DurationMin);
        }

        if (item.IsActive)
        {
            return nowLocal;
        }

        return startLocal.AddMinutes(25);
    }

    private static DateTimeOffset GetWeekStart(DateTimeOffset date)
    {
        var localDate = date.ToLocalTime().Date;
        var dayOfWeek = (int)localDate.DayOfWeek;
        // Adjust so Monday = 0, Sunday = 6
        var adjustedDay = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        var weekStartLocal = localDate.AddDays(-adjustedDay);
        var offset = TimeZoneInfo.Local.GetUtcOffset(weekStartLocal);
        return new DateTimeOffset(weekStartLocal, offset);
    }
}
