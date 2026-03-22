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
                _lastConnectionState = healthy;
                ConnectionStatusChanged?.Invoke(this, healthy);
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

            if (HasPromptChanged(_lastPrompt, prompt))
            {
                _lastPrompt = prompt;
                if (prompt?.Prompt != null)
                {
                    var msg = $"[Polling] PendingPrompt: id={prompt.Prompt.PromptId}, kind={prompt.Prompt.Kind}, title={prompt.Prompt.Title}, actions={string.Join("|", prompt.Prompt.Actions.Select(a => $"{a.Id}({a.Label})"))}";
                    System.Diagnostics.Debug.WriteLine(msg);
                    App.DebugLog(msg);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Polling] PendingPrompt: null");
                    App.DebugLog("[Polling] PendingPrompt: null");
                }
                PendingPromptChanged?.Invoke(this, prompt);
            }

            if (HasTimelineChanged(_lastTimeline, timeline))
            {
                _lastTimeline = timeline;
                TimelineChanged?.Invoke(this, timeline);
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

        var oldActions = string.Join(",", old.Prompt.Actions.Select(a => a.Id));
        var currentActions = string.Join(",", current.Prompt.Actions.Select(a => a.Id));
        return old.Prompt.PromptId != current.Prompt.PromptId
            || old.Prompt.Kind != current.Prompt.Kind
            || old.Prompt.Title != current.Prompt.Title
            || old.Prompt.Body != current.Prompt.Body
            || old.Prompt.Why != current.Prompt.Why
            || oldActions != currentActions
            || old.Prompt.ExpiresAt != current.Prompt.ExpiresAt
            || old.Prompt.Stale != current.Prompt.Stale;
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
        _daemonManager.Dispose();
    }
}
