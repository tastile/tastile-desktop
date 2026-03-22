using Microsoft.UI.Xaml;
using TastileDesktop.Models;
using System.Linq;
using System.Threading;

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
    private PendingPromptResponse? _lastPrompt;
    private TimelineTodayResponse? _lastTimeline;
    private bool _lastConnectionState;
    private readonly PollingHealthCoordinator _coordinator = new();
    
    // UI更新のthrottle用
    private readonly object _pendingChangesLock = new();
    private bool _hasActiveTileChange;
    private bool _hasTilesChange;
    private bool _hasPromptChange;
    private bool _hasTimelineChange;
    private bool _hasConnectionChange;
    private ActiveTileResponse? _pendingActiveTile;
    private TilesResponse? _pendingTiles;
    private PendingPromptResponse? _pendingPrompt;
    private TimelineTodayResponse? _pendingTimeline;
    private bool _pendingConnectionState;
    private readonly DispatcherTimer _uiUpdateTimer;

    /// <summary>
/// Raised when the active tile changes (new tile, phase change, etc.)
    /// </summary>
    public event EventHandler<ActiveTileResponse?>? ActiveTileChanged;
    
    /// <summary>
    /// Raised when the tiles list changes.
    /// </summary>
    public event EventHandler<TilesResponse?>? TilesChanged;

    /// <summary>
    /// Raised when the current pending prompt view changes.
    /// </summary>
    public event EventHandler<PendingPromptResponse?>? PendingPromptChanged;

    /// <summary>
    /// Raised when the today timeline view changes.
    /// </summary>
    public event EventHandler<TimelineTodayResponse?>? TimelineChanged;
    
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

    public PendingPromptResponse? CurrentPrompt => _lastPrompt;

    public TimelineTodayResponse? CurrentTimeline => _lastTimeline;
    
    /// <summary>
    /// Whether the daemon is currently connected.
    /// </summary>
    public bool IsConnected => _lastConnectionState;

    public PollingService(CoreApiClient api, DaemonManager daemonManager)
    {
        _api = api;
        _daemonManager = daemonManager;
        // ポーリングは2秒間隔で状態検出のみ
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await PollAsync();
        
        // UI更新は200ms間隔でthrottle（変更があった場合のみ発火）
        _uiUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _uiUpdateTimer.Tick += OnUIUpdateTick;
        _uiUpdateTimer.Start();
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
    public void Stop()
    {
        _timer.Stop();
        _uiUpdateTimer.Stop();
    }

    private void OnUIUpdateTick(object? sender, object e)
    {
        lock (_pendingChangesLock)
        {
            if (_hasConnectionChange)
            {
                _hasConnectionChange = false;
                _lastConnectionState = _pendingConnectionState;
                System.Diagnostics.Debug.WriteLine($"[UI Update] Connection: {_pendingConnectionState}");
                ConnectionStatusChanged?.Invoke(this, _pendingConnectionState);
            }

            if (_hasActiveTileChange)
            {
                _hasActiveTileChange = false;
                _lastActiveTile = _pendingActiveTile;
                System.Diagnostics.Debug.WriteLine($"[UI Update] ActiveTile: phase={_pendingActiveTile?.Phase}, tile={_pendingActiveTile?.Tile?.Title}");
                ActiveTileChanged?.Invoke(this, _pendingActiveTile);
            }

            if (_hasTilesChange)
            {
                _hasTilesChange = false;
                _lastTiles = _pendingTiles;
                System.Diagnostics.Debug.WriteLine($"[UI Update] Tiles: count={_pendingTiles?.Tiles?.Count}");
                TilesChanged?.Invoke(this, _pendingTiles);
            }

            if (_hasPromptChange)
            {
                _hasPromptChange = false;
                _lastPrompt = _pendingPrompt;
                if (_pendingPrompt?.Prompt != null)
                {
                    var msg = $"[UI Update] PendingPrompt: id={_pendingPrompt.Prompt.PromptId}, kind={_pendingPrompt.Prompt.Kind}, title={_pendingPrompt.Prompt.Title}";
                    System.Diagnostics.Debug.WriteLine(msg);
                    App.DebugLog(msg);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[UI Update] PendingPrompt: null");
                    App.DebugLog("[UI Update] PendingPrompt: null");
                }
                PendingPromptChanged?.Invoke(this, _pendingPrompt);
            }

            if (_hasTimelineChange)
            {
                _hasTimelineChange = false;
                _lastTimeline = _pendingTimeline;
                System.Diagnostics.Debug.WriteLine($"[UI Update] Timeline: items={_pendingTimeline?.Items?.Count}");
                TimelineChanged?.Invoke(this, _pendingTimeline);
            }
        }
    }

    /// <summary>
    /// Force an immediate poll.
    /// </summary>
    public async Task PollAsync()
    {
        if (!_coordinator.TryBeginPoll())
        {
            return;
        }

        try
        {
            var healthy = await _api.CheckHealthAsync();
            if (!healthy && _coordinator.TryBeginRecovery(DateTimeOffset.UtcNow))
            {
                try
                {
                    await _daemonManager.EnsureRunningAsync();
                    healthy = await _api.CheckHealthAsync();
                }
                catch
                {
                    healthy = false;
                }
            }
            
            // Connection status changed
            if (healthy != _lastConnectionState)
            {
                lock (_pendingChangesLock)
                {
                    _hasConnectionChange = true;
                    _pendingConnectionState = healthy;
                }
            }

            if (!healthy)
            {
                return;
            }

            try
            {
                await _api.TriggerSyncAsync();
            }
            catch
            {
                // Keep serving local state even if background sync trigger fails.
            }

            // Fetch data in parallel
            var activeTask = _api.GetActiveTileAsync();
            var tilesTask = _api.GetTilesAsync();
            var promptTask = _api.GetPendingPromptAsync();
            var timelineTask = _api.GetTodayTimelineAsync();
            await Task.WhenAll(activeTask, tilesTask, promptTask, timelineTask);

            var active = activeTask.Result;
            var tiles = tilesTask.Result;
            var prompt = promptTask.Result;
            var timeline = timelineTask.Result;

            // Check for changes and mark pending (UI更新は別スレッドで throttle)
            if (HasActiveTileChanged(_lastActiveTile, active))
            {
                lock (_pendingChangesLock)
                {
                    _hasActiveTileChange = true;
                    _pendingActiveTile = active;
                }
            }

            if (HasTilesChanged(_lastTiles, tiles))
            {
                lock (_pendingChangesLock)
                {
                    _hasTilesChange = true;
                    _pendingTiles = tiles;
                }
            }

            if (HasPromptChanged(_lastPrompt, prompt))
            {
                lock (_pendingChangesLock)
                {
                    _hasPromptChange = true;
                    _pendingPrompt = prompt;
                }
            }

            if (HasTimelineChanged(_lastTimeline, timeline))
            {
                lock (_pendingChangesLock)
                {
                    _hasTimelineChange = true;
                    _pendingTimeline = timeline;
                }
            }
        }
        catch
        {
            // On error, mark as disconnected
            if (_lastConnectionState)
            {
                lock (_pendingChangesLock)
                {
                    _hasConnectionChange = true;
                    _pendingConnectionState = false;
                }
            }
        }
        finally
        {
            _coordinator.EndPoll();
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
        var hash = string.Join(",", current.Tiles.Select(t => $"{t.Id}:{t.Lifecycle}:{t.ResumeNote}:{t.WorkedMinutes}:{t.BreakMinutes}"));
        if (hash == _lastTilesHash) return false;
        _lastTilesHash = hash;
        return true;
    }

    private static bool HasPromptChanged(PendingPromptResponse? old, PendingPromptResponse? current)
    {
        if (old?.Prompt == null && current?.Prompt == null) return false;
        if (old?.Prompt == null || current?.Prompt == null) return true;

        // PromptId のみで判定（同じプロンプトなら Stale や他のフィールドが変わっても再描画しない）
        return old.Prompt.PromptId != current.Prompt.PromptId;
    }

    private static bool HasTimelineChanged(TimelineTodayResponse? old, TimelineTodayResponse? current)
    {
        var oldHash = old == null
            ? null
            : string.Join(",", old.Items.Select(i => $"{i.Kind}:{i.TileId}:{i.StartedAt}:{i.EndedAt}:{i.IsActive}:{i.DurationMin}"));
        var currentHash = current == null
            ? null
            : string.Join(",", current.Items.Select(i => $"{i.Kind}:{i.TileId}:{i.StartedAt}:{i.EndedAt}:{i.IsActive}:{i.DurationMin}"));
        return oldHash != currentHash;
    }

    public void Dispose()
    {
        _timer.Stop();
        _uiUpdateTimer.Stop();
        _daemonManager.Dispose();
    }
}
