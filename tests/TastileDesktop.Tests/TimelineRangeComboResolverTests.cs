using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class TimelineRangeComboResolverTests
{
    [Fact]
    public void ResolvePlan_DoesNotRebuild_WhenConfiguredModesAlreadyMatchScale()
    {
        var plan = TimelineRangeComboResolver.ResolvePlan(
            TimelineScaleUnit.Day,
            TimelineRangeMode.AroundNow24,
            [
                TimelineRangeMode.Day24,
                TimelineRangeMode.AroundNow24,
                TimelineRangeMode.SunriseToSunset,
                TimelineRangeMode.Custom,
            ]);

        Assert.False(plan.ShouldRebuildOptions);
        Assert.Equal(1, plan.SelectedIndex);
        Assert.Equal(TimelineRangeMode.AroundNow24, plan.Options[plan.SelectedIndex].Mode);
    }

    [Fact]
    public void ResolvePlan_Rebuilds_WhenConfiguredModesDoNotMatchScale()
    {
        var plan = TimelineRangeComboResolver.ResolvePlan(
            TimelineScaleUnit.Week,
            TimelineRangeMode.Week2,
            [
                TimelineRangeMode.Day24,
                TimelineRangeMode.AroundNow24,
                TimelineRangeMode.SunriseToSunset,
                TimelineRangeMode.Custom,
            ]);

        Assert.True(plan.ShouldRebuildOptions);
        Assert.Collection(
            plan.Options,
            option => Assert.Equal(TimelineRangeMode.Week1, option.Mode),
            option => Assert.Equal(TimelineRangeMode.Week2, option.Mode),
            option => Assert.Equal(TimelineRangeMode.Week4, option.Mode));
        Assert.Equal(1, plan.SelectedIndex);
    }

    [Fact]
    public void ResolvePlan_FallsBackToFirstOption_WhenRangeModeIsUnsupportedForScale()
    {
        var plan = TimelineRangeComboResolver.ResolvePlan(
            TimelineScaleUnit.Month,
            TimelineRangeMode.SunriseToSunset,
            []);

        Assert.True(plan.ShouldRebuildOptions);
        Assert.Equal(0, plan.SelectedIndex);
        Assert.Equal(TimelineRangeMode.Month1, plan.Options[0].Mode);
    }
}
