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
    private readonly DaemonManager _daemonManager;
    
    private ExecutionView? _lastExecutionView;
    private TilesResponse? _lastTiles;
    private PendingPromptResponse? _lastPrompt;
    private TimelineTodayResponse? _lastTimeline;
    private bool _lastConnectionState;
    private readonly PollingHealthCoordinator _coordinator = new();
    
    // UI更新のthrottle用
    private readonly object _pendingChangesLock = new();
    private bool _hasExecutionViewChange;
    private bool _hasTilesChange;
    private bool _hasPromptChange;
    private bool _hasTimelineChange;
    private bool _hasConnectionChange;
    private ExecutionView? _pendingExecutionView;
    private TilesResponse? _pendingTiles;
    private PendingPromptResponse? _pendingPrompt;
    private TimelineTodayResponse? _pendingTimeline;
    private bool _pendingConnectionState;
    private readonly DispatcherTimer _uiUpdateTimer;
    private readonly DispatcherTimer _wallClockPollTimer;
    private CancellationTokenSource? _eventStreamCts;
    private Task? _eventStreamTask;

    /// <summary>
    /// Raised when execution state changes (work/break/idle status, main tile, etc.)
    /// This is derived from Core and should be used directly by UI without calculation.
    /// </summary>
    public event EventHandler<ExecutionView?>? ExecutionViewChanged;
    
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
    /// Current execution state derived from Core (tiles are the only truth).
    /// UI should use this directly - do not calculate IsWorking/IsOnBreak/IsIdle.
    /// </summary>
    public ExecutionView? CurrentExecutionView => _lastExecutionView;
    
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

        // UI更新は200ms間隔でthrottle（変更があった場合のみ発火）
        _uiUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _uiUpdateTimer.Tick += OnUIUpdateTick;
        _uiUpdateTimer.Start();

        _wallClockPollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _wallClockPollTimer.Tick += OnWallClockPollTick;
        _wallClockPollTimer.Start();
    }

    /// <summary>
    /// Start polling. Ensures daemon is running first.
    /// </summary>
    public async Task StartAsync()
    {
        await _daemonManager.EnsureRunningAsync();
        await PollAsync();
        _eventStreamCts = new CancellationTokenSource();
        _eventStreamTask = RunStateEventLoopAsync(_eventStreamCts.Token);
    }

    /// <summary>
    /// Stop polling.
    /// </summary>
    public void Stop()
    {
        _eventStreamCts?.Cancel();
        _uiUpdateTimer.Stop();
        _wallClockPollTimer.Stop();
    }

    private void OnUIUpdateTick(object? sender, object e)
    {
        lock (_pendingChangesLock)
        {
            if (_hasConnectionChange)
            {
                _hasConnectionChange = false;
                _lastConnectionState = _pendingConnectionState;
                var msg = $"[UI Update] Connection: {_pendingConnectionState}";
                System.Diagnostics.Debug.WriteLine(msg);
                App.DebugLog(msg);
                ConnectionStatusChanged?.Invoke(this, _pendingConnectionState);
            }

            if (_hasExecutionViewChange)
            {
                _hasExecutionViewChange = false;
                _lastExecutionView = _pendingExecutionView;
                var msg = $"[UI Update] ExecutionView: isWorking={_pendingExecutionView?.IsWorking}, isOnBreak={_pendingExecutionView?.IsOnBreak}, isIdle={_pendingExecutionView?.IsIdle}, mainTile={_pendingExecutionView?.MainTile?.Title}, mainTileId={_pendingExecutionView?.MainTile?.Id}";
                System.Diagnostics.Debug.WriteLine(msg);
                App.DebugLog(msg);
                ExecutionViewChanged?.Invoke(this, _pendingExecutionView);
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

            // Fetch data in parallel (no more tick-based auto processing)
            var executionViewTask = _api.GetExecutionViewAsync();
            var tilesTask = _api.GetTilesAsync();
            var promptTask = _api.GetPendingPromptAsync();
            var timelineTask = _api.GetTodayTimelineAsync();
            await Task.WhenAll(executionViewTask, tilesTask, promptTask, timelineTask);

            var executionView = executionViewTask.Result;
            var tiles = tilesTask.Result;
            var prompt = promptTask.Result;
            var timeline = timelineTask.Result;

            // Check for changes and mark pending (UI更新は別スレッドで throttle)
            // ExecutionView comes from Core - UI should use it directly without calculation
            if (executionView != null && HasExecutionViewChanged(_lastExecutionView, executionView))
            {
                lock (_pendingChangesLock)
                {
                    _hasExecutionViewChange = true;
                    _pendingExecutionView = executionView;
                }
            }

            if (tiles != null && HasTilesChanged(_lastTiles, tiles))
            {
                lock (_pendingChangesLock)
                {
                    _hasTilesChange = true;
                    _pendingTiles = tiles;
                }
            }

            if (prompt != null && HasPromptChanged(_lastPrompt, prompt))
            {
                lock (_pendingChangesLock)
                {
                    _hasPromptChange = true;
                    _pendingPrompt = prompt;
                }
            }

            if (timeline != null && HasTimelineChanged(_lastTimeline, timeline))
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

    private static bool HasExecutionViewChanged(ExecutionView? old, ExecutionView? current)
    {
        if (old == null && current == null) return false;
        if (old == null || current == null) return true;
        
        return old.IsWorking != current.IsWorking
            || old.IsOnBreak != current.IsOnBreak
            || old.IsIdle != current.IsIdle
            || (old.MainTile?.Id != current.MainTile?.Id)
            || (old.MainTile?.Title != current.MainTile?.Title)
            || old.MainTileStartedAt != current.MainTileStartedAt
            || old.MainTileEndsAt != current.MainTileEndsAt
            || old.PendingPromptId != current.PendingPromptId;
    }

    private string? _lastTilesHash;

    private bool HasTilesChanged(TilesResponse? old, TilesResponse? current)
    {
        if (current?.Tiles == null) return _lastTilesHash != null;
        var hash = TileHashResolver.Build(current);
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
        _eventStreamCts?.Cancel();
        _uiUpdateTimer.Stop();
        _wallClockPollTimer.Stop();
        _daemonManager.Dispose();
    }

    private void OnWallClockPollTick(object? sender, object e)
    {
        _ = PollAsync();
    }

    private async Task RunStateEventLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var _ in _api.StreamStateEventsAsync(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    await PollAsync();
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
