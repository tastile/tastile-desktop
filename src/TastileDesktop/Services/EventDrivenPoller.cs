using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// Refreshes Core view state in response to user actions, window focus, and an
/// optional idle timer. Replaces the always-on <see cref="PollingService"/>:
/// no wall-clock tick, no daemon child process, no <c>/commands/tick</c>.
/// </summary>
public sealed class EventDrivenPoller : IDisposable
{
    private readonly CoreApiClient _api;
    private readonly DispatcherQueue _dispatcher;
    private readonly int _idleSeconds;
    private readonly DispatcherTimer _uiUpdateTimer;
    private readonly SemaphoreSlim _pollGate = new(1, 1);
    private DispatcherQueueTimer? _idleTimer;
    private CancellationTokenSource? _eventStreamCts;
    private Task? _eventStreamTask;

    private ExecutionView? _lastExecutionView;
    private TilesResponse? _lastTiles;
    private PendingPromptResponse? _lastPrompt;
    private TimelineTodayResponse? _lastTimeline;
    private bool _lastConnectionState;

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

    private string? _lastTilesHash;
    private TimelineViewportSettings _timelineViewport = new(
        ScaleUnit: TimelineScaleUnit.Day,
        RangeMode: TimelineRangeMode.Day24,
        AnchorLocal: DateTimeOffset.Now.ToLocalTime());

    public event EventHandler<ExecutionView?>? ExecutionViewChanged;
    public event EventHandler<TilesResponse?>? TilesChanged;
    public event EventHandler<PendingPromptResponse?>? PendingPromptChanged;
    public event EventHandler<TimelineTodayResponse?>? TimelineChanged;
    public event EventHandler<bool>? ConnectionStatusChanged;

    public ExecutionView? CurrentExecutionView => _lastExecutionView;
    public TilesResponse? CurrentTiles => _lastTiles;
    public PendingPromptResponse? CurrentPrompt => _lastPrompt;
    public TimelineTodayResponse? CurrentTimeline => _lastTimeline;
    public bool IsConnected => _lastConnectionState;

    public EventDrivenPoller(CoreApiClient api, DispatcherQueue dispatcher)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _idleSeconds = AppSettings.PollIdleSeconds;
        _uiUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _uiUpdateTimer.Tick += OnUIUpdateTick;
    }

    public void SetTimelineViewport(TimelineViewportSettings viewport)
    {
        _timelineViewport = viewport;
    }

    public void Start()
    {
        _uiUpdateTimer.Start();
        if (_idleSeconds > 0 && _idleTimer == null)
        {
            _idleTimer = _dispatcher.CreateTimer();
            _idleTimer.Interval = TimeSpan.FromSeconds(_idleSeconds);
            _idleTimer.Tick += OnIdleTimerTick;
            _idleTimer.Start();
        }

        if (AppSettings.EnableSse && _eventStreamCts == null)
        {
            _eventStreamCts = new CancellationTokenSource();
            _eventStreamTask = RunStateEventLoopAsync(_eventStreamCts.Token);
        }
    }

    public void Stop()
    {
        _uiUpdateTimer.Stop();
        _idleTimer?.Stop();
        _eventStreamCts?.Cancel();
    }

    public async Task StartAsync()
    {
        Start();
        await RefreshAsync(forcePublish: true);
    }

    public async Task RefreshAsync(bool forcePublish = false, bool userInitiated = true)
    {
        // Reset idle timer on user actions so we don't double-fetch.
        if (userInitiated)
        {
            _idleTimer?.Start();
        }

        if (!await _pollGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var connected = true;
            ExecutionView? executionView = null;
            TilesResponse? tiles = null;
            PendingPromptResponse? prompt = null;
            TimelineTodayResponse? timeline = null;

            try
            {
                var executionViewTask = _api.GetExecutionViewAsync();
                var tilesTask = _api.GetTilesAsync();
                var promptTask = _api.GetPendingPromptAsync();
                var timelineTask = _api.GetTimelineForViewportAsync(_timelineViewport);
                await Task.WhenAll(executionViewTask, tilesTask, promptTask, timelineTask);
                executionView = executionViewTask.Result;
                tiles = tilesTask.Result;
                prompt = promptTask.Result;
                timeline = timelineTask.Result;
            }
            catch
            {
                connected = false;
            }

            if (connected)
            {
                if (forcePublish || HasExecutionViewChanged(_lastExecutionView, executionView))
                {
                    lock (_pendingChangesLock)
                    {
                        _hasExecutionViewChange = true;
                        _pendingExecutionView = executionView;
                    }
                }

                if (forcePublish || HasTilesChanged(_lastTiles, tiles))
                {
                    lock (_pendingChangesLock)
                    {
                        _hasTilesChange = true;
                        _pendingTiles = tiles;
                    }
                }

                if (forcePublish || HasPromptChanged(_lastPrompt, prompt))
                {
                    lock (_pendingChangesLock)
                    {
                        _hasPromptChange = true;
                        _pendingPrompt = prompt;
                    }
                }

                if (forcePublish || HasTimelineChanged(_lastTimeline, timeline))
                {
                    lock (_pendingChangesLock)
                    {
                        _hasTimelineChange = true;
                        _pendingTimeline = timeline;
                    }
                }
            }

            if (connected != _lastConnectionState)
            {
                lock (_pendingChangesLock)
                {
                    _hasConnectionChange = true;
                    _pendingConnectionState = connected;
                }
            }
        }
        finally
        {
            _pollGate.Release();
        }
    }

    public void Dispose()
    {
        Stop();
        _eventStreamCts?.Dispose();
        _idleTimer = null;
        _pollGate.Dispose();
    }

    private void OnIdleTimerTick(DispatcherQueueTimer sender, object args)
    {
        _ = RefreshAsync(userInitiated: false);
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
                    System.Diagnostics.Debug.WriteLine("[UI Update] PendingPrompt: null");
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

    private bool HasTilesChanged(TilesResponse? old, TilesResponse? current)
    {
        if (current?.Tiles == null)
        {
            return _lastTilesHash != null;
        }
        var hash = TileHashResolver.Build(current);
        if (hash == _lastTilesHash) return false;
        _lastTilesHash = hash;
        return true;
    }

    private static bool HasPromptChanged(PendingPromptResponse? old, PendingPromptResponse? current)
    {
        if (old?.Prompt == null && current?.Prompt == null) return false;
        if (old?.Prompt == null || current?.Prompt == null) return true;
        return !string.Equals(old.Prompt.PromptId, current.Prompt.PromptId, StringComparison.Ordinal);
    }

    private static bool HasTimelineChanged(TimelineTodayResponse? old, TimelineTodayResponse? current)
    {
        return TimelineDiffResolver.HasTimelineChanged(old, current);
    }

    private async Task RunStateEventLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var _ in _api.StreamStateEventsAsync(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    await RefreshAsync(userInitiated: false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
