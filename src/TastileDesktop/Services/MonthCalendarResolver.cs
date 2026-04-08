using TastileDesktop.Models;

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
}
