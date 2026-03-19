using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using TastileDesktop.Models;
using TastileDesktop.Services;
using System.Collections.Generic;
using System.Linq;

namespace TastileDesktop.ViewModels;

/// <summary>
/// ViewModel for timeline segment display.
/// </summary>
public sealed class TimelineSegmentViewModel : ObservableObject
{
    public string TimeText { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;
    public SolidColorBrush BadgeColor { get; set; } = new(Microsoft.UI.Colors.Gray);
}

public sealed class PromptActionButtonViewModel : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Bindable item for tile list display.
/// </summary>
public sealed class TileListItem : ObservableObject
{
    private string _id = string.Empty;
    private string _title = string.Empty;
    private string _lifecycle = string.Empty;
    private long _workedMinutes;
    private string? _nextAction;

    public string Id 
    { 
        get => _id; 
        set => SetProperty(ref _id, value); 
    }
    
    public string Title 
    { 
        get => _title; 
        set => SetProperty(ref _title, value); 
    }
    
    public string Lifecycle 
    { 
        get => _lifecycle; 
        set => SetProperty(ref _lifecycle, value); 
    }
    
    public long WorkedMinutes 
    { 
        get => _workedMinutes; 
        set => SetProperty(ref _workedMinutes, value); 
    }

    public string? NextAction
    {
        get => _nextAction;
        set => SetProperty(ref _nextAction, value);
    }

    public string WorkedText => WorkedMinutes > 0 ? $"{WorkedMinutes}m" : "";

    public bool IsStartEnabled => string.Equals(Lifecycle, "Ready", StringComparison.OrdinalIgnoreCase);
    public bool IsCompleteEnabled => string.Equals(Lifecycle, "Started", StringComparison.OrdinalIgnoreCase);
    public bool IsDeferEnabled => !string.Equals(Lifecycle, "Done", StringComparison.OrdinalIgnoreCase);

    public SolidColorBrush BadgeBackground => Lifecycle.Trim().ToLowerInvariant() switch
    {
        "ready" => new SolidColorBrush(ColorHelper.FromArgb(255, 0, 120, 212)),
        "started" => new SolidColorBrush(ColorHelper.FromArgb(255, 16, 124, 16)),
        "done" => new SolidColorBrush(ColorHelper.FromArgb(255, 96, 96, 96)),
        "closed" => new SolidColorBrush(ColorHelper.FromArgb(255, 96, 96, 96)),
        _ => new SolidColorBrush(ColorHelper.FromArgb(255, 128, 128, 128)),
    };

    public SolidColorBrush BadgeForeground => new(Colors.White);

    // Fix 3: NextAction display properties
    public string NextActionText => !string.IsNullOrEmpty(NextAction) ? $"→ {NextAction}" : "";
    public Visibility HasNextAction =>
        !string.IsNullOrEmpty(NextAction) ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>
/// ViewModel for the main window.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly CoreApiClient _api;
    
    public CoreApiClient ApiClient => _api;
    private readonly PollingService _pollingService;
    private List<TileListItem> _allTiles = new();

    [ObservableProperty]
    private ObservableCollection<TileListItem> _tiles = new();

    // Fix 2: Filter
    [ObservableProperty]
    private string _selectedFilter = "All";

    public bool IsFilterAll
    {
        get => SelectedFilter == "All";
        set { if (value) SelectedFilter = "All"; }
    }
    public bool IsFilterReady
    {
        get => SelectedFilter == "Ready";
        set { if (value) SelectedFilter = "Ready"; }
    }
    public bool IsFilterStarted
    {
        get => SelectedFilter == "Started";
        set { if (value) SelectedFilter = "Started"; }
    }
    public bool IsFilterDone
    {
        get => SelectedFilter == "Done";
        set { if (value) SelectedFilter = "Done"; }
    }

