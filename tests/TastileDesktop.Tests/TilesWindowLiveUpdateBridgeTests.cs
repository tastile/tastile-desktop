using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class TilesWindowLiveUpdateBridgeTests
{
    private sealed class FakePollerSource
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

    private sealed class TestableTilesWindowLiveUpdateBridge : IDisposable
    {
        private readonly FakePollerSource _source;
        private readonly Func<Task> _refreshAsync;
        private bool _disposed;

        public TestableTilesWindowLiveUpdateBridge(FakePollerSource source, Func<Task> refreshAsync)
        {
            _source = source;
            _refreshAsync = refreshAsync;
            _source.TilesChanged += OnTilesChanged;
        }

        private void OnTilesChanged(object? sender, TilesResponse? e)
        {
            _ = _refreshAsync();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _source.TilesChanged -= OnTilesChanged;
        }
    }

    [Fact]
    public void Bridge_SubscribesAndUnsubscribes_FromTilesChangedSource()
    {
        var source = new FakePollerSource();
        var bridge = new TestableTilesWindowLiveUpdateBridge(source, () => Task.CompletedTask);

        Assert.Equal(1, source.SubscriberCount);

        bridge.Dispose();

        Assert.Equal(0, source.SubscriberCount);
    }

    [Fact]
    public async Task Bridge_InvokesRefreshCallback_WhenTilesChangedIsRaised()
    {
        var source = new FakePollerSource();
        var called = 0;
        var bridge = new TestableTilesWindowLiveUpdateBridge(source, () =>
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
