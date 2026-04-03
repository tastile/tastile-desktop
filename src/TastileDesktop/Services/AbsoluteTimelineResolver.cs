using TastileDesktop.Models;

namespace TastileDesktop.Services;

public enum TimelineScaleUnit
{
    Day,
    Week,
    Month,
}

public enum TimelineRangeMode
{
    Day24,
    AroundNow24,
    SunriseToSunset,
    Custom,
}

public sealed record TimelineViewportSettings(
    TimelineScaleUnit ScaleUnit,
    TimelineRangeMode RangeMode,
    DateTimeOffset AnchorLocal,
    DateTimeOffset? CustomStartLocal = null,
    DateTimeOffset? CustomEndLocal = null,
    int PixelsPerHourBase = 120,
    double ZoomScale = 1.0,
    double MinZoomScale = 0.4,
    double MaxZoomScale = 16.0,
    int LaneGap = 4,
    int MinBlockHeight = 44);

public sealed class TimelineLayout
{
    public List<TimelineHourMarker> HourMarkers { get; set; } = [];
    public List<TimelineBlock> Blocks { get; set; } = [];
    public List<TimelineNowIndicator> NowIndicators { get; set; } = [];
    public double CanvasHeight { get; set; }
    public string RangeLabel { get; set; } = string.Empty;
    public DateTimeOffset WindowStart { get; set; }
    public DateTimeOffset WindowEnd { get; set; }
}

public sealed class TimelineHourMarker
{
    public string Label { get; set; } = string.Empty;
    public double Top { get; set; }
}

