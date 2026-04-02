using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class CountdownTextResolverTests
{
    [Fact]
    public void Resolve_UsesNextActionableStart_WhenNoActiveExecutionEndsAt()
    {
        var now = new DateTimeOffset(2026, 4, 2, 5, 0, 0, TimeSpan.Zero);
        var nextStart = now.AddMinutes(1).AddSeconds(5);

        var text = CountdownTextResolver.Resolve(null, nextStart, now);

        Assert.Equal("01:05", text);
    }

    [Fact]
    public void Resolve_PrioritizesExecutionEndsAt_OverNextActionableStart()
    {
        var now = new DateTimeOffset(2026, 4, 2, 5, 0, 0, TimeSpan.Zero);
        var mainEnds = now.AddMinutes(2).ToString("O");
        var nextStart = now.AddMinutes(20);

        var text = CountdownTextResolver.Resolve(mainEnds, nextStart, now);

        Assert.Equal("02:00", text);
    }

    [Fact]
    public void Format_ReturnsZeroWhenExpired()
    {
        var text = CountdownTextResolver.Format(TimeSpan.FromSeconds(-1));
        Assert.Equal("00:00", text);
    }
}
