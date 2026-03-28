using TastileDesktop.Models;
using TastileDesktop.Views;

namespace TastileDesktop.Services;

/// <summary>
/// Detects when user intervention is needed based on pending prompt state.
/// </summary>
public class InterventionEngine : IDisposable
{
    private readonly PollingService _pollingService;
    private readonly CoreApiClient _api;
    private InterventionWindow? _interventionWindow;

    public InterventionEngine(PollingService pollingService, CoreApiClient api, SettingsService? settingsService = null)
    {
        _pollingService = pollingService;
        _api = api;
        _pollingService.PendingPromptChanged += OnPendingPromptChanged;
    }

    private void OnPendingPromptChanged(object? sender, PendingPromptResponse? prompt)
    {
        var decision = PromptNotificationPolicy.Decide(prompt?.Prompt, isFullscreen: false);
        if (!decision.ShowIntervention || prompt?.Prompt == null)
        {
            return;
        }

        ShowIntervention(prompt.Prompt);
    }

    private void ShowIntervention(PromptView prompt)
    {
        if (_interventionWindow != null)
        {
            return;
        }

        var kind = Normalize(prompt.Kind);
        switch (kind)
        {
            case "break_end":
                ShowBreakIntervention();
                break;
            case "start":
                ShowIdleIntervention();
                break;
            default:
                ShowWorkIntervention(prompt);
                break;
        }
    }

    private void ShowWorkIntervention(PromptView prompt)
    {
        if (_interventionWindow != null) return;

        var executionView = _pollingService.CurrentExecutionView;
        var window = new InterventionWindow(
            InterventionType.Work,
            executionView?.MainTile?.Title ?? prompt.Title,
            executionView?.MainTileStartedAt,
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
                    var tileId = _interventionWindow?.SelectedTileId;
                    if (!string.IsNullOrEmpty(tileId))
                    {
                        await _api.StartTileAsync(tileId);
                    }
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
        _pollingService.PendingPromptChanged -= OnPendingPromptChanged;
        _interventionWindow?.Close();
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
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
