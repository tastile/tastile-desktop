namespace TastileDesktop.Services;

public sealed record CalendarViewportRequest(string ViewPath, DateTimeOffset AnchorLocal);

public static class CalendarViewportResolver
{
    public static CalendarViewportRequest Resolve(TimelineViewportSettings viewport, DateTimeOffset nowLocal)
    {
        var anchor = ResolveAnchor(viewport, nowLocal);
        var path = ResolvePath(viewport);
        return new CalendarViewportRequest(path, anchor);
    }

    private static string ResolvePath(TimelineViewportSettings viewport)
    {
        if (viewport.ScaleUnit == TimelineScaleUnit.Day)
        {
            return "/v1/calendar/day";
        }

        if (viewport.ScaleUnit == TimelineScaleUnit.Week)
        {
            return "/v1/calendar/week";
        }

        return viewport.RangeMode == TimelineRangeMode.Year1
            ? "/v1/calendar/year"
            : "/v1/calendar/month";
    }

    private static DateTimeOffset ResolveAnchor(TimelineViewportSettings viewport, DateTimeOffset nowLocal)
    {
        if (viewport.RangeMode == TimelineRangeMode.Custom && viewport.CustomStartLocal.HasValue)
        {
            return viewport.CustomStartLocal.Value;
        }

        var anchor = viewport.AnchorLocal;
        if (anchor == default)
        {
            return nowLocal;
        }

        return anchor;
    }
}
