using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class TimelineStatusActionResolverTests
{
    [Fact]
    public void Resolve_DoneBlock_ReturnsIgnore()
    {
        var decision = TimelineStatusActionResolver.Resolve("tile-1", "done");

        Assert.Equal(TimelineStatusActionKind.Ignore, decision.Kind);
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("started")]
    public void Resolve_ReadyOrStarted_ReturnsPromptRequest(string lifecycle)
    {
        var decision = TimelineStatusActionResolver.Resolve("tile-2", lifecycle);

        Assert.Equal(TimelineStatusActionKind.RequestPrompt, decision.Kind);
        Assert.Equal("tile-2", decision.TileId);
    }

    [Fact]
    public void Resolve_MissingTileId_ReturnsIgnore()
    {
        var decision = TimelineStatusActionResolver.Resolve("", "ready");

        Assert.Equal(TimelineStatusActionKind.Ignore, decision.Kind);
    }
}
