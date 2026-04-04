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

public sealed class TimelineHourMarkerViewModel : ObservableObject
{
    public string Label { get; set; } = string.Empty;
    public double Top { get; set; }
}

public sealed class TimelineAbsoluteBlockViewModel : ObservableObject
{
    public string? TileId { get; set; }
    public string Lifecycle { get; set; } = "scheduled";
    public string StatusIconGlyph { get; set; } = "\uE739";
    public string StatusIconToolTip { get; set; } = "scheduled";
    public string KindLabel { get; set; } = "task";
    public string Title { get; set; } = string.Empty;
    public string TimeRangeText { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;
    public int Lane { get; set; }
    public int TotalLanes { get; set; } = 1;
    public bool IsFullWidth { get; set; }
    public double Left { get; set; }
    public double Width { get; set; }
    public double Top { get; set; }
    public double Height { get; set; }
    public SolidColorBrush Fill { get; set; } = new(Microsoft.UI.Colors.Transparent);
    public SolidColorBrush BorderBrush { get; set; } = new(Microsoft.UI.Colors.Transparent);
    public SolidColorBrush ForegroundBrush { get; set; } = new(Microsoft.UI.Colors.White);
    public SolidColorBrush SecondaryForegroundBrush { get; set; } = new(Microsoft.UI.Colors.White);
    public SolidColorBrush StatusFill { get; set; } = new(Microsoft.UI.Colors.Transparent);
    public SolidColorBrush StatusBorderBrush { get; set; } = new(Microsoft.UI.Colors.Transparent);
    public SolidColorBrush StatusForegroundBrush { get; set; } = new(Microsoft.UI.Colors.White);
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
    private int? _targetWorkMin;
    private double _progressPercent;
    private string? _nextStartLabel;
    private string? _projectedNextStartAt;
    private string? _fixedStart;
    private string? _activeStart;
    private string? _fixedEnd;
    private string? _activeEnd;
    private string? _releaseAt;
    private string? _dueAt;
    private int? _targetRestMin;
    private string? _doneRule;
    private string? _doneDefinition;
    private int _interruptPenalty;
    private int _resumePenalty;
    private bool _breakSplitsWork;
    private bool _externalInterruptOnly;
    private bool _autoStart;
    private bool _autoComplete;
    private string? _semanticRole;
    private List<string>? _labels;

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

    public int? TargetWorkMin
    {
        get => _targetWorkMin;
        set => SetProperty(ref _targetWorkMin, value);
    }

    public string WorkedText => WorkedMinutes > 0 ? $"{WorkedMinutes}m" : "";
    public string TargetDurationText
    {
        get => TileDurationResolver.Resolve(SemanticRole, TargetWorkMin, TargetRestMin);
    }
    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }
    public string? NextStartLabel
    {
        get => _nextStartLabel;
        set
        {
            if (SetProperty(ref _nextStartLabel, value))
            {
                OnPropertyChanged(nameof(NextStartDisplay));
            }
        }
    }
    public string? ProjectedNextStartAt
    {
        get => _projectedNextStartAt;
        set => SetProperty(ref _projectedNextStartAt, value);
    }
    public string NextStartDisplay => string.IsNullOrWhiteSpace(NextStartLabel) ? "unscheduled" : NextStartLabel;
    
    public string? FixedStart
    {
        get => _fixedStart;
        set => SetProperty(ref _fixedStart, value);
    }

    public string? ActiveStart
    {
        get => _activeStart;
        set => SetProperty(ref _activeStart, value);
    }

    public string? FixedEnd
    {
        get => _fixedEnd;
        set => SetProperty(ref _fixedEnd, value);
    }

    public string? ActiveEnd
    {
        get => _activeEnd;
        set => SetProperty(ref _activeEnd, value);
    }

    public string? ReleaseAt
    {
        get => _releaseAt;
        set => SetProperty(ref _releaseAt, value);
    }

    public string? DueAt
    {
        get => _dueAt;
        set => SetProperty(ref _dueAt, value);
    }

    public int? TargetRestMin
    {
        get => _targetRestMin;
        set => SetProperty(ref _targetRestMin, value);
    }

    public string? DoneRule
    {
        get => _doneRule;
        set => SetProperty(ref _doneRule, value);
    }

    public string? ObjectiveMode { get; set; }

    public string? DoneDefinition
    {
        get => _doneDefinition;
        set => SetProperty(ref _doneDefinition, value);
    }

    public int InterruptPenalty
    {
        get => _interruptPenalty;
        set => SetProperty(ref _interruptPenalty, value);
    }

    public int ResumePenalty
    {
        get => _resumePenalty;
        set => SetProperty(ref _resumePenalty, value);
    }

    public bool BreakSplitsWork
    {
        get => _breakSplitsWork;
        set => SetProperty(ref _breakSplitsWork, value);
    }

    public bool ExternalInterruptOnly
    {
        get => _externalInterruptOnly;
        set => SetProperty(ref _externalInterruptOnly, value);
    }

    public bool AutoStart
    {
        get => _autoStart;
        set => SetProperty(ref _autoStart, value);
    }

    public bool AutoComplete
    {
        get => _autoComplete;
        set => SetProperty(ref _autoComplete, value);
    }

    public string? SemanticRole
    {
        get => _semanticRole;
        set => SetProperty(ref _semanticRole, value);
    }

    public List<string>? Labels
    {
        get => _labels;
        set => SetProperty(ref _labels, value);
    }

    public string? RecurrenceSettings { get; set; }

    public RecurrenceInfo? RecurrenceFromObjective { get; set; }

    public int? RecurrenceStepMin { get; set; }
    public int? RecurrenceWindowStartMin { get; set; }
    public int? RecurrenceWindowEndMin { get; set; }
    public string? RecurrenceExpression { get; set; }

    public string ScheduledTimeDisplay
    {
        get
        {
            if (Lifecycle.Equals("Started", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(ActiveEnd))
                {
                    if (DateTime.TryParse(ActiveEnd, out var endTime))
                    {
                        var remaining = endTime - DateTime.Now;
                        if (remaining.TotalMinutes > 0)
                            return $"{(int)remaining.TotalMinutes}m remaining";
                        return "ending";
                    }
                }
                return WorkedMinutes > 0 ? $"{WorkedMinutes}m worked" : "";
            }

            if (Lifecycle.Equals("Done", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(FixedEnd))
                {
                    if (DateTime.TryParse(FixedEnd, out var endTime))
                    {
                        return $"ended {endTime:HH:mm}";
                    }
                }
                return WorkedMinutes > 0 ? $"{WorkedMinutes}m total" : "";
            }

            return TileTimeDisplayResolver.ResolveScheduledTimeDisplay(
                FixedStart,
                ActiveStart,
                ProjectedNextStartAt);
        }
    }

    public SolidColorBrush StatusBadgeBackground => Lifecycle.Trim().ToLowerInvariant() switch
    {
        _ => new SolidColorBrush(Colors.Transparent),
    };

    public SolidColorBrush StatusBadgeBorder => Lifecycle.Trim().ToLowerInvariant() switch
    {
        _ => new SolidColorBrush(Colors.Transparent),
    };

    public SolidColorBrush StatusBadgeForeground => Lifecycle.Trim().ToLowerInvariant() switch
    {
        "started" => new SolidColorBrush(Colors.White),
        "ready" => new SolidColorBrush(Colors.White),
        "done" => new SolidColorBrush(ColorHelper.FromArgb(255, 214, 214, 214)),
        _ => new SolidColorBrush(ColorHelper.FromArgb(255, 214, 214, 214)),
    };

