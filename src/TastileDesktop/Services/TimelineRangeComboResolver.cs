namespace TastileDesktop.Services;

public sealed record TimelineRangeOption(TimelineRangeMode Mode, string Label);

public sealed record TimelineRangeComboPlan(
    IReadOnlyList<TimelineRangeOption> Options,
    int SelectedIndex,
    bool ShouldRebuildOptions);

public static class TimelineRangeComboResolver
{
    public static TimelineRangeComboPlan ResolvePlan(
        TimelineScaleUnit scaleUnit,
        TimelineRangeMode selectedMode,
        IReadOnlyList<TimelineRangeMode>? configuredModes)
    {
        var options = ResolveOptions(scaleUnit);
        var desiredModes = options.Select(option => option.Mode).ToArray();
        var existingModes = configuredModes ?? [];
        var shouldRebuild = existingModes.Count != desiredModes.Length
            || existingModes.Where((mode, index) => mode != desiredModes[index]).Any();

        var selectedIndex = 0;
        for (var i = 0; i < options.Count; i++)
        {
            if (options[i].Mode == selectedMode)
            {
                selectedIndex = i;
                break;
            }
        }

        return new TimelineRangeComboPlan(options, selectedIndex, shouldRebuild);
    }

    public static IReadOnlyList<TimelineRangeOption> ResolveOptions(TimelineScaleUnit scaleUnit)
    {
        return scaleUnit switch
        {
            TimelineScaleUnit.Day =>
            [
                new TimelineRangeOption(TimelineRangeMode.Day24, "24h"),
                new TimelineRangeOption(TimelineRangeMode.AroundNow24, "±12h"),
                new TimelineRangeOption(TimelineRangeMode.SunriseToSunset, "Sun"),
                new TimelineRangeOption(TimelineRangeMode.Custom, "Custom"),
            ],
            TimelineScaleUnit.Week =>
            [
                new TimelineRangeOption(TimelineRangeMode.Week1, "1w"),
                new TimelineRangeOption(TimelineRangeMode.Week2, "2w"),
                new TimelineRangeOption(TimelineRangeMode.Week4, "4w"),
            ],
            _ =>
            [
                new TimelineRangeOption(TimelineRangeMode.Month1, "1m"),
                new TimelineRangeOption(TimelineRangeMode.Month3, "3m"),
                new TimelineRangeOption(TimelineRangeMode.Month6, "6m"),
                new TimelineRangeOption(TimelineRangeMode.Year1, "1y"),
            ],
        };
    }
}