    partial void OnSelectedFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterReady));
        OnPropertyChanged(nameof(IsFilterStarted));
        OnPropertyChanged(nameof(IsFilterDone));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var source = _allTiles;
        if (SelectedFilter != "All")
            source = source.Where(t => string.Equals(t.Lifecycle, SelectedFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        Tiles.Clear();
        foreach (var tile in source)
            Tiles.Add(tile);
    }

    [ObservableProperty]
    private ActiveTileResponse? _activeTile;

    [ObservableProperty]
    private PendingPromptResponse? _pendingPrompt;

    [ObservableProperty]
    private bool _isConnected;

    public Visibility ConnectedIndicatorVisibility => IsConnected ? Visibility.Visible : Visibility.Collapsed;
    
    public Visibility DisconnectedIndicatorVisibility => IsConnected ? Visibility.Collapsed : Visibility.Visible;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _newTileTitle = string.Empty;

    [ObservableProperty]
    private string _newTileNextAction = string.Empty;

    [ObservableProperty]
    private string _newTileDoneDefinition = string.Empty;

    [ObservableProperty]
    private string _memoText = string.Empty;

    // Timeline
    [ObservableProperty]
    private ObservableCollection<TimelineSegmentViewModel> _timelineSegments = new();

    [ObservableProperty]
    private ObservableCollection<PromptActionButtonViewModel> _promptActions = new();

    public bool HasNoTimelineSegments => TimelineSegments.Count == 0;
    public bool IsTilesEmpty => Tiles.Count == 0;
    public Visibility TilesEmptyVisibility => IsTilesEmpty ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TilesListVisibility => IsTilesEmpty ? Visibility.Collapsed : Visibility.Visible;
    public int TotalCount => _allTiles.Count;
    public int ReadyCount => _allTiles.Count(t => t.Lifecycle.Equals("Ready", StringComparison.OrdinalIgnoreCase));
    public int StartedCount => _allTiles.Count(t => t.Lifecycle.Equals("Started", StringComparison.OrdinalIgnoreCase));
    public int DoneCount => _allTiles.Count(t => t.Lifecycle.Equals("Done", StringComparison.OrdinalIgnoreCase));
    public Visibility IdleVisibility => IsIdle ? Visibility.Visible : Visibility.Collapsed;
    public Visibility WorkingVisibility => IsWorking ? Visibility.Visible : Visibility.Collapsed;
    public Visibility BreakVisibility => IsOnBreak ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PromptVisibility => PendingPrompt?.Prompt != null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PromptEmptyVisibility => PendingPrompt?.Prompt != null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility HasNextAction =>
        !string.IsNullOrEmpty(ActiveTileNextAction) ? Visibility.Visible : Visibility.Collapsed;

    public string PendingPromptTitle => PendingPrompt?.Prompt?.Title ?? "No pending prompt";
    public string PendingPromptBody => PendingPrompt?.Prompt?.Body ?? "Core has not requested a response.";
    public string PendingPromptWhy => PendingPrompt?.Prompt?.Why ?? string.Empty;
    public string MemoPlaceholder => ActiveTile?.Tile != null
        ? "Attach memo to active tile..."
        : "Send a free memo to core...";

    public TileListItem? NextUpTile =>
        _allTiles.FirstOrDefault(t => t.Lifecycle.Equals("Ready", StringComparison.OrdinalIgnoreCase))
        ?? _allTiles.FirstOrDefault(t => t.Lifecycle.Equals("Started", StringComparison.OrdinalIgnoreCase));

    public string NextUpTitle => NextUpTile?.Title ?? "No suggested tile";
    public string NextUpAction => NextUpTile?.NextAction ?? "Create a tile or adjust its schedule to surface the next actionable tile.";
    public string NextUpWorkedText => string.IsNullOrWhiteSpace(NextUpTile?.WorkedText) ? "Ready" : NextUpTile!.WorkedText;
    public string? NextUpTileId => NextUpTile?.Id;
    public Visibility NextUpVisibility => NextUpTile is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility NextUpEmptyVisibility => NextUpTile is null ? Visibility.Visible : Visibility.Collapsed;

    public string IdleGuidanceText
    {
        get
        {
            if (_allTiles.Count == 0)
                return "Create your first tile above to get started.";
            var ready = _allTiles.Count(t =>
                t.Lifecycle.Equals("Ready", StringComparison.OrdinalIgnoreCase));
            if (ready > 0)
                return $"{ready} tile(s) ready — click one below to start.";
            return "All tiles done. Create a new one!";
        }
    }

    // Computed properties for UI state
    public bool IsIdle => ActiveTile?.Tile == null || 
                          string.Equals(ActiveTile.Phase, "Idle", StringComparison.OrdinalIgnoreCase);
    
    public bool IsWorking => ActiveTile?.Tile != null && 
                             string.Equals(ActiveTile.Phase, "Work", StringComparison.OrdinalIgnoreCase);
    
    public bool IsOnBreak => ActiveTile?.Tile != null && 
                             string.Equals(ActiveTile.Phase, "Break", StringComparison.OrdinalIgnoreCase);

    public string? ActiveTileTitle => ActiveTile?.Tile?.Title;
    
    public string? ActiveTileNextAction => ActiveTile?.Tile?.NextAction;

    public string WorkElapsedText
    {
        get
        {
            if (ActiveTile?.PhaseStartedAt == null) return "0:00";
            if (!DateTimeOffset.TryParse(ActiveTile.PhaseStartedAt, out var startedAt)) return "0:00";
            
            var elapsed = DateTimeOffset.UtcNow - startedAt;
            return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
        }
    }

    public string BreakRemainingText
    {
        get
        {
            if (ActiveTile?.PhaseEndsAt == null) return "On break";
            if (!DateTimeOffset.TryParse(ActiveTile.PhaseEndsAt, out var endsAt)) return "On break";
            
            var remaining = endsAt - DateTimeOffset.UtcNow;
            return remaining.TotalSeconds > 0 
                ? $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2} remaining"
                : "Break ended";
        }
    }

    private InterventionEngine? _interventionEngine;

    public MainViewModel()
    {
        _api = new CoreApiClient();
        _pollingService = new PollingService(_api, new DaemonManager());
        
        // Subscribe to polling events
        _pollingService.ActiveTileChanged += OnActiveTileChanged;
        _pollingService.TilesChanged += OnTilesChanged;
        _pollingService.PendingPromptChanged += OnPendingPromptChanged;
        _pollingService.TimelineChanged += OnTimelineChanged;
        _pollingService.ConnectionStatusChanged += OnConnectionStatusChanged;

        // Initialize intervention engine
        _interventionEngine = new InterventionEngine(_pollingService, _api);
    }

    public async Task InitializeAsync()
    {
        await _pollingService.StartAsync();
    }

    private void OnActiveTileChanged(object? sender, ActiveTileResponse? active)
    {
        ActiveTile = active;
        
        // Notify all computed properties changed
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsWorking));
        OnPropertyChanged(nameof(IsOnBreak));
        OnPropertyChanged(nameof(IdleVisibility));
        OnPropertyChanged(nameof(WorkingVisibility));
        OnPropertyChanged(nameof(BreakVisibility));
        OnPropertyChanged(nameof(ActiveTileTitle));
        OnPropertyChanged(nameof(ActiveTileNextAction));
        OnPropertyChanged(nameof(WorkElapsedText));
        OnPropertyChanged(nameof(BreakRemainingText));
        OnPropertyChanged(nameof(MemoPlaceholder));
    }

    private void OnPendingPromptChanged(object? sender, PendingPromptResponse? prompt)
    {
        PendingPrompt = prompt;
        PromptActions = new ObservableCollection<PromptActionButtonViewModel>(
            prompt?.Prompt?.Actions.Select(action => new PromptActionButtonViewModel
            {
                Id = action.Id,
                Label = action.Label,
            }) ?? Enumerable.Empty<PromptActionButtonViewModel>());

        OnPropertyChanged(nameof(PromptVisibility));
        OnPropertyChanged(nameof(PromptEmptyVisibility));
        OnPropertyChanged(nameof(PendingPromptTitle));
        OnPropertyChanged(nameof(PendingPromptBody));
        OnPropertyChanged(nameof(PendingPromptWhy));
    }

    private void OnTimelineChanged(object? sender, TimelineTodayResponse? timeline)
    {
        var segments = new ObservableCollection<TimelineSegmentViewModel>(
            timeline?.Items.Select(item =>
            {
                var startedAt = DateTimeOffset.TryParse(item.StartedAt, out var parsed)
                    ? parsed.ToLocalTime()
                    : DateTimeOffset.Now;

                return new TimelineSegmentViewModel
                {
                    TimeText = startedAt.ToString("HH:mm"),
                    Title = item.Title,
                    DurationText = item.IsActive ? $"{Math.Max(item.DurationMin, 0)}m ongoing" : $"{Math.Max(item.DurationMin, 0)}m",
                    BadgeColor = item.Kind.Equals("break", StringComparison.OrdinalIgnoreCase)
                        ? new SolidColorBrush(ColorHelper.FromArgb(255, 16, 124, 16))
                        : new SolidColorBrush(ColorHelper.FromArgb(255, 0, 120, 212)),
                };
            }) ?? Enumerable.Empty<TimelineSegmentViewModel>());

        TimelineSegments = segments;
        OnPropertyChanged(nameof(TimelineSegments));
        OnPropertyChanged(nameof(HasNoTimelineSegments));
    }

    private void OnTilesChanged(object? sender, TilesResponse? tiles)
    {
        if (tiles?.Tiles == null) return;

        _allTiles = tiles.Tiles.Select(t => new TileListItem
        {
            Id = t.Id,
            Title = t.Title,
            Lifecycle = NormalizeLifecycle(t.Lifecycle),
            WorkedMinutes = t.WorkedMinutes,
            NextAction = t.NextAction,
        })
        .OrderBy(t => LifecycleSortKey(t.Lifecycle))
        .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
        .ToList();

        ApplyFilter();
        OnPropertyChanged(nameof(IsTilesEmpty));
        OnPropertyChanged(nameof(TilesEmptyVisibility));
        OnPropertyChanged(nameof(TilesListVisibility));
        OnPropertyChanged(nameof(IdleGuidanceText));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ReadyCount));
        OnPropertyChanged(nameof(StartedCount));
        OnPropertyChanged(nameof(DoneCount));
        OnPropertyChanged(nameof(NextUpTile));
        OnPropertyChanged(nameof(NextUpTitle));
        OnPropertyChanged(nameof(NextUpAction));
        OnPropertyChanged(nameof(NextUpWorkedText));
        OnPropertyChanged(nameof(NextUpTileId));
        OnPropertyChanged(nameof(NextUpVisibility));
        OnPropertyChanged(nameof(NextUpEmptyVisibility));
    }

    private void OnConnectionStatusChanged(object? sender, bool connected)
    {
        IsConnected = connected;
        OnPropertyChanged(nameof(ConnectedIndicatorVisibility));
        OnPropertyChanged(nameof(DisconnectedIndicatorVisibility));
        StatusMessage = connected ? "Connected" : "Daemon offline";
    }

    [RelayCommand]
    private async Task CreateTileAsync()
    {
        var title = NewTileTitle?.Trim();
        if (string.IsNullOrEmpty(title)) return;

        try
        {
            var nextAction = string.IsNullOrWhiteSpace(NewTileNextAction) ? null : NewTileNextAction.Trim();
            var doneDef = string.IsNullOrWhiteSpace(NewTileDoneDefinition) ? null : NewTileDoneDefinition.Trim();
            
            var result = await _api.CreateTileAsync(title, nextAction, doneDef);
            if (result != null && !result.Ok)
                StatusMessage = $"Error: {result.Error}";
            else
            {
                StatusMessage = $"Created: {title}";
                NewTileTitle = string.Empty;
                NewTileNextAction = string.Empty;
                NewTileDoneDefinition = string.Empty;
            }
            await _pollingService.PollAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    public async Task<bool> SubmitTileAsync(string title, string? nextAction, string? doneDefinition)
    {
        NewTileTitle = title;
        NewTileNextAction = nextAction ?? string.Empty;
        NewTileDoneDefinition = doneDefinition ?? string.Empty;

        try
        {
            await CreateTileAsync();
            return !StatusMessage.StartsWith("Error:", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            OnPropertyChanged(nameof(NextUpTile));
            OnPropertyChanged(nameof(NextUpTitle));
            OnPropertyChanged(nameof(NextUpAction));
            OnPropertyChanged(nameof(NextUpWorkedText));
            OnPropertyChanged(nameof(NextUpTileId));
            OnPropertyChanged(nameof(NextUpVisibility));
            OnPropertyChanged(nameof(NextUpEmptyVisibility));
        }
    }

    public Task RefreshAsync() => _pollingService.PollAsync();

    [RelayCommand]
    private async Task CompleteTileAsync()
    {
        try
        {
            var result = await _api.CompleteTileAsync();
            if (result != null && !result.Ok)
                StatusMessage = $"Error: {result.Error}";
            else
                StatusMessage = "Tile completed";
            await _pollingService.PollAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StartBreakAsync()
    {
        try
        {
            var result = await _api.StartBreakAsync(5);
            if (result != null && !result.Ok)
                StatusMessage = $"Error: {result.Error}";
            else
                StatusMessage = "Break started (5 min)";
            await _pollingService.PollAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task EndBreakAsync()
    {
        try
        {
            var result = await _api.EndBreakAsync();
            if (result != null && !result.Ok)
                StatusMessage = $"Error: {result.Error}";
            else
                StatusMessage = "Break ended";
            await _pollingService.PollAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StartTileAsync(string? tileId)
    {
        if (string.IsNullOrEmpty(tileId)) return;

        try
        {
            var result = await _api.StartTileAsync(tileId);
            if (result != null && !result.Ok)
                StatusMessage = $"Error: {result.Error}";
            else
            {
                var tile = Tiles.FirstOrDefault(t => t.Id == tileId);
                StatusMessage = $"Started: {tile?.Title ?? tileId}";
            }
            await _pollingService.PollAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SendMemoAsync()
    {
        var text = MemoText?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        try
        {
            var result = await _api.AttachMemoAsync(ActiveTile?.Tile?.Id, text);
            if (result != null && !result.Ok)
                StatusMessage = $"Error: {result.Error}";
            else
            {
                StatusMessage = ActiveTile?.Tile != null ? "Memo attached to active tile" : "Global memo sent";
                MemoText = string.Empty;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    public async Task RespondToPromptAsync(string? actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId) || PendingPrompt?.Prompt == null)
            return;

        try
        {
            CommandResponse? result = actionId switch
            {
                "START" when !string.IsNullOrWhiteSpace(PendingPrompt.Prompt.TileId)
                    => await _api.StartTileAsync(PendingPrompt.Prompt.TileId),
                "DEFER" when !string.IsNullOrWhiteSpace(PendingPrompt.Prompt.TileId)
                    => await _api.DeferTileAsync(PendingPrompt.Prompt.TileId),
                "COMPLETE_AND_START_NEXT" => await _api.CompleteTileAsync(),
                "EXTEND" => await _api.ExtendTileAsync(10),
                "END_BREAK" => await _api.EndBreakAsync(),
                _ => null,
            };

            if (result != null && !result.Ok)
            {
                StatusMessage = $"Error: {result.Error}";
            }
            else
            {
                StatusMessage = $"Prompt action: {actionId}";
            }

            await _pollingService.PollAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    // Fix 1: DeferTile command
    [RelayCommand]
    private async Task DeferTileAsync(string? tileId)
    {
        if (string.IsNullOrEmpty(tileId)) return;

        try
        {
            var result = await _api.DeferTileAsync(tileId);
            if (result != null && !result.Ok)
                StatusMessage = $"Error: {result.Error}";
            else
            {
                var tile = Tiles.FirstOrDefault(t => t.Id == tileId);
                StatusMessage = $"Deferred: {tile?.Title ?? tileId}";
            }
            await _pollingService.PollAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    // Fix 1: DeleteTile command
    [RelayCommand]
    private async Task DeleteTileAsync(string? tileId)
    {
        if (string.IsNullOrEmpty(tileId)) return;

        try
        {
            var result = await _api.DeleteTileAsync(tileId);
            if (result != null && !result.Ok)
                StatusMessage = $"Error: {result.Error}";
            else
                StatusMessage = "Tile deleted";
            await _pollingService.PollAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    public void Dispose()
    {
        _interventionEngine?.Dispose();
        _pollingService.Dispose();
    }

    private static string NormalizeLifecycle(string lifecycle) =>
        lifecycle.Trim().ToLowerInvariant() switch
        {
            "ready" => "Ready",
            "started" => "Started",
            "done" => "Done",
            "closed" => "Done",
            _ => lifecycle,
        };

    private static int LifecycleSortKey(string lifecycle) =>
        NormalizeLifecycle(lifecycle) switch
        {
            "Started" => 0,
            "Ready" => 1,
            "Done" => 2,
            _ => 3,
        };
}