    public string StatusGlyph => Lifecycle.Trim().ToLowerInvariant() switch
    {
        "started" => "\uE945",
        "ready" => "\uE768",
        "done" => "\uE73E",
        _ => "\uE9CE",
    };

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
    private ObservableCollection<TileListItem> _tiles = new();
    private string _selectedFilter = "All";
    private ExecutionView? _executionView;
    private PendingPromptResponse? _pendingPrompt;
    private bool _isConnected;
    private string _statusMessage = string.Empty;
    private string _newTileTitle = string.Empty;
    private string _newTileNextAction = string.Empty;
    private string _newTileDoneDefinition = string.Empty;
    private string _memoText = string.Empty;
    private ObservableCollection<TimelineSegmentViewModel> _timelineSegments = new();
    private ObservableCollection<TimelineHourMarkerViewModel> _timelineHourMarkers = new();
    private ObservableCollection<TimelineAbsoluteBlockViewModel> _timelineBlocks = new();
    private ObservableCollection<PromptActionButtonViewModel> _promptActions = new();
    private double _timelineCanvasWidth = 620d;
    private TimelineViewportSettings _timelineViewport = new(
        ScaleUnit: TimelineScaleUnit.Day,
        RangeMode: TimelineRangeMode.Day24,
        AnchorLocal: DateTimeOffset.Now.ToLocalTime());
    private string _timelineRangeLabel = string.Empty;
    private double _timelineNowTop;
    private string _timelineNowLabel = string.Empty;
    private Visibility _timelineNowVisibility = Visibility.Collapsed;
    private string? _focusedRunningTileId;
    private string? _nextActionableTileId;
    private DateTimeOffset? _nextActionableStartAt;

    public ObservableCollection<TileListItem> Tiles
    {
        get => _tiles;
        set => SetProperty(ref _tiles, value);
    }



