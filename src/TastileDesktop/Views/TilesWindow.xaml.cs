using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TastileDesktop.Services;
using System.Collections.ObjectModel;
using TastileDesktop.Models;
using TastileDesktop.ViewModels;

namespace TastileDesktop.Views;

public sealed partial class TilesWindow : Window
{
    private readonly CoreApiClient _api = new();
    private readonly PromptToastDisplayService _promptToast = PromptToastDisplayService.Instance;
    public ObservableCollection<TileListItem> ReadyTiles { get; } = new();
    public ObservableCollection<TileListItem> StartedTiles { get; } = new();
    public ObservableCollection<TileListItem> DoneTiles { get; } = new();

    private string _viewMode = "by_state";
    private string _searchQuery = string.Empty;
    private int _readyLimit = 4;
    private int _startedLimit = 4;
    private int _doneLimit = 4;

    private int _readyTotal = 0;
    private int _startedTotal = 0;
    private int _doneTotal = 0;

    public TilesWindow()
    {
        InitializeComponent();
        RootGrid.DataContext = this;
        FloatingWindowHelper.Configure(this, TitleBarArea, 720, 760);
        _ = RefreshTilesAsync();
    }

    private async Task RefreshTilesAsync()
    {
        try
        {
            var searchParam = string.IsNullOrWhiteSpace(_searchQuery) ? null : _searchQuery;

            var readyTask = _api.GetTilesAsync(_viewMode, "ready", _readyLimit, searchParam);
            var startedTask = _api.GetTilesAsync(_viewMode, "started", _startedLimit, searchParam);
            var doneTask = _api.GetTilesAsync(_viewMode, "done", _doneLimit, searchParam);

            var readyCountTask = _api.GetTilesAsync(_viewMode, "ready", 10000, searchParam);
            var startedCountTask = _api.GetTilesAsync(_viewMode, "started", 10000, searchParam);
            var doneCountTask = _api.GetTilesAsync(_viewMode, "done", 10000, searchParam);

            await Task.WhenAll(readyTask, startedTask, doneTask, readyCountTask, startedCountTask, doneCountTask);

            ReadyTiles.Clear();
            if (readyTask.Result?.Tiles != null)
            {
                foreach (var tile in readyTask.Result.Tiles)
                    ReadyTiles.Add(ToTileListItem(tile));
            }

            StartedTiles.Clear();
            if (startedTask.Result?.Tiles != null)
            {
                foreach (var tile in startedTask.Result.Tiles)
                    StartedTiles.Add(ToTileListItem(tile));
            }

            DoneTiles.Clear();
            if (doneTask.Result?.Tiles != null)
            {
                foreach (var tile in doneTask.Result.Tiles)
                    DoneTiles.Add(ToTileListItem(tile));
            }

            _readyTotal = readyCountTask.Result?.Tiles?.Count ?? 0;
            _startedTotal = startedCountTask.Result?.Tiles?.Count ?? 0;
            _doneTotal = doneCountTask.Result?.Tiles?.Count ?? 0;

            ReadyCount.Text = _readyTotal.ToString();
            StartedCount.Text = _startedTotal.ToString();
            DoneCount.Text = _doneTotal.ToString();

            ReadyMore.Text = _readyTotal > _readyLimit ? $"他{_readyTotal - _readyLimit}件 ▼" : "";
            StartedMore.Text = _startedTotal > _startedLimit ? $"他{_startedTotal - _startedLimit}件 ▼" : "";
            DoneMore.Text = _doneTotal > _doneLimit ? $"他{_doneTotal - _doneLimit}件 ▼" : "";

            ReadySection.Visibility = _viewMode == "by_state" ? Visibility.Visible : Visibility.Collapsed;
            StartedSection.Visibility = _viewMode == "by_state" ? Visibility.Visible : Visibility.Collapsed;
            DoneSection.Visibility = _viewMode == "by_state" ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TilesWindow] RefreshTilesAsync error: {ex.Message}");
        }
    }

    private static TileListItem ToTileListItem(TileView tv)
    {
        var recFromTemporal = tv.Recurrence;
        var recFromObjective = tv.Objective?.Recurrence;
        var effectiveRecurrence = recFromTemporal ?? recFromObjective;

        var recurrenceSettings = effectiveRecurrence != null
            ? $"every {effectiveRecurrence.StepMin}min ({effectiveRecurrence.WindowStartMin}-{effectiveRecurrence.WindowEndMin})"
            : null;

        return new TileListItem
        {
            Id = tv.Id,
            Title = tv.Title,
            Lifecycle = tv.Lifecycle,
            WorkedMinutes = tv.WorkedMinutes,
            NextAction = tv.NextAction,
            DoneDefinition = tv.DoneDefinition,
            TargetWorkMin = tv.TargetWorkMin,
            TargetRestMin = tv.TargetRestMin,
            DoneRule = tv.DoneRule,
            ObjectiveMode = tv.ObjectiveMode,
            ProgressPercent = tv.TargetWorkMin.HasValue && tv.TargetWorkMin.Value > 0
                ? Math.Clamp((double)tv.WorkedMinutes / tv.TargetWorkMin.Value * 100d, 0d, 100d)
                : 0d,
            FixedStart = tv.Temporal?.FixedStart,
            ActiveStart = tv.Temporal?.ActiveStart,
            FixedEnd = tv.Temporal?.FixedEnd,
            ActiveEnd = tv.Temporal?.ActiveEnd,
            ReleaseAt = tv.Temporal?.ReleaseAt,
            DueAt = tv.Temporal?.DueAt,
            InterruptPenalty = tv.Interruption?.InterruptPenalty ?? 0,
            ResumePenalty = tv.Interruption?.ResumePenalty ?? 0,
            BreakSplitsWork = tv.Interruption?.BreakSplitsWork ?? false,
            ExternalInterruptOnly = tv.Interruption?.ExternalInterruptOnly ?? false,
            AutoStart = tv.Automation?.AutoStart ?? false,
            AutoComplete = tv.Automation?.AutoComplete ?? false,
            SemanticRole = tv.SemanticRole,
            Labels = tv.Labels,
            RecurrenceSettings = recurrenceSettings,
            RecurrenceFromObjective = recFromObjective,
            RecurrenceStepMin = effectiveRecurrence?.StepMin,
            RecurrenceWindowStartMin = effectiveRecurrence?.WindowStartMin,
            RecurrenceWindowEndMin = effectiveRecurrence?.WindowEndMin,
            RecurrenceExpression = effectiveRecurrence?.Expression,
        };
    }

    private void OnViewByStateClick(object sender, RoutedEventArgs e)
    {
        _viewMode = "by_state";
        _ = RefreshTilesAsync();
    }

    private void OnViewByGroupClick(object sender, RoutedEventArgs e)
    {
        _viewMode = "by_group";
        _ = RefreshTilesAsync();
    }

    private void OnViewByProjectClick(object sender, RoutedEventArgs e)
    {
        _viewMode = "by_project";
        _ = RefreshTilesAsync();
    }

    private void OnViewByTagClick(object sender, RoutedEventArgs e)
    {
        _viewMode = "by_tag";
        _ = RefreshTilesAsync();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            _searchQuery = textBox.Text;
            _ = RefreshTilesAsync();
        }
    }

    private void OnReadyHeaderClick(object sender, RoutedEventArgs e)
    {
        _readyLimit = _readyLimit >= 64 ? 4 : _readyLimit * 4;
        _ = RefreshTilesAsync();
    }

    private void OnStartedHeaderClick(object sender, RoutedEventArgs e)
    {
        _startedLimit = _startedLimit >= 64 ? 4 : _startedLimit * 4;
        _ = RefreshTilesAsync();
    }

    private void OnDoneHeaderClick(object sender, RoutedEventArgs e)
    {
        _doneLimit = _doneLimit >= 64 ? 4 : _doneLimit * 4;
        _ = RefreshTilesAsync();
    }

    private async void OnTileStatusClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tileId || string.IsNullOrWhiteSpace(tileId)) return;

        var allTiles = ReadyTiles.Concat(StartedTiles).Concat(DoneTiles);
        var tile = allTiles.FirstOrDefault(t => t.Id == tileId);
        if (tile == null) return;

        var lifecycle = tile.Lifecycle?.Trim().ToLowerInvariant();
        if (lifecycle == "ready" || lifecycle == "started")
        {
            await RequestPromptForTileAsync(tileId);
        }
        else if (lifecycle == "done")
        {
            var api = new CoreApiClient();
            await api.StartTileAsync(tileId);
            await RefreshTilesAsync();
        }
    }

    private async Task RequestPromptForTileAsync(string tileId)
    {
        try
        {
            var response = await _api.RequestPromptAsync(tileId);
            if (response?.Ok == true && response.Prompt != null)
            {
                _promptToast.ShowPrompt(
                    response.Prompt,
                    5,
                    async (actionId, stopAt) =>
                    {
                        _promptToast.Hide();
                        var api = new CoreApiClient();
                        switch (actionId.ToUpperInvariant())
                        {
                            case "START":
                            case "START_TILE":
                                await api.StartTileAsync(tileId);
                                break;
                            case "COMPLETE":
                            case "COMPLETE_AND_START_NEXT":
                                await api.CompleteTileAsync(tileId);
                                break;
                            case "CONFIRM_CONTINUE":
                            case "CONFIRM_STOP_AT":
                            case "CONFIRM_EXECUTED":
                            case "CONFIRM_SKIPPED":
                            case "DISMISS":
                                if (!string.IsNullOrWhiteSpace(response.Prompt.PromptId) &&
                                    !string.IsNullOrWhiteSpace(response.Prompt.TileId))
                                {
                                    await api.RespondStartupRecoveryPromptAsync(
                                        response.Prompt.PromptId,
                                        response.Prompt.TileId!,
                                        actionId.ToUpperInvariant(),
                                        stopAt);
                                }
                                break;
                        }
                        await RefreshTilesAsync();
                    });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TilesWindow] RequestPromptForTileAsync error: {ex.Message}");
        }
    }

    private async void OnTileEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tileId) return;

        var freshTile = await _api.GetTileByIdAsync(tileId);
        if (freshTile == null) return;

        var tileListItem = ToTileListItem(freshTile);
        var createWindow = new CreateTileWindow(tileId, tileListItem);
        createWindow.Activate();
    }
}
