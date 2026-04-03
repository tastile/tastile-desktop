using TastileDesktop.Models;

namespace TastileDesktop.Services;

public sealed class TilesWindowLiveUpdateBridge : IDisposable
{
    private readonly ITilesChangedSource _source;
    private readonly Func<Task> _refreshAsync;
    private bool _disposed;

    public TilesWindowLiveUpdateBridge(ITilesChangedSource source, Func<Task> refreshAsync)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(refreshAsync);

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
