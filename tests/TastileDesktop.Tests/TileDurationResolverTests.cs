using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class TileDurationResolverTests
{
    [Fact]
    public void Resolve_UsesBreakDuration_ForBreakTiles()
    {
        var text = TileDurationResolver.Resolve("break", 30, 5);
        Assert.Equal("5m", text);
    }

    [Fact]
    public void Resolve_UsesWorkDuration_ForWorkTiles()
    {
        var text = TileDurationResolver.Resolve("work", 30, 5);
        Assert.Equal("30m", text);
    }
}
