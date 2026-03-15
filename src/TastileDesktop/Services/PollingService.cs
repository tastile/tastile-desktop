using Microsoft.UI.Xaml;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// Polls the daemon API and raises events when state changes.
/// </summary>
public class PollingService : IDisposable
{
    private readonly CoreApiClient _api;
    private readonly DispatcherTimer _timer;
    private readonly DaemonManager _daemonManager;
    
    private ActiveTileResponse? _lastActiveTile;
    private TilesResponse? _lastTiles;
    private bool _lastConnectionState;

    /// <summary>
/// Raised when the active tile changes (new tile, phase change, etc.)
    /// </summary>
    public event EventHandler<ActiveTileResponse?>? ActiveTileChanged;
    
    /// <summary>
    /// Raised when the tiles list changes.
    /// </summary>
    public event EventHandler<TilesResponse?>? TilesChanged;
    
    /// <summary>
    /// Raised when connection status changes.
    /// </summary>
    public event EventHandler<bool>? ConnectionStatusChanged;

    /// <summary>
    /// Current cached active tile.
    /// </summary>
    public ActiveTileResponse? CurrentActiveTile => _lastActiveTile;
    
    /// <summary>
    /// Current cached tiles.
    /// </summary>
    public TilesResponse? CurrentTiles => _lastTiles;
    
    /// <summary>
    /// Whether the daemon is currently connected.
    /// </summary>
    public bool IsConnected => _lastConnectionState;

    public PollingService(CoreApiClient api, DaemonManager daemonManager)
    {
        _api = api;
        _daemonManager = daemonManager;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    /// <summary>
    /// Start polling. Ensures daemon is running first.
    /// </summary>
    public async Task StartAsync()
    {
        await _daemonManager.EnsureRunningAsync();
        _timer.Start();
        await PollAsync(); // Initial poll
    }

    /// <summary>
    /// Stop polling.
    /// </summary>
    public void Stop() => _timer.Stop();

    /// <summary>
    /// Force an immediate poll.
    /// </summary>
    public async Task PollAsync()
    {
        try
        {
            var healthy = await _api.CheckHealthAsync();
            
            // Connection status changed
            if (healthy != _lastConnectionState)
            {
                _lastConnectionState = healthy;
                ConnectionStatusChanged?.Invoke(this, healthy);
            }

            if (!healthy)
            {
                return;
            }

            // Fetch data in parallel
            var activeTask = _api.GetActiveTileAsync();
            var tilesTask = _api.GetTilesAsync();
            await Task.WhenAll(activeTask, tilesTask);

            var active = activeTask.Result;
            var tiles = tilesTask.Result;

            // Check for changes and raise events
            if (HasActiveTileChanged(_lastActiveTile, active))
            {
                _lastActiveTile = active;
                ActiveTileChanged?.Invoke(this, active);
            }

            if (HasTilesChanged(_lastTiles, tiles))
            {
                _lastTiles = tiles;
                TilesChanged?.Invoke(this, tiles);
            }
        }
        catch
        {
            // On error, mark as disconnected
            if (_lastConnectionState)
            {
                _lastConnectionState = false;
                ConnectionStatusChanged?.Invoke(this, false);
            }
        }
    }

    private static bool HasActiveTileChanged(ActiveTileResponse? old, ActiveTileResponse? current)
    {
        if (old == null && current == null) return false;
        if (old == null || current == null) return true;
        
        return old.Phase != current.Phase
            || old.PhaseStartedAt != current.PhaseStartedAt
            || old.PhaseEndsAt != current.PhaseEndsAt
            || (old.Tile?.Id != current.Tile?.Id)
            || (old.Tile?.Title != current.Tile?.Title);
    }

    private string? _lastTilesHash;

    private bool HasTilesChanged(TilesResponse? old, TilesResponse? current)
    {
        if (current?.Tiles == null) return _lastTilesHash != null;
        var hash = string.Join(",", current.Tiles.Select(t => $"{t.Id}:{t.Lifecycle}"));
        if (hash == _lastTilesHash) return false;
        _lastTilesHash = hash;
        return true;
    }

    public void Dispose()
    {
        _timer.Stop();
        _daemonManager.Dispose();
    }
}
