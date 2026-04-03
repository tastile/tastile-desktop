using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class TilesWindowLiveUpdateBridgeTests
{
    private sealed class FakeTilesChangedSource : ITilesChangedSource
    {
        public event EventHandler<TilesResponse?>? TilesChanged;

        public int SubscriberCount => TilesChanged?.GetInvocationList().Length ?? 0;

        public void Raise()
        {
            TilesChanged?.Invoke(
                this,
                new TilesResponse(
                    Tiles: [],
                    NextActionableTileId: null,
                    NextActionableStartAt: null));
        }
    }

    [Fact]
    public void Bridge_SubscribesAndUnsubscribes_FromTilesChangedSource()
    {
        var source = new FakeTilesChangedSource();
        var bridge = new TilesWindowLiveUpdateBridge(source, () => Task.CompletedTask);

        Assert.Equal(1, source.SubscriberCount);

        bridge.Dispose();

        Assert.Equal(0, source.SubscriberCount);
    }

    [Fact]
    public async Task Bridge_InvokesRefreshCallback_WhenTilesChangedIsRaised()
    {
        var source = new FakeTilesChangedSource();
        var called = 0;
        var bridge = new TilesWindowLiveUpdateBridge(source, () =>
        {
            Interlocked.Increment(ref called);
            return Task.CompletedTask;
        });

        source.Raise();
        await Task.Delay(20);

        bridge.Dispose();
        Assert.Equal(1, called);
    }
}
