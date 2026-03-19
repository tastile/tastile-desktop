using TastileDesktop.Models;
using TastileDesktop.Views;

namespace TastileDesktop.Services;

/// <summary>
/// Detects when user intervention is needed based on active tile state.
/// </summary>
public class InterventionEngine : IDisposable
{
    private readonly PollingService _pollingService;
    private readonly CoreApiClient _api;
    private readonly NotificationService _notificationService;
    private readonly SettingsService _settingsService;
    private InterventionWindow? _interventionWindow;
    
    // Timing thresholds (loaded from settings)
    private TimeSpan _toastNotifyThreshold => TimeSpan.FromMinutes(_settingsService.Current.ToastNotifyMinutes);
    private TimeSpan _workInterventionThreshold => TimeSpan.FromMinutes(_settingsService.Current.InterventionMinutes);
    private TimeSpan _breakOverdueThreshold => TimeSpan.FromMinutes(1); // Grace period after break ends
    private TimeSpan _idlePromptThreshold => TimeSpan.FromMinutes(_settingsService.Current.IdlePromptMinutes);
    private TimeSpan _interventionRepeatInterval => TimeSpan.FromMinutes(_settingsService.Current.InterventionRepeatMinutes);
    
    // Track last intervention times to prevent spam
    private DateTimeOffset _lastWorkIntervention = DateTimeOffset.MinValue;
    private DateTimeOffset _lastBreakIntervention = DateTimeOffset.MinValue;
    private DateTimeOffset _lastIdleIntervention = DateTimeOffset.MinValue;
    private DateTimeOffset? _lastToastShown;
    private bool _toastShownForCurrentPhase = false;
    
    // Track last completion time for idle detection
    private DateTimeOffset _lastCompletionTime = DateTimeOffset.UtcNow;
    private string _lastPhase = "Idle";

    public InterventionEngine(PollingService pollingService, CoreApiClient api, SettingsService? settingsService = null)
    {
        _pollingService = pollingService;
        _api = api;
        _settingsService = settingsService ?? new SettingsService();
        _notificationService = new NotificationService(api);
        _pollingService.ActiveTileChanged += OnActiveTileChanged;
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Settings are automatically reloaded via _settingsService.Current
        // Next polling cycle will use new values
    }

    private void OnActiveTileChanged(object? sender, ActiveTileResponse? active)
    {
        if (active == null) return;

        var now = DateTimeOffset.UtcNow;
        var phase = active.Phase ?? "Idle";

        // Detect completion (transition from Work to Idle)
        if (_lastPhase == "Work" && phase == "Idle")
        {
            _lastCompletionTime = now;
        }
        
        // Reset toast flag when phase changes
        if (_lastPhase != phase)
        {
            _toastShownForCurrentPhase = false;
        }
        
        _lastPhase = phase;

        switch (phase)
        {
            case "Work":
                CheckWorkIntervention(active, now);
                break;
            case "Break":
                CheckBreakIntervention(active, now);
                break;
            case "Idle":
                CheckIdleIntervention(active, now);
                break;
        }
    }

    private void CheckWorkIntervention(ActiveTileResponse active, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(active.PhaseStartedAt)) return;
        if (!DateTimeOffset.TryParse(active.PhaseStartedAt, out var startedAt)) return;

        var elapsed = now - startedAt;
        
        // Phase 1: Show toast at 15 minutes
        if (elapsed >= _toastNotifyThreshold && !_toastShownForCurrentPhase)
        {
            _toastShownForCurrentPhase = true;
            _lastToastShown = now;
            _notificationService.ShowWorkReminder(active.Tile?.Title ?? "Unknown", (int)elapsed.TotalMinutes);
        }
        