    // Fix 2: Filter
    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                OnSelectedFilterChanged(value);
            }
        }
    }

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

    private void OnSelectedFilterChanged(string value)
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

    public PendingPromptResponse? PendingPrompt
    {
        get => _pendingPrompt;
        set => SetProperty(ref _pendingPrompt, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public Visibility ConnectedIndicatorVisibility => IsConnected ? Visibility.Visible : Visibility.Collapsed;
    
    public Visibility DisconnectedIndicatorVisibility => IsConnected ? Visibility.Collapsed : Visibility.Visible;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string NewTileTitle
    {
        get => _newTileTitle;
        set => SetProperty(ref _newTileTitle, value);
    }

    public string NewTileNextAction
    {
        get => _newTileNextAction;
        set => SetProperty(ref _newTileNextAction, value);
    }

    public string NewTileDoneDefinition
    {
        get => _newTileDoneDefinition;
        set => SetProperty(ref _newTileDoneDefinition, value);
    }

    public string MemoText
    {
        get => _memoText;
        set => SetProperty(ref _memoText, value);
    }

    // Timeline
    public ObservableCollection<TimelineSegmentViewModel> TimelineSegments
    {
        get => _timelineSegments;
        set => SetProperty(ref _timelineSegments, value);
    }

    public ObservableCollection<TimelineHourMarkerViewModel> TimelineHourMarkers
    {
        get => _timelineHourMarkers;
        set => SetProperty(ref _timelineHourMarkers, value);
    }

    public ObservableCollection<TimelineAbsoluteBlockViewModel> TimelineBlocks
    {
        get => _timelineBlocks;
        set => SetProperty(ref _timelineBlocks, value);
    }


    public double TimelineCanvasHeight { get; private set; } = 24 * 120;
    public double TimelineCanvasWidth
    {
        get => _timelineCanvasWidth;
        set => SetProperty(ref _timelineCanvasWidth, value);
    }
    public string TimelineRangeLabel
    {
        get => _timelineRangeLabel;
        set => SetProperty(ref _timelineRangeLabel, value);
    }
    public TimelineViewportSettings TimelineViewport
    {
        get => _timelineViewport;
        set => SetProperty(ref _timelineViewport, value);
    }
    public double TimelineNowTop
    {
        get => _timelineNowTop;
        set => SetProperty(ref _timelineNowTop, value);
    }
    public string TimelineNowLabel
    {
        get => _timelineNowLabel;
        set => SetProperty(ref _timelineNowLabel, value);
    }
    public Visibility TimelineNowVisibility
    {
        get => _timelineNowVisibility;
        set => SetProperty(ref _timelineNowVisibility, value);
    }

    public ObservableCollection<PromptActionButtonViewModel> PromptActions
    {
        get => _promptActions;
        set => SetProperty(ref _promptActions, value);
    }

    public bool HasNoTimelineSegments => TimelineBlocks.Count == 0;
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
    public string PendingPromptKind => PendingPrompt?.Prompt?.Kind ?? string.Empty;
    public string PendingPromptSeverity => PendingPrompt?.Prompt?.Severity ?? string.Empty;
    public int? PendingPromptSuggestedMinutes => PendingPrompt?.Prompt?.SuggestedMinutes;
    public bool HasPendingPrompt => PendingPrompt?.Prompt != null;
    public string MemoPlaceholder => _executionView?.MainTile != null
        ? "Attach memo to active tile..."
        : "Send a free memo to core...";

    internal static TileListItem? ResolveNextUpTile(IReadOnlyList<TileListItem> allTiles, string? nextActionableTileId)
    {
        if (string.IsNullOrWhiteSpace(nextActionableTileId))
        {
            return null;
        }

        return allTiles.FirstOrDefault(tile =>
            string.Equals(tile.Id, nextActionableTileId, StringComparison.OrdinalIgnoreCase));
    }

    public TileListItem? NextUpTile => ResolveNextUpTile(_allTiles, _nextActionableTileId);
    // Alias used by MainWindow.xaml x:Bind (ViewModel.NextUp.StatusGlyph)
    public TileListItem? NextUp => NextUpTile;

    public string NextUpTitle => NextUpTile?.Title ?? "No suggested tile";
    public string NextUpAction => NextUpTile?.NextAction ?? "Create a tile or adjust its schedule to surface the next actionable tile.";
    public string NextUpWorkedText => string.IsNullOrWhiteSpace(NextUpTile?.WorkedText) ? "Ready" : NextUpTile!.WorkedText;
    public string NextUpMetaText => NextUpTile is null
        ? "No upcoming task"
        : $"{NextUpTile.TargetDurationText} • {(string.IsNullOrWhiteSpace(NextUpTile.NextAction) ? "No note" : NextUpTile.NextAction)}";
    public string? NextUpTileId => NextUpTile?.Id;
    public Visibility NextUpVisibility => NextUpTile is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility NextUpEmptyVisibility => NextUpTile is null ? Visibility.Visible : Visibility.Collapsed;
    public IReadOnlyList<TileListItem> RunningQuickTiles =>
        _allTiles.Where(t => t.Lifecycle.Equals("Started", StringComparison.OrdinalIgnoreCase)).ToList();
    public TileListItem? MainRunningTask
    {
        get
        {
            var runningTiles = RunningQuickTiles;
            if (runningTiles.Count == 0)
            {
                return null;
            }

            var selectedId = RunningTileSelection.SelectMainRunningTileId(
                runningTiles.Select(tile => new RunningTileSnapshot(tile.Id, tile.Title)).ToList(),
                _focusedRunningTileId,
                _executionView?.MainTile?.Id);

            return runningTiles.FirstOrDefault(tile => string.Equals(tile.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                ?? runningTiles.FirstOrDefault();
        }
    }
    
    public bool HasMainRunningTask => MainRunningTask != null;
    public Visibility MainRunningTaskVisibility => HasMainRunningTask ? Visibility.Visible : Visibility.Collapsed;
    public IReadOnlyList<TileListItem> SecondaryRunningQuickTiles =>
        MainRunningTask == null
            ? RunningQuickTiles
            : RunningQuickTiles.Where(t => !string.Equals(t.Id, MainRunningTask.Id, StringComparison.OrdinalIgnoreCase)).ToList();
    public IReadOnlyList<TileListItem> NextQuickCandidates =>
        _allTiles
            .Where(t => t.Lifecycle.Equals("Ready", StringComparison.OrdinalIgnoreCase))
            .Where(t => NextUpTile == null || !string.Equals(t.Id, NextUpTile.Id, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();
    public string MainCountdownText => CountdownTextResolver.Resolve(
        _executionView?.MainTileEndsAt,
        _nextActionableStartAt,
        DateTimeOffset.UtcNow);

    public double MainRunningProgressPercent
    {
        get
        {
            var running = MainRunningTask;
            if (running is null)
            {
                return 0d;
            }

            if (_executionView?.MainTileStartedAt != null
                && _executionView?.MainTileEndsAt != null
                && DateTimeOffset.TryParse(_executionView.MainTileStartedAt, out var startedAt)
                && DateTimeOffset.TryParse(_executionView.MainTileEndsAt, out var endsAt))
            {
                var totalSeconds = Math.Max(1d, (endsAt - startedAt).TotalSeconds);
                var elapsedSeconds = Math.Clamp((DateTimeOffset.UtcNow - startedAt).TotalSeconds, 0d, totalSeconds);
                return Math.Round((elapsedSeconds / totalSeconds) * 100d, 1);
            }

            return running.ProgressPercent;
        }
    }

    public string NextUpStartText => NextUpTile?.NextStartLabel ?? "unscheduled";
    
    // Core が計算した next_start を表示するだけ（UI側で計算しない）

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
    // These values come directly from Core via ExecutionView - do not calculate in UI
    public bool IsIdle => _executionView?.IsIdle ?? true;
    
    public bool IsWorking => _executionView?.IsWorking ?? false;
    
    public bool IsOnBreak => _executionView?.IsOnBreak ?? false;

    public string? ActiveTileTitle => _executionView?.MainTile?.Title;
    
    public string? ActiveTileNextAction => _executionView?.MainTile?.NextAction;

    public string WorkElapsedText => "N/A"; // Core が計算するため UI 側では不要

    public string BreakRemainingText => "N/A"; // Core が計算するため UI 側では不要

    private InterventionEngine? _interventionEngine;
    private PromptAttentionOverlayService? _promptAttentionOverlayService;
    private PromptToastDisplayService? _promptToastDisplayService;
    private string? _lastHandledPromptId;
    private bool _toastDismissedByAction;
    private readonly Dictionary<string, DateTimeOffset> _promptCooldownById = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan PromptCooldownWindow = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PromptAutoExecutionDelay = TimeSpan.FromSeconds(30);
    private CancellationTokenSource? _promptAutoExecutionCts;
    public PollingService PollingService => _pollingService;

    public MainViewModel()
    {
        _api = new CoreApiClient();
        _pollingService = new PollingService(_api, DaemonManager.Shared);
        
        // Subscribe to polling events
        _pollingService.ExecutionViewChanged += OnExecutionViewChanged;
        _pollingService.TilesChanged += OnTilesChanged;
        _pollingService.PendingPromptChanged += OnPendingPromptChanged;
        _pollingService.TimelineChanged += OnTimelineChanged;
        _pollingService.ConnectionStatusChanged += OnConnectionStatusChanged;

        // Initialize intervention engine
        _interventionEngine = new InterventionEngine(_pollingService, _api);
        PromptAttentionOverlayService.Instance.Initialize(_pollingService);
        _promptAttentionOverlayService = PromptAttentionOverlayService.Instance;
        _promptToastDisplayService = PromptToastDisplayService.Instance;
        _pollingService.PendingPromptChanged += OnPromptToastPromptChanged;

    }

    public async Task InitializeAsync()
    {
        await _pollingService.StartAsync();
    }

    private void OnExecutionViewChanged(object? sender, ExecutionView? view)
    {
        _executionView = view;
        
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
        OnPropertyChanged(nameof(WorkRemainingText));
        OnPropertyChanged(nameof(BreakRemainingText));
        OnPropertyChanged(nameof(MemoPlaceholder));
        OnPropertyChanged(nameof(ExecutionStatusLabel));
        OnPropertyChanged(nameof(ExecutionStatusTitle));
        OnPropertyChanged(nameof(ExecutionStatusBody));
        OnPropertyChanged(nameof(ExecutionStatusDetail));
        OnPropertyChanged(nameof(QuickBarStatus));
        OnPropertyChanged(nameof(QuickBarTitle));
        OnPropertyChanged(nameof(QuickBarSubtitle));
        OnPropertyChanged(nameof(QuickBarSubtitleVisibility));
        OnPropertyChanged(nameof(QuickBarMeta));
        OnPropertyChanged(nameof(QuickPanelHint));
        OnPropertyChanged(nameof(QuickPanelHintVisibility));
        OnPropertyChanged(nameof(QuickPanelPrimaryActionId));
        OnPropertyChanged(nameof(QuickPanelPrimaryLabel));
        OnPropertyChanged(nameof(QuickPanelPrimaryGlyph));
        OnPropertyChanged(nameof(QuickPanelPrimaryToolTip));
        OnPropertyChanged(nameof(QuickPanelSecondaryActionId));
        OnPropertyChanged(nameof(QuickPanelSecondaryLabel));
        OnPropertyChanged(nameof(QuickPanelSecondaryGlyph));
        OnPropertyChanged(nameof(QuickPanelSecondaryToolTip));
        OnPropertyChanged(nameof(QuickPanelLeadingText));
        OnPropertyChanged(nameof(QuickBarTimerText));
        OnPropertyChanged(nameof(QuickBarProgressValue));
        OnPropertyChanged(nameof(QuickBarProgressVisibility));
        OnPropertyChanged(nameof(QuickBarStartNextVisibility));
        OnPropertyChanged(nameof(QuickBarCompleteVisibility));
        OnPropertyChanged(nameof(QuickBarBreakVisibility));
        OnPropertyChanged(nameof(QuickBarResumeVisibility));
        OnPropertyChanged(nameof(QuickPanelPrimaryVisibility));
        OnPropertyChanged(nameof(QuickPanelSecondaryVisibility));
        OnPropertyChanged(nameof(QuickBarWorkingIconVisibility));
        OnPropertyChanged(nameof(QuickBarBreakIconVisibility));
        OnPropertyChanged(nameof(QuickBarReadyIconVisibility));
        OnPropertyChanged(nameof(QuickBarOfflineIconVisibility));
        OnPropertyChanged(nameof(MainCountdownText));
        OnPropertyChanged(nameof(MainRunningProgressPercent));
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
        OnPropertyChanged(nameof(PendingPromptKind));
        OnPropertyChanged(nameof(PendingPromptSeverity));
        OnPropertyChanged(nameof(PendingPromptSuggestedMinutes));
        OnPropertyChanged(nameof(HasPendingPrompt));
        OnPropertyChanged(nameof(ExecutionStatusDetail));
        OnPropertyChanged(nameof(QuickBarMeta));
        OnPropertyChanged(nameof(QuickPanelHint));
        OnPropertyChanged(nameof(QuickPanelHintVisibility));
        OnPropertyChanged(nameof(QuickPanelPrimaryActionId));
        OnPropertyChanged(nameof(QuickPanelPrimaryLabel));
        OnPropertyChanged(nameof(QuickPanelPrimaryGlyph));
        OnPropertyChanged(nameof(QuickPanelPrimaryToolTip));
        OnPropertyChanged(nameof(QuickPanelSecondaryActionId));
        OnPropertyChanged(nameof(QuickPanelSecondaryLabel));
        OnPropertyChanged(nameof(QuickPanelSecondaryGlyph));
        OnPropertyChanged(nameof(QuickPanelSecondaryToolTip));
        OnPropertyChanged(nameof(QuickBarPromptIndicatorVisibility));
        OnPropertyChanged(nameof(QuickPanelPrimaryVisibility));
        OnPropertyChanged(nameof(QuickPanelSecondaryVisibility));
    }

    private void OnPromptToastPromptChanged(object? sender, PendingPromptResponse? prompt)
    {
        System.Diagnostics.Debug.WriteLine($"[OnPromptToastPromptChanged] Called with prompt: {(prompt?.Prompt != null ? prompt.Prompt.Title : "null")}");
        App.DebugLog($"[OnPromptToastPromptChanged] Called with prompt: {(prompt?.Prompt != null ? prompt.Prompt.Title : "null")}");

        if (prompt?.Prompt == null)
        {
            System.Diagnostics.Debug.WriteLine($"[OnPromptToastPromptChanged] Prompt is null, hiding toast");
            App.DebugLog($"[OnPromptToastPromptChanged] Prompt is null, hiding toast");
            _lastHandledPromptId = null;
            _toastDismissedByAction = false;
            CancelPromptAutoExecution();
            _promptToastDisplayService?.Hide();
            return;
        }

        CleanupPromptCooldowns();

        System.Diagnostics.Debug.WriteLine($"[OnPromptToastPromptChanged] Prompt ID: {prompt.Prompt.PromptId}, Last handled: {_lastHandledPromptId}, Dismissed by action: {_toastDismissedByAction}");
        App.DebugLog($"[OnPromptToastPromptChanged] Prompt ID: {prompt.Prompt.PromptId}, Last handled: {_lastHandledPromptId}, Dismissed by action: {_toastDismissedByAction}");

        if (_toastDismissedByAction && prompt.Prompt.PromptId == _lastHandledPromptId)
        {
            _toastDismissedByAction = false;
            _lastHandledPromptId = null;
        }

        if (prompt.Prompt.PromptId == _lastHandledPromptId)
        {
            System.Diagnostics.Debug.WriteLine($"[OnPromptToastPromptChanged] Skipping - already handled");
            App.DebugLog($"[OnPromptToastPromptChanged] Skipping - already handled");
            return;
        }

        if (_promptCooldownById.TryGetValue(prompt.Prompt.PromptId, out var blockedUntil)
            && blockedUntil > DateTimeOffset.UtcNow)
        {
            System.Diagnostics.Debug.WriteLine($"[OnPromptToastPromptChanged] Cooldown active for prompt {prompt.Prompt.PromptId} until {blockedUntil:O}");
            App.DebugLog($"[OnPromptToastPromptChanged] Cooldown active for prompt {prompt.Prompt.PromptId} until {blockedUntil:O}");
            return;
        }

        var settings = new SettingsService();
        var decision = PromptNotificationPolicy.Decide(prompt.Prompt, isFullscreen: false);

        System.Diagnostics.Debug.WriteLine($"[OnPromptToastPromptChanged] Decision: ShowToast={decision.ShowToast}, ShowIntervention={decision.ShowIntervention}");
        App.DebugLog($"[OnPromptToastPromptChanged] Decision: ShowToast={decision.ShowToast}, ShowIntervention={decision.ShowIntervention}");

        if (!decision.ShowToast)
        {
            System.Diagnostics.Debug.WriteLine($"[OnPromptToastPromptChanged] Decision is not to show toast");
            App.DebugLog($"[OnPromptToastPromptChanged] Decision is not to show toast");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[Toast] Showing prompt: {prompt.Prompt.Title}, actions: {string.Join(",", prompt.Prompt.Actions.Select(a => a.Id))}, kind: {prompt.Prompt.Kind}");
        App.DebugLog($"[Toast] Showing prompt: {prompt.Prompt.Title}, actions: {string.Join(",", prompt.Prompt.Actions.Select(a => a.Id))}, kind: {prompt.Prompt.Kind}");

        _lastHandledPromptId = prompt.Prompt.PromptId;
        _toastDismissedByAction = false; // リセット
        StartPromptAutoExecution(prompt.Prompt);
        
        // UI スレッドでトースト表示
        _promptToastDisplayService?.ShowPrompt(
            prompt.Prompt,
            settings.Current.PromptToastMaxVisible,
            async (actionId, stopAt) =>
            {
                _toastDismissedByAction = true;
                MarkPromptCooldown(prompt.Prompt.PromptId);
                CancelPromptAutoExecution();
                System.Diagnostics.Debug.WriteLine($"[Toast] Action clicked: {actionId}");
                App.DebugLog($"[Toast] Action clicked: {actionId}");
                
                // まずトーストを隠す
                _promptToastDisplayService?.Hide();
                
                try
                {
                    var id = actionId.ToUpperInvariant();
                    System.Diagnostics.Debug.WriteLine($"[Toast] Action ID (upper): {id}");
                    bool handledByStartupRecovery = false;
                    if (PendingPrompt?.Prompt != null)
                    {
                        handledByStartupRecovery = await TryRespondStartupRecoveryAsync(
                            PendingPrompt.Prompt,
                            id,
                            stopAt);
                    }

                    if (!handledByStartupRecovery)
                    {
                        switch (id)
                        {
                            case "CONTINUE":
                            case "DISMISS":
                                System.Diagnostics.Debug.WriteLine($"[Toast] Action matched: {id}");
                                break;
                            case "BREAK":
                            case "START_BREAK":
                                System.Diagnostics.Debug.WriteLine($"[Toast] Action matched: {id}");
                                await _api.StartBreakAsync(settings.Current.DefaultBreakMinutes);
                                break;
                            case "COMPLETE":
                            case "COMPLETE_AND_START_NEXT":
                                System.Diagnostics.Debug.WriteLine($"[Toast] Action matched: {id}");
                                await _api.CompleteTileAsync(prompt.Prompt.TileId);
                                break;
                            case "END_BREAK":
                                System.Diagnostics.Debug.WriteLine($"[Toast] Action matched: {id}");
                                await _api.EndBreakAsync();
                                break;
                            case "EXTEND":
                                System.Diagnostics.Debug.WriteLine($"[Toast] Action matched: {id}");
                                await _api.ExtendTileAsync(10);
                                break;
                            case "START":
                            case "START_TILE":
                                System.Diagnostics.Debug.WriteLine($"[Toast] Action matched: {id}");
                                if (!string.IsNullOrWhiteSpace(prompt.Prompt.TileId))
                                {
                                    await _api.StartTileAsync(prompt.Prompt.TileId);
                                }
                                break;
                            default:
                                System.Diagnostics.Debug.WriteLine($"[Toast] Action NOT matched: {id}");
                                break;
                        }
                    }
                    
                    // アクション実行後、即座にポーリングして状態を更新
                    System.Diagnostics.Debug.WriteLine($"[Toast] Polling after action");
                    App.DebugLog($"[Toast] Polling after action");
                    await _pollingService.PollAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Toast] Action error: {ex.Message}");
                    App.DebugLog($"[Toast] Action error: {ex.Message}");
                }
            },
            async (actionId, minutes) =>
            {
                _toastDismissedByAction = true;
                MarkPromptCooldown(prompt.Prompt.PromptId);
                CancelPromptAutoExecution();
                System.Diagnostics.Debug.WriteLine($"[Toast] Defer: action={actionId}, minutes={minutes}");
                App.DebugLog($"[Toast] Defer: action={actionId}, minutes={minutes}");
                
                // まずトーストを隠す
                _promptToastDisplayService?.Hide();
                
                try
                {
                    if (!string.IsNullOrWhiteSpace(prompt.Prompt.TileId) && minutes.HasValue)
                    {
                        await _api.DeferTileAsync(prompt.Prompt.TileId, minutes: minutes.Value);
                    }
                    
                    // アクション実行後、即座にポーリングして状態を更新
                    System.Diagnostics.Debug.WriteLine($"[Toast] Polling after defer");
                    App.DebugLog($"[Toast] Polling after defer");
                    await _pollingService.PollAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Toast] Defer error: {ex.Message}");
                    App.DebugLog($"[Toast] Defer error: {ex.Message}");
                }
            });
    }

    private void StartPromptAutoExecution(PromptView prompt)
    {
        CancelPromptAutoExecution();
        var actionId = ResolveAutoActionId(prompt);
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return;
        }

        var promptId = prompt.PromptId;
        _promptAutoExecutionCts = new CancellationTokenSource();
        var token = _promptAutoExecutionCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(PromptAutoExecutionDelay, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (PendingPrompt?.Prompt?.PromptId != promptId)
                {
                    return;
                }

                _toastDismissedByAction = true;
                MarkPromptCooldown(promptId);
                _promptToastDisplayService?.Hide();
                await ExecutePromptActionAsync(actionId, prompt, null);
                await _pollingService.PollAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Toast] Auto action error: {ex.Message}");
                App.DebugLog($"[Toast] Auto action error: {ex.Message}");
            }
        }, token);
    }

    private void CancelPromptAutoExecution()
    {
        _promptAutoExecutionCts?.Cancel();
        _promptAutoExecutionCts?.Dispose();
        _promptAutoExecutionCts = null;
    }

    private static string? ResolveAutoActionId(PromptView prompt)
    {
        var kind = prompt.Kind?.Trim().ToLowerInvariant();
        switch (kind)
        {
            case "start":
                if (prompt.Actions.Any(action =>
                        string.Equals(action.Id, "START", StringComparison.OrdinalIgnoreCase)))
                {
                    return "START";
                }
                if (prompt.Actions.Any(action =>
                        string.Equals(action.Id, "START_TILE", StringComparison.OrdinalIgnoreCase)))
                {
                    return "START_TILE";
                }
                return null;
            case "end":
                if (prompt.Actions.Any(action =>
                        string.Equals(action.Id, "COMPLETE", StringComparison.OrdinalIgnoreCase)))
                {
                    return "COMPLETE";
                }
                if (prompt.Actions.Any(action =>
                        string.Equals(action.Id, "COMPLETE_AND_START_NEXT", StringComparison.OrdinalIgnoreCase)))
                {
                    return "COMPLETE_AND_START_NEXT";
                }
                return null;
            default:
                return null;
        }
    }

    private async Task ExecutePromptActionAsync(string actionId, PromptView prompt, DateTimeOffset? stopAt)
    {
        var settings = new SettingsService();
        var id = actionId.ToUpperInvariant();
        var handledByStartupRecovery = await TryRespondStartupRecoveryAsync(prompt, id, stopAt);
        if (handledByStartupRecovery)
        {
            return;
        }

        switch (id)
        {
            case "CONTINUE":
            case "DISMISS":
                return;
            case "BREAK":
            case "START_BREAK":
                await _api.StartBreakAsync(settings.Current.DefaultBreakMinutes);
                return;
            case "COMPLETE":
            case "COMPLETE_AND_START_NEXT":
                await _api.CompleteTileAsync(prompt.TileId);
                return;
            case "END_BREAK":
                await _api.EndBreakAsync();
                return;
            case "EXTEND":
                await _api.ExtendTileAsync(10);
                return;
            case "START":
            case "START_TILE":
                if (!string.IsNullOrWhiteSpace(prompt.TileId))
                {
                    await _api.StartTileAsync(prompt.TileId);
                }
                return;
            default:
                return;
        }
    }

    private void MarkPromptCooldown(string? promptId)
    {
        if (string.IsNullOrWhiteSpace(promptId))
        {
            return;
        }
        _promptCooldownById[promptId] = DateTimeOffset.UtcNow + PromptCooldownWindow;
    }

    private void CleanupPromptCooldowns()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _promptCooldownById
                     .Where(kv => kv.Value <= now)
                     .Select(kv => kv.Key)
                     .ToList())
        {
            _promptCooldownById.Remove(key);
        }
    }

    private void OnTimelineChanged(object? sender, TimelineTodayResponse? timeline)
    {
        foreach (var tile in _allTiles)
        {
            tile.NextStartLabel = ResolveNextStartLabel(tile.ProjectedNextStartAt);
        }

        const double laneGap = 4d;
        var timelineWidth = Math.Max(280d, TimelineCanvasWidth);
        var layout = AbsoluteTimelineResolver.Resolve(
            timeline?.Items ?? [],
            DateTimeOffset.Now,
            TimelineViewport);

        TimelineHourMarkers = new ObservableCollection<TimelineHourMarkerViewModel>(
            layout.HourMarkers.Select(marker => new TimelineHourMarkerViewModel
            {
                Label = marker.Label,
                Top = marker.Top,
            }));

        TimelineBlocks = new ObservableCollection<TimelineAbsoluteBlockViewModel>(
            layout.Blocks.Select(block => new TimelineAbsoluteBlockViewModel
            {
                Title = block.Title,
                TimeRangeText = $"{block.StartLabel} - {block.EndLabel}",
                DurationText = block.IsActive ? $"{block.DurationLabel} ongoing" : block.DurationLabel,
                KindLabel = string.IsNullOrWhiteSpace(block.Kind) ? "task" : block.Kind,
                Lane = block.Lane,
                TotalLanes = block.TotalLanes,
                IsFullWidth = block.IsFullWidth,
                Left = block.IsFullWidth
                    ? laneGap / 2
                    : ((timelineWidth / Math.Max(1, block.TotalLanes)) * block.Lane) + (laneGap / 2),
                Width = block.IsFullWidth
                    ? Math.Max(24, timelineWidth - laneGap)
                    : Math.Max(24, (timelineWidth / Math.Max(1, block.TotalLanes)) - laneGap),
                Top = block.Top,
                Height = block.Height,
                TileId = block.TileId,
                Lifecycle = block.IsDone ? "done" : block.IsActive ? "started" : "ready",
                StatusIconGlyph = block.IsDone ? "\uE73E" : block.IsActive ? "\uE945" : "\uE768",
                StatusIconToolTip = block.IsDone ? "done" : block.IsActive ? "active" : "scheduled",
                Fill = block.IsActive
                    ? (SolidColorBrush)Application.Current.Resources["AppSurface1Brush"]
                    : (SolidColorBrush)Application.Current.Resources["AppSurfaceElevatedBrush"],
                BorderBrush = (SolidColorBrush)Application.Current.Resources["AppBorderBrush"],
                ForegroundBrush = (SolidColorBrush)Application.Current.Resources["AppForegroundBrush"],
                SecondaryForegroundBrush = (SolidColorBrush)Application.Current.Resources["AppForegroundMutedBrush"],
                StatusFill = ResolveTimelineStatusFill(block),
                StatusBorderBrush = ResolveTimelineStatusBorder(block),
                StatusForegroundBrush = ResolveTimelineStatusForeground(block),
            }));

        TimelineCanvasHeight = layout.CanvasHeight;
        TimelineRangeLabel = layout.RangeLabel;
        var nowMarker = layout.NowIndicators.FirstOrDefault();
        if (nowMarker != null)
        {
            TimelineNowTop = nowMarker.Top;
            TimelineNowLabel = nowMarker.Label;
            TimelineNowVisibility = Visibility.Visible;
        }
        else
        {
            TimelineNowTop = 0;
            TimelineNowLabel = string.Empty;
            TimelineNowVisibility = Visibility.Collapsed;
        }

        TimelineSegments = new ObservableCollection<TimelineSegmentViewModel>();
        OnPropertyChanged(nameof(TimelineSegments));
        OnPropertyChanged(nameof(TimelineHourMarkers));
        OnPropertyChanged(nameof(TimelineBlocks));
        OnPropertyChanged(nameof(TimelineCanvasHeight));
        OnPropertyChanged(nameof(TimelineRangeLabel));
        OnPropertyChanged(nameof(TimelineNowTop));
        OnPropertyChanged(nameof(TimelineNowLabel));
        OnPropertyChanged(nameof(TimelineNowVisibility));
        OnPropertyChanged(nameof(HasNoTimelineSegments));
        OnPropertyChanged(nameof(NextUpStartText));
        OnPropertyChanged(nameof(MainCountdownText));
        OnPropertyChanged(nameof(NextQuickCandidates));
    }

    public void UpdateTimelineViewport(TimelineViewportSettings viewport)
    {
        TimelineViewport = viewport;
        OnTimelineChanged(this, _pollingService.CurrentTimeline);
    }

    private void OnTilesChanged(object? sender, TilesResponse? tiles)
    {
        if (tiles?.Tiles == null) return;

        _nextActionableTileId = string.IsNullOrWhiteSpace(tiles.NextActionableTileId)
            ? null
            : tiles.NextActionableTileId;
        _nextActionableStartAt = DateTimeOffset.TryParse(tiles.NextActionableStartAt, out var nextStart)
            ? nextStart
            : null;

        _allTiles = tiles.Tiles.Select(t =>
        {
            var item = TileListItemMapper.Map(t);
            item.Lifecycle = NormalizeLifecycle(item.Lifecycle);
            return item;
        })
        .OrderBy(t => LifecycleSortKey(t.Lifecycle))
        .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
        .ToList();

        var runningCount = _allTiles.Count(t => t.Lifecycle.Equals("Started", StringComparison.OrdinalIgnoreCase));
        System.Diagnostics.Debug.WriteLine($"[OnTilesChanged] Total tiles: {_allTiles.Count}, Running: {runningCount}");
        foreach (var tile in _allTiles.Where(t => t.Lifecycle.Equals("Started", StringComparison.OrdinalIgnoreCase)))
        {
            System.Diagnostics.Debug.WriteLine($"  - Running tile: {tile.Title} (id={tile.Id})");
        }

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
        OnPropertyChanged(nameof(NextUp));
        OnPropertyChanged(nameof(NextUpTitle));
        OnPropertyChanged(nameof(NextUpAction));
        OnPropertyChanged(nameof(NextUpWorkedText));
        OnPropertyChanged(nameof(NextUpMetaText));
        OnPropertyChanged(nameof(NextUpStartText));
        OnPropertyChanged(nameof(MainCountdownText));
        OnPropertyChanged(nameof(NextUpTileId));
        OnPropertyChanged(nameof(NextUpVisibility));
        OnPropertyChanged(nameof(NextUpEmptyVisibility));
        OnPropertyChanged(nameof(RunningQuickTiles));
        OnPropertyChanged(nameof(MainRunningTask));
        OnPropertyChanged(nameof(HasMainRunningTask));
        OnPropertyChanged(nameof(MainRunningTaskVisibility));
        OnPropertyChanged(nameof(MainRunningProgressPercent));
        OnPropertyChanged(nameof(SecondaryRunningQuickTiles));
        OnPropertyChanged(nameof(NextQuickCandidates));
        OnPropertyChanged(nameof(ExecutionStatusTitle));
        OnPropertyChanged(nameof(ExecutionStatusBody));
        OnPropertyChanged(nameof(ExecutionStatusDetail));
        OnPropertyChanged(nameof(QuickBarTitle));
        OnPropertyChanged(nameof(QuickBarSubtitle));
        OnPropertyChanged(nameof(QuickBarSubtitleVisibility));
        OnPropertyChanged(nameof(QuickBarMeta));
        OnPropertyChanged(nameof(QuickPanelHint));
        OnPropertyChanged(nameof(QuickPanelHintVisibility));
        OnPropertyChanged(nameof(QuickPanelPrimaryActionId));
        OnPropertyChanged(nameof(QuickPanelPrimaryLabel));
        OnPropertyChanged(nameof(QuickPanelPrimaryGlyph));
        OnPropertyChanged(nameof(QuickPanelPrimaryToolTip));
        OnPropertyChanged(nameof(QuickPanelSecondaryActionId));
        OnPropertyChanged(nameof(QuickPanelSecondaryLabel));
        OnPropertyChanged(nameof(QuickPanelSecondaryGlyph));
        OnPropertyChanged(nameof(QuickPanelSecondaryToolTip));
        OnPropertyChanged(nameof(QuickPanelLeadingText));
        OnPropertyChanged(nameof(QuickBarStartNextVisibility));
        OnPropertyChanged(nameof(QuickPanelPrimaryVisibility));
        OnPropertyChanged(nameof(QuickPanelSecondaryVisibility));
    }

    private void OnConnectionStatusChanged(object? sender, bool connected)
    {
        IsConnected = connected;
        OnPropertyChanged(nameof(ConnectedIndicatorVisibility));
        OnPropertyChanged(nameof(DisconnectedIndicatorVisibility));
        StatusMessage = connected ? "Connected" : "Daemon offline";
        OnPropertyChanged(nameof(QuickBarStatus));
        OnPropertyChanged(nameof(QuickBarTitle));
        OnPropertyChanged(nameof(QuickBarSubtitle));
        OnPropertyChanged(nameof(QuickBarSubtitleVisibility));
        OnPropertyChanged(nameof(QuickBarMeta));
        OnPropertyChanged(nameof(QuickPanelHint));
        OnPropertyChanged(nameof(QuickPanelHintVisibility));
        OnPropertyChanged(nameof(QuickPanelPrimaryActionId));
        OnPropertyChanged(nameof(QuickPanelPrimaryLabel));
        OnPropertyChanged(nameof(QuickPanelPrimaryGlyph));
        OnPropertyChanged(nameof(QuickPanelPrimaryToolTip));
        OnPropertyChanged(nameof(QuickPanelSecondaryActionId));
        OnPropertyChanged(nameof(QuickPanelSecondaryLabel));
        OnPropertyChanged(nameof(QuickPanelSecondaryGlyph));
        OnPropertyChanged(nameof(QuickPanelSecondaryToolTip));
        OnPropertyChanged(nameof(QuickPanelLeadingText));
        OnPropertyChanged(nameof(QuickBarTimerText));
        OnPropertyChanged(nameof(QuickBarProgressValue));
        OnPropertyChanged(nameof(QuickBarProgressVisibility));
        OnPropertyChanged(nameof(QuickBarStartNextVisibility));
        OnPropertyChanged(nameof(QuickPanelPrimaryVisibility));
        OnPropertyChanged(nameof(QuickPanelSecondaryVisibility));
        OnPropertyChanged(nameof(QuickBarWorkingIconVisibility));
        OnPropertyChanged(nameof(QuickBarBreakIconVisibility));
        OnPropertyChanged(nameof(QuickBarReadyIconVisibility));
        OnPropertyChanged(nameof(QuickBarOfflineIconVisibility));
        OnPropertyChanged(nameof(MainCountdownText));
    }

    public void FocusRunningTile(string tileId)
    {
        if (string.IsNullOrWhiteSpace(tileId)) return;
        _focusedRunningTileId = tileId;
        OnPropertyChanged(nameof(MainRunningTask));
        OnPropertyChanged(nameof(MainRunningProgressPercent));
        OnPropertyChanged(nameof(SecondaryRunningQuickTiles));
    }

    private string? ResolveNextStartLabel(string? projectedNextStartAt)
    {
        return TileTimeDisplayResolver.ResolveNextStartLabel(projectedNextStartAt);
    }

    private static SolidColorBrush ResolveTimelineStatusFill(TimelineBlock block)
    {
        return new SolidColorBrush(Colors.Transparent);
    }

    private static SolidColorBrush ResolveTimelineStatusBorder(TimelineBlock block)
    {
        return new SolidColorBrush(Colors.Transparent);
    }

    private static SolidColorBrush ResolveTimelineStatusForeground(TimelineBlock block)
    {
        if (block.IsDone)
        {
            return new SolidColorBrush(ColorHelper.FromArgb(255, 214, 214, 214));
        }

        if (block.IsActive)
        {
            return new SolidColorBrush(Colors.White);
        }

        return new SolidColorBrush(Colors.White);
    }


    private async Task<bool> EnsureCreateQuotaAvailableAsync()
    {
        try
        {
            var quota = await _api.GetTileQuotaAsync();
            if (quota == null)
            {
                StatusMessage = "Error: failed to validate tile limit.";
                return false;
            }

            if (quota.LimitReached)
            {
                StatusMessage = "Error: free plan limit reached (100 tiles).";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: failed to validate tile limit ({ex.Message})";
            return false;
        }
    }

    [RelayCommand]
    private async Task CreateTileAsync()
    {
        var title = NewTileTitle?.Trim();
        if (string.IsNullOrEmpty(title)) return;
        if (!await EnsureCreateQuotaAvailableAsync()) return;

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

    public string WorkRemainingText => "N/A"; // Core が phase_ends_at で計算済み

    public string ExecutionStatusLabel => IsWorking
        ? "Work block"
        : IsOnBreak
            ? "Break block"
            : "Ready";

    private QuickBarPresentation CurrentQuickBarPresentation => QuickBarPresentationResolver.Resolve(
        isConnected: IsConnected,
        isWorking: IsWorking,
        isOnBreak: IsOnBreak,
        activeTitle: ActiveTileTitle,
        activeNextAction: ActiveTileNextAction,
        nextUpTitle: NextUpTitle,
        nextUpAction: NextUpAction,
        workElapsedText: WorkElapsedText,
        breakRemainingText: BreakRemainingText,
        hasPendingPrompt: HasPendingPrompt);

    private QuickPanelActionState CurrentQuickPanelActionState => QuickPanelActionResolver.Resolve(
        isConnected: IsConnected,
        hasPendingPrompt: HasPendingPrompt,
        isWorking: IsWorking,
        isOnBreak: IsOnBreak,
        hasNextTile: !string.IsNullOrWhiteSpace(NextUpTileId));

    public string QuickBarStatus => CurrentQuickBarPresentation.Status;
    public string QuickBarTitle => CurrentQuickBarPresentation.Title;
    public string QuickBarSubtitle => CurrentQuickBarPresentation.Subtitle ?? string.Empty;
    public Visibility QuickBarSubtitleVisibility => string.IsNullOrWhiteSpace(QuickBarSubtitle) ? Visibility.Collapsed : Visibility.Visible;
    public string QuickBarMeta => CurrentQuickBarPresentation.Meta;
    public string QuickPanelHint => CurrentQuickPanelActionState.Hint;
    public Visibility QuickPanelHintVisibility => string.IsNullOrWhiteSpace(QuickPanelHint) ? Visibility.Collapsed : Visibility.Visible;
    public string? QuickPanelPrimaryActionId => CurrentQuickPanelActionState.PrimaryActionId;
    public string QuickPanelPrimaryLabel => CurrentQuickPanelActionState.PrimaryLabel ?? string.Empty;
    public string? QuickPanelSecondaryActionId => CurrentQuickPanelActionState.SecondaryActionId;
    public string QuickPanelSecondaryLabel => CurrentQuickPanelActionState.SecondaryLabel ?? string.Empty;
    public string QuickPanelPrimaryGlyph => QuickPanelActionVisualResolver.Resolve(QuickPanelPrimaryActionId).Glyph;
    public string QuickPanelPrimaryToolTip => QuickPanelActionVisualResolver.Resolve(QuickPanelPrimaryActionId).ToolTip;
    public string QuickPanelSecondaryGlyph => QuickPanelActionVisualResolver.Resolve(QuickPanelSecondaryActionId).Glyph;
    public string QuickPanelSecondaryToolTip => QuickPanelActionVisualResolver.Resolve(QuickPanelSecondaryActionId).ToolTip;
    public string QuickPanelLeadingText => QuickPanelLeadingResolver.Resolve(
        IsConnected,
        IsWorking,
        IsOnBreak,
        ReadyCount,
        MainCountdownText,
        MainCountdownText);
    public string QuickBarTimerText => IsConnected
        ? MainCountdownText
        : "Offline";
    public double QuickBarProgressValue
    {
        get
        {
            if (_executionView?.MainTileStartedAt == null || _executionView?.MainTileEndsAt == null)
            {
                return 0;
            }

            if (!DateTimeOffset.TryParse(_executionView.MainTileStartedAt, out var startedAt) ||
                !DateTimeOffset.TryParse(_executionView.MainTileEndsAt, out var endsAt))
            {
                return 0;
            }

            var totalSeconds = Math.Max(1d, (endsAt - startedAt).TotalSeconds);
            var elapsedSeconds = Math.Clamp((DateTimeOffset.UtcNow - startedAt).TotalSeconds, 0d, totalSeconds);
            return Math.Round((elapsedSeconds / totalSeconds) * 100d, 1);
        }
    }
    public Visibility QuickBarProgressVisibility => (IsWorking || IsOnBreak) && _executionView?.MainTileEndsAt != null
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility QuickBarPromptIndicatorVisibility => HasPendingPrompt ? Visibility.Visible : Visibility.Collapsed;
    public Visibility QuickBarStartNextVisibility => (!IsWorking && !IsOnBreak && !string.IsNullOrWhiteSpace(NextUpTileId) && IsConnected)
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility QuickBarCompleteVisibility => IsWorking ? Visibility.Visible : Visibility.Collapsed;
    public Visibility QuickBarBreakVisibility => IsWorking ? Visibility.Visible : Visibility.Collapsed;
    public Visibility QuickBarResumeVisibility => IsOnBreak ? Visibility.Visible : Visibility.Collapsed;
    public Visibility QuickBarWorkingIconVisibility => IsWorking ? Visibility.Visible : Visibility.Collapsed;
    public Visibility QuickBarBreakIconVisibility => IsOnBreak ? Visibility.Visible : Visibility.Collapsed;
    public Visibility QuickBarReadyIconVisibility => (!IsWorking && !IsOnBreak && IsConnected) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility QuickBarOfflineIconVisibility => !IsConnected ? Visibility.Visible : Visibility.Collapsed;
    public Visibility QuickPanelPrimaryVisibility => CurrentQuickPanelActionState.PrimaryActionId is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility QuickPanelSecondaryVisibility => CurrentQuickPanelActionState.SecondaryActionId is null ? Visibility.Collapsed : Visibility.Visible;

    public string ExecutionStatusTitle => IsWorking
        ? (ActiveTileTitle ?? "Working")
        : IsOnBreak
            ? "Break in progress"
            : (NextUpTile?.Title ?? "No active tile");

    public string ExecutionStatusBody => IsWorking
        ? (ActiveTileNextAction ?? "Continue the current tile.")
        : IsOnBreak
            ? "Step away briefly, then return for the next focus block."
            : (NextUpTile?.NextAction ?? IdleGuidanceText);

    public string ExecutionStatusDetail
    {
        get
        {
            if (_executionView?.MainTileEndsAt != null &&
                DateTimeOffset.TryParse(_executionView.MainTileEndsAt, out var endsAt))
            {
                var label = IsOnBreak ? "Break ends" : IsWorking ? "Block ends" : "Available at";
                return $"{label} {endsAt.ToLocalTime():HH:mm}";
            }

            if (HasPendingPrompt)
            {
                return "Respond to prompts from the fixed in-app prompt card.";
            }

            return IsIdle
                ? "Prompt notifications stay visible as fixed in-app cards."
                : "Watching the daemon schedule for the next transition.";
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
            OnPropertyChanged(nameof(NextUp));
            OnPropertyChanged(nameof(NextUpTitle));
            OnPropertyChanged(nameof(NextUpAction));
            OnPropertyChanged(nameof(NextUpWorkedText));
            OnPropertyChanged(nameof(NextUpTileId));
            OnPropertyChanged(nameof(NextUpVisibility));
            OnPropertyChanged(nameof(NextUpEmptyVisibility));
        }
    }

    public Task RefreshAsync() => _pollingService.PollAsync();

    public void NotifyTimeAdvanced()
    {
        OnPropertyChanged(nameof(MainCountdownText));
        OnPropertyChanged(nameof(QuickBarTimerText));
        OnPropertyChanged(nameof(QuickBarProgressValue));
        OnPropertyChanged(nameof(MainRunningProgressPercent));
        OnPropertyChanged(nameof(ExecutionStatusDetail));
        OnPropertyChanged(nameof(QuickPanelLeadingText));
    }

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
            var result = await _api.AttachMemoAsync(_executionView?.MainTile?.Id, text);
            if (result != null && !result.Ok)
                StatusMessage = $"Error: {result.Error}";
            else
            {
                StatusMessage = _executionView?.MainTile != null ? "Memo attached to active tile" : "Global memo sent";
                MemoText = string.Empty;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    public async Task RespondToPromptAsync(string? actionId, DateTimeOffset? stopAt = null)
    {
        if (string.IsNullOrWhiteSpace(actionId) || PendingPrompt?.Prompt == null)
            return;

        try
        {
            if (await TryRespondStartupRecoveryAsync(PendingPrompt.Prompt, actionId, stopAt))
            {
                StatusMessage = $"Prompt action: {actionId}";
                await _pollingService.PollAsync();
                return;
            }

            CommandResponse? result = actionId switch
            {
                "START" when !string.IsNullOrWhiteSpace(PendingPrompt.Prompt.TileId)
                    => await _api.StartTileAsync(PendingPrompt.Prompt.TileId),
                "DEFER" when !string.IsNullOrWhiteSpace(PendingPrompt.Prompt.TileId)
                    => await _api.DeferTileAsync(PendingPrompt.Prompt.TileId),
                "COMPLETE" when !string.IsNullOrWhiteSpace(PendingPrompt.Prompt.TileId)
                    => await _api.CompleteTileAsync(PendingPrompt.Prompt.TileId),
                "COMPLETE_AND_START_NEXT" => await _api.CompleteTileAsync(PendingPrompt.Prompt.TileId),
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

    private async Task<bool> TryRespondStartupRecoveryAsync(PromptView prompt, string actionId, DateTimeOffset? stopAt)
    {
        var normalizedAction = actionId.Trim().ToUpperInvariant();
        if (!IsStartupRecoveryAction(normalizedAction))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(prompt.PromptId) || string.IsNullOrWhiteSpace(prompt.TileId))
        {
            StatusMessage = "Error: startup recovery prompt is missing required identifiers";
            return true;
        }

        var result = await _api.RespondStartupRecoveryPromptAsync(
            prompt.PromptId,
            prompt.TileId,
            normalizedAction,
            stopAt);

        if (result != null && !result.Ok)
        {
            StatusMessage = $"Error: {result.Error}";
        }

        return true;
    }

    private static bool IsStartupRecoveryAction(string actionId)
        => actionId is "CONFIRM_CONTINUE" or "CONFIRM_STOP_AT" or "CONFIRM_EXECUTED" or "CONFIRM_SKIPPED" or "DISMISS";

    public void InjectPrompt(PromptView prompt)
    {
        if (prompt == null) return;
        
        System.Diagnostics.Debug.WriteLine($"[InjectPrompt] Injecting prompt: {prompt.Title}");
        App.DebugLog($"[InjectPrompt] Injecting prompt: {prompt.Title}");
        
        // PendingPromptを更新
        var response = new PendingPromptResponse(prompt);
        OnPendingPromptChanged(this, response);
        
        // トースト通知も直接トリガー
        OnPromptToastPromptChanged(this, response);
    }

    // DeferTile command - Core に defer を送るだけ（UI側で判断しない）
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
        _promptAttentionOverlayService?.Dispose();
        CancelPromptAutoExecution();
        _promptToastDisplayService?.Hide();
        _promptToastDisplayService?.Dispose();
        _pollingService.PendingPromptChanged -= OnPromptToastPromptChanged;
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