public sealed class TimelineNowIndicator
{
    public double Top { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class TimelineBlock
{
    public string? TileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string StartLabel { get; set; } = string.Empty;
    public string EndLabel { get; set; } = string.Empty;
    public string DurationLabel { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public bool IsBreak { get; set; }
    public bool IsActive { get; set; }
    public bool IsDone { get; set; }
    public bool IsLabelTile { get; set; }
    public int Lane { get; set; }
    public int TotalLanes { get; set; } = 1;
    public bool IsFullWidth { get; set; }
    public double Top { get; set; }
    public double Height { get; set; }
}

internal sealed class RawTimelineSegment
{
    public required TimelineItemView Item { get; init; }
    public required DateTimeOffset Start { get; init; }
    public required DateTimeOffset End { get; init; }
    public required double Top { get; init; }
    public required double Height { get; init; }
    public required double Bottom { get; init; }
    public required string Key { get; init; }
    public int Lane { get; set; }
    public int TotalLanes { get; set; } = 1;
}

public static class AbsoluteTimelineResolver
{
    public static TimelineLayout Resolve(List<TimelineItemView> items, DateTimeOffset now, TimelineViewportSettings settings)
    {
        var localNow = now.ToLocalTime();
        var zoomScale = Math.Clamp(settings.ZoomScale, settings.MinZoomScale, settings.MaxZoomScale);
        var (windowStart, windowEnd) = ResolveWindow(settings, localNow);
        var windowDurationMinutes = Math.Max(1, (windowEnd - windowStart).TotalMinutes);
        var pxPerMinuteBase = (settings.PixelsPerHourBase * zoomScale) / 60d;
        var pxPerMinute = ResolveReadablePxPerMinute(
            items,
            localNow,
            windowStart,
            windowEnd,
            pxPerMinuteBase,
            settings.MinBlockHeight);
        var canvasHeight = windowDurationMinutes * pxPerMinute;

        var segments = ResolveSegments(items, localNow, windowStart, windowEnd, pxPerMinute, settings.MinBlockHeight);
        AssignLanes(segments);

        return new TimelineLayout
        {
            HourMarkers = BuildHourMarkers(windowStart, windowEnd, pxPerMinute, settings.ScaleUnit, zoomScale),
            Blocks = segments.OrderBy(segment => segment.Top).Select(segment => ToBlock(segment, now.ToLocalTime())).ToList(),
            NowIndicators = BuildNowIndicators(localNow, settings.ScaleUnit, windowStart, windowEnd, pxPerMinute),
            CanvasHeight = canvasHeight,
            RangeLabel = BuildRangeLabel(settings.ScaleUnit, windowStart, windowEnd),
            WindowStart = windowStart,
            WindowEnd = windowEnd,
        };
    }

    private static double ResolveReadablePxPerMinute(
        List<TimelineItemView> items,
        DateTimeOffset nowLocal,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        double pxPerMinuteBase,
        int minBlockHeight)
    {
        var minVisibleDurationMinutes = double.MaxValue;

        foreach (var item in items)
        {
            if (!DateTimeOffset.TryParse(item.StartedAt, out var start))
            {
                continue;
            }

            var startLocal = start.ToLocalTime();
            var endLocal = ResolveEnd(item, startLocal, nowLocal);
            if (endLocal <= startLocal)
            {
                continue;
            }

            var clippedStart = startLocal < windowStart ? windowStart : startLocal;
            var clippedEnd = endLocal > windowEnd ? windowEnd : endLocal;
            if (clippedEnd <= clippedStart)
            {
                continue;
            }

            var durationMinutes = (clippedEnd - clippedStart).TotalMinutes;
            if (durationMinutes > 0)
            {
                minVisibleDurationMinutes = Math.Min(minVisibleDurationMinutes, durationMinutes);
            }
        }

        if (minVisibleDurationMinutes == double.MaxValue)
        {
            return pxPerMinuteBase;
        }

        var readablePxPerMinute = minBlockHeight / minVisibleDurationMinutes;
        var cappedReadablePxPerMinute = Math.Min(readablePxPerMinute, pxPerMinuteBase * 1.35d);
        return Math.Max(pxPerMinuteBase, cappedReadablePxPerMinute);
    }

    private static (DateTimeOffset Start, DateTimeOffset End) ResolveWindow(TimelineViewportSettings settings, DateTimeOffset localNow)
    {
        var anchor = settings.AnchorLocal.ToLocalTime();
        if (settings.ScaleUnit == TimelineScaleUnit.Day)
        {
            return settings.RangeMode switch
            {
                TimelineRangeMode.AroundNow24 => (localNow.AddHours(-12), localNow.AddHours(12)),
                TimelineRangeMode.SunriseToSunset => ResolveSunriseSunsetWindow(anchor),
                TimelineRangeMode.Custom when settings.CustomStartLocal.HasValue && settings.CustomEndLocal.HasValue
                    => NormalizeWindow(settings.CustomStartLocal.Value.ToLocalTime(), settings.CustomEndLocal.Value.ToLocalTime()),
                _ => ResolveDayWindow(anchor),
            };
        }

        if (settings.ScaleUnit == TimelineScaleUnit.Week)
        {
            var weekStart = StartOfWeek(anchor);
            return (weekStart, weekStart.AddDays(7));
        }

        var monthStart = new DateTimeOffset(new DateTime(anchor.Year, anchor.Month, 1, 0, 0, 0), anchor.Offset);
        return (monthStart, monthStart.AddMonths(1));
    }

    private static (DateTimeOffset Start, DateTimeOffset End) ResolveDayWindow(DateTimeOffset anchor)
    {
        var dayStart = new DateTimeOffset(anchor.Year, anchor.Month, anchor.Day, 0, 0, 0, anchor.Offset);
        return (dayStart, dayStart.AddDays(1));
    }

    private static (DateTimeOffset Start, DateTimeOffset End) ResolveSunriseSunsetWindow(DateTimeOffset anchor)
    {
        var sunrise = new DateTimeOffset(anchor.Year, anchor.Month, anchor.Day, 6, 0, 0, anchor.Offset);
        var sunset = new DateTimeOffset(anchor.Year, anchor.Month, anchor.Day, 18, 0, 0, anchor.Offset);
        return (sunrise, sunset);
    }

    private static (DateTimeOffset Start, DateTimeOffset End) NormalizeWindow(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start) return (start, start.AddHours(1));
        return (start, end);
    }

    private static DateTimeOffset StartOfWeek(DateTimeOffset value)
    {
        var day = (int)value.DayOfWeek;
        var diff = day == 0 ? -6 : 1 - day;
        var start = value.AddDays(diff);
        return new DateTimeOffset(start.Year, start.Month, start.Day, 0, 0, 0, start.Offset);
    }

    private static List<TimelineHourMarker> BuildHourMarkers(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        double pxPerMinute,
        TimelineScaleUnit scaleUnit,
        double zoomScale)
    {
        var markers = new List<TimelineHourMarker>();
        var step = ResolveMarkerStep(scaleUnit, zoomScale);
        var cursor = AlignCursor(windowStart, step);

        while (cursor <= windowEnd.AddMinutes(0.1))
        {
            var top = (cursor - windowStart).TotalMinutes * pxPerMinute;
            markers.Add(new TimelineHourMarker
            {
                Label = FormatMarkerLabel(cursor, scaleUnit, step),
                Top = top,
            });
            cursor = cursor.AddMinutes(step);
        }
        return markers;
    }

    private static int ResolveMarkerStep(TimelineScaleUnit scaleUnit, double zoomScale)
    {
        if (scaleUnit == TimelineScaleUnit.Day)
        {
            if (zoomScale >= 6) return 15;
            if (zoomScale >= 2.5) return 30;
            return 60;
        }

        if (scaleUnit == TimelineScaleUnit.Week)
        {
            if (zoomScale >= 5) return 60;
            if (zoomScale >= 2.5) return 180;
            if (zoomScale >= 1.2) return 360;
            return 720;
        }

        if (zoomScale >= 4) return 360;
        if (zoomScale >= 1.8) return 720;
        return 1440;
    }

    private static DateTimeOffset AlignCursor(DateTimeOffset start, int stepMinutes)
    {
        if (stepMinutes >= 1440)
        {
            var date = new DateTimeOffset(start.Year, start.Month, start.Day, 0, 0, 0, start.Offset);
            return date < start ? date.AddDays(1) : date;
        }

        var totalMinutes = start.Hour * 60 + start.Minute;
        var nextBucket = ((totalMinutes + stepMinutes - 1) / stepMinutes) * stepMinutes;
        var dayStart = new DateTimeOffset(start.Year, start.Month, start.Day, 0, 0, 0, start.Offset);
        return dayStart.AddMinutes(nextBucket);
    }

    private static string FormatMarkerLabel(DateTimeOffset value, TimelineScaleUnit scaleUnit, int stepMinutes)
    {
        if (scaleUnit == TimelineScaleUnit.Month || stepMinutes >= 1440)
        {
            return value.ToString("M/d");
        }

        if (scaleUnit == TimelineScaleUnit.Week && stepMinutes >= 360)
        {
            return value.ToString("ddd HH:mm");
        }

        return value.ToString("HH:mm");
    }

    private static List<TimelineNowIndicator> BuildNowIndicators(DateTimeOffset localNow, TimelineScaleUnit scale, DateTimeOffset windowStart, DateTimeOffset windowEnd, double pxPerMinute)
    {
        if (localNow < windowStart || localNow > windowEnd) return [];

        var top = (localNow - windowStart).TotalMinutes * pxPerMinute;
        var label = scale switch
        {
            TimelineScaleUnit.Day => localNow.ToString("HH:mm"),
            TimelineScaleUnit.Week => localNow.ToString("ddd HH:mm"),
            _ => localNow.ToString("M/d HH:mm"),
        };
        return [new TimelineNowIndicator { Top = top, Label = label }];
    }

    private static List<RawTimelineSegment> ResolveSegments(
        List<TimelineItemView> items,
        DateTimeOffset now,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        double pxPerMinute,
        int minBlockHeight)
    {
        var result = new List<RawTimelineSegment>();
        foreach (var item in items)
        {
            if (!DateTimeOffset.TryParse(item.StartedAt, out var start)) continue;
            var startLocal = start.ToLocalTime();
            var endLocal = ResolveEnd(item, startLocal, now);
            if (endLocal <= startLocal) continue;

            var clippedStart = startLocal < windowStart ? windowStart : startLocal;
            var clippedEnd = endLocal > windowEnd ? windowEnd : endLocal;
            if (clippedEnd <= clippedStart) continue;

            var top = (clippedStart - windowStart).TotalMinutes * pxPerMinute;
            var height = Math.Max(minBlockHeight, (clippedEnd - clippedStart).TotalMinutes * pxPerMinute);
            var isLabel = string.Equals(item.Kind, "label", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.SemanticRole, "label", StringComparison.OrdinalIgnoreCase)
                || item.Title.Contains("label", StringComparison.OrdinalIgnoreCase)
                || item.Title.Contains("ラベル", StringComparison.OrdinalIgnoreCase);

            result.Add(new RawTimelineSegment
            {
                Item = item,
                Start = clippedStart,
                End = clippedEnd,
                Top = top,
                Height = height,
                Bottom = top + height,
                Key = $"{item.TileId ?? item.Title}:{startLocal:O}",
            });
        }

        return result
            .OrderBy(segment => segment.Top)
            .ThenByDescending(segment => segment.Height)
            .ToList();
    }

    private static DateTimeOffset ResolveEnd(TimelineItemView item, DateTimeOffset startLocal, DateTimeOffset now)
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
            return now.ToLocalTime();
        }

        return startLocal.AddMinutes(25);
    }