        // Phase 2: Show unavoidable intervention at 25 minutes
        if (elapsed >= _workInterventionThreshold && 
            now - _lastWorkIntervention >= _interventionRepeatInterval)
        {
            _lastWorkIntervention = now;
            ShowWorkIntervention(active);
        }
    }

    private void CheckBreakIntervention(ActiveTileResponse active, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(active.PhaseEndsAt)) return;
        if (!DateTimeOffset.TryParse(active.PhaseEndsAt, out var endsAt)) return;

        // Break ended - show toast
        if (now > endsAt && !_toastShownForCurrentPhase)
        {
            _toastShownForCurrentPhase = true;
            _notificationService.ShowBreakOverReminder();
        }

        // Break is overdue (1 minute grace period) - show intervention
        if (now > endsAt + _breakOverdueThreshold &&
            now - _lastBreakIntervention >= _interventionRepeatInterval)
        {
            _lastBreakIntervention = now;
            ShowBreakIntervention();
        }
    }

    private void CheckIdleIntervention(ActiveTileResponse active, DateTimeOffset now)
    {
        var idleTime = now - _lastCompletionTime;
        
        // Phase 1: Show toast at 5 minutes
        if (idleTime >= _idlePromptThreshold && !_toastShownForCurrentPhase)
        {
            _toastShownForCurrentPhase = true;
            _notificationService.ShowIdleReminder();
        }
        
        // Phase 2: Show intervention at 10 minutes (5 + 5)
        if (idleTime >= _idlePromptThreshold + TimeSpan.FromMinutes(5) &&
            now - _lastIdleIntervention >= _interventionRepeatInterval)
        {
            _lastIdleIntervention = now;
            ShowIdleIntervention();
        }
    }

    private void ShowWorkIntervention(ActiveTileResponse active)
    {
        if (_interventionWindow != null) return; // Already showing

        var window = new InterventionWindow(
            InterventionType.Work,
            active.Tile?.Title ?? "Unknown",
            active.PhaseStartedAt,
            _pollingService.CurrentTiles);
        
        window.ActionTaken += async (sender, action) =>
        {
            await HandleInterventionAction(action);
            _interventionWindow = null;
        };

        _interventionWindow = window;
        window.Activate();
    }

    private void ShowBreakIntervention()
    {
        if (_interventionWindow != null) return;

        var window = new InterventionWindow(
            InterventionType.BreakOver,
            null,
            null,
            _pollingService.CurrentTiles);
        
        window.ActionTaken += async (sender, action) =>
        {
            await HandleInterventionAction(action);
            _interventionWindow = null;
        };

        _interventionWindow = window;
        window.Activate();
    }

    private void ShowIdleIntervention()
    {
        if (_interventionWindow != null) return;

        var window = new InterventionWindow(
            InterventionType.Idle,
            null,
            null,
            _pollingService.CurrentTiles);
        
        window.ActionTaken += async (sender, action) =>
        {
            await HandleInterventionAction(action);
            _interventionWindow = null;
        };

        _interventionWindow = window;
        window.Activate();
    }

    private async Task HandleInterventionAction(InterventionAction action)
    {
        try
        {
            switch (action)
            {
                case InterventionAction.Continue:
                    // Extend the current work session
                    await _api.ExtendTileAsync(25);
                    break;
                case InterventionAction.TakeBreak:
                    await _api.StartBreakAsync(5);
                    break;
                case InterventionAction.Complete:
                    await _api.CompleteTileAsync();
                    break;
                case InterventionAction.EndBreak:
                    await _api.EndBreakAsync();
                    break;
                case InterventionAction.StartTile:
                    // Start the selected tile
                    var tileId = _interventionWindow?.SelectedTileId;
                    if (!string.IsNullOrEmpty(tileId))
                        await _api.StartTileAsync(tileId);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Intervention action failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _pollingService.ActiveTileChanged -= OnActiveTileChanged;
        _notificationService?.Dispose();
        _interventionWindow?.Close();
    }
}

/// <summary>
/// Types of intervention that can be triggered.
/// </summary>
public enum InterventionType
{
    Work,
    BreakOver,
    Idle,
}

/// <summary>
/// Actions that can be taken from an intervention dialog.
/// </summary>
public enum InterventionAction
{
    Continue,
    TakeBreak,
    Complete,
    EndBreak,
    StartTile,
}


