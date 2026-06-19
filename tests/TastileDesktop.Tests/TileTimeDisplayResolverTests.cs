using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class TileTimeDisplayResolverTests
{
    [Fact]
    public void ResolveNextStartLabel_ReturnsExpectedLocalFormattedTime()
    {
        var utc = new DateTimeOffset(2026, 4, 2, 16, 0, 0, TimeSpan.Zero);
        var expected = utc.ToLocalTime().ToString("MM/dd HH:mm");

        var label = TileTimeDisplayResolver.ResolveNextStartLabel(utc.ToString("O"), null);

        Assert.Equal(expected, label);
    }

    [Fact]
    public void ResolveNextStartLabel_ReturnsNull_ForInvalidInput()
    {
        var label = TileTimeDisplayResolver.ResolveNextStartLabel("not-a-date", null);
        Assert.Null(label);
    }

    [Fact]
    public void ResolveScheduledTimeDisplay_PrefersFixedStart_AndFormatsLocalTime()
    {
        var fixedStartUtc = new DateTimeOffset(2026, 4, 2, 16, 0, 0, TimeSpan.Zero);

        var display = TileTimeDisplayResolver.ResolveScheduledTimeDisplay(
            fixedStartUtc.ToString("O"),
            null,
            null,
            null);

        Assert.Equal($"scheduled {fixedStartUtc.ToLocalTime():HH:mm}", display);
    }

    [Fact]
    public void ResolveScheduledTimeDisplay_UsesProjectedStart_WhenNoFixedOrActiveStart()
    {
        var projectedUtc = new DateTimeOffset(2026, 4, 2, 18, 30, 0, TimeSpan.Zero);

        var display = TileTimeDisplayResolver.ResolveScheduledTimeDisplay(
            null,
            null,
            projectedUtc.ToString("O"),
            null);

        Assert.Equal($"scheduled {projectedUtc.ToLocalTime():HH:mm}", display);
    }
}