    private static void AssignLanes(List<RawTimelineSegment> segments)
    {
        static bool Overlaps(RawTimelineSegment a, RawTimelineSegment b) => a.Top < b.Bottom && a.Bottom > b.Top;
        var normalIndexes = Enumerable.Range(0, segments.Count)
            .Where(index => !string.Equals(segments[index].Item.SemanticRole, "label", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(segments[index].Item.Kind, "label", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var lanes = new List<List<int>>();
        foreach (var index in normalIndexes)
        {
            var segment = segments[index];
            var laneIndex = -1;
            for (var l = 0; l < lanes.Count; l++)
            {
                if (!lanes[l].Any(existingIndex => Overlaps(segments[existingIndex], segment)))
                {
                    laneIndex = l;
                    break;
                }
            }

            if (laneIndex == -1)
            {
                laneIndex = lanes.Count;
                lanes.Add([]);
            }

            segment.Lane = laneIndex;
            lanes[laneIndex].Add(index);
        }

        var groupByIndex = Enumerable.Repeat(-1, segments.Count).ToArray();
        var groupLaneCount = new Dictionary<int, int>();
        var groupId = 0;

        foreach (var rootIndex in normalIndexes)
        {
            if (groupByIndex[rootIndex] != -1) continue;
            var stack = new Stack<int>();
            stack.Push(rootIndex);
            groupByIndex[rootIndex] = groupId;
            var maxLane = segments[rootIndex].Lane;

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                foreach (var candidate in normalIndexes)
                {
                    if (groupByIndex[candidate] != -1) continue;
                    if (!Overlaps(segments[current], segments[candidate])) continue;
                    groupByIndex[candidate] = groupId;
                    maxLane = Math.Max(maxLane, segments[candidate].Lane);
                    stack.Push(candidate);
                }
            }

            groupLaneCount[groupId] = Math.Max(1, maxLane + 1);
            groupId++;
        }

        for (var i = 0; i < segments.Count; i++)
        {
            if (string.Equals(segments[i].Item.SemanticRole, "label", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segments[i].Item.Kind, "label", StringComparison.OrdinalIgnoreCase))
            {
                segments[i].Lane = 0;
                segments[i].TotalLanes = 1;
                continue;
            }

            var group = groupByIndex[i];
            segments[i].TotalLanes = group >= 0 && groupLaneCount.TryGetValue(group, out var count)
                ? count
                : 1;
        }
    }

    private static TimelineBlock ToBlock(RawTimelineSegment segment, DateTimeOffset nowLocal)
    {
        var durationMinutes = Math.Max(1, (int)Math.Round((segment.End - segment.Start).TotalMinutes));
        var durationLabel = durationMinutes >= 60
            ? $"{durationMinutes / 60}h {durationMinutes % 60}m"
            : $"{durationMinutes}m";
        var isBreak = string.Equals(segment.Item.Kind, "break", StringComparison.OrdinalIgnoreCase);
        var isLabel = string.Equals(segment.Item.Kind, "label", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment.Item.SemanticRole, "label", StringComparison.OrdinalIgnoreCase)
            || segment.Item.Title.Contains("ラベル", StringComparison.OrdinalIgnoreCase);

        return new TimelineBlock
        {
            TileId = segment.Item.TileId,
            Title = segment.Item.Title,
            StartLabel = segment.Start.ToString("HH:mm"),
            EndLabel = segment.End.ToString("HH:mm"),
            DurationLabel = durationLabel,
            Kind = segment.Item.Kind,
            IsBreak = isBreak,
            IsActive = segment.Item.IsActive,
            IsDone = !segment.Item.IsActive && segment.End <= nowLocal,
            IsLabelTile = isLabel,
            Lane = segment.Lane,
            TotalLanes = segment.TotalLanes,
            IsFullWidth = isLabel,
            Top = segment.Top,
            Height = segment.Height,
        };
    }

    private static string BuildRangeLabel(TimelineScaleUnit scaleUnit, DateTimeOffset start, DateTimeOffset end)
    {
        return scaleUnit switch
        {
            TimelineScaleUnit.Day => $"Day: {start:yyyy/MM/dd HH:mm} - {end:yyyy/MM/dd HH:mm}",
            TimelineScaleUnit.Week => $"Week: {start:yyyy/MM/dd} - {end.AddMinutes(-1):yyyy/MM/dd}",
            _ => $"Month: {start:yyyy/MM} ({start:MM/dd} - {end.AddMinutes(-1):MM/dd})",
        };
    }
}
