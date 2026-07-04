using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class CalendarViewportResolverTests
{
    [Fact]
    public void Resolve_MapsDayScaleToDayViewPath()
    {
        var viewport = new TimelineViewportSettings(
            ScaleUnit: TimelineScaleUnit.Day,
            RangeMode: TimelineRangeMode.Day24,
            AnchorLocal: new DateTimeOffset(2026, 4, 8, 9, 0, 0, TimeSpan.FromHours(9)));

        var request = CalendarViewportResolver.Resolve(viewport, viewport.AnchorLocal);

        Assert.Equal("/v1/calendar/day", request.ViewPath);
        Assert.Equal(viewport.AnchorLocal, request.AnchorLocal);
    }

    [Fact]
    public void Resolve_MapsYearRangeToYearViewPath()
    {
        var viewport = new TimelineViewportSettings(
            ScaleUnit: TimelineScaleUnit.Month,
            RangeMode: TimelineRangeMode.Year1,
            AnchorLocal: new DateTimeOffset(2026, 4, 8, 9, 0, 0, TimeSpan.FromHours(9)));

        var request = CalendarViewportResolver.Resolve(viewport, viewport.AnchorLocal);

        Assert.Equal("/v1/calendar/year", request.ViewPath);
    }

    [Fact]
    public void Resolve_UsesCustomStartAsAnchor_WhenRangeIsCustom()
    {
        var anchor = new DateTimeOffset(2026, 4, 8, 9, 0, 0, TimeSpan.FromHours(9));
        var customStart = new DateTimeOffset(2026, 4, 9, 6, 0, 0, TimeSpan.FromHours(9));
        var viewport = new TimelineViewportSettings(
            ScaleUnit: TimelineScaleUnit.Day,
            RangeMode: TimelineRangeMode.Custom,
            AnchorLocal: anchor,
            CustomStartLocal: customStart,
            CustomEndLocal: customStart.AddHours(2));

        var request = CalendarViewportResolver.Resolve(viewport, DateTimeOffset.Now);

        Assert.Equal("/v1/calendar/day", request.ViewPath);
        Assert.Equal(customStart, request.AnchorLocal);
    }
}
