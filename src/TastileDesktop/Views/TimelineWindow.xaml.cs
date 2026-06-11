using Microsoft.UI.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TastileDesktop.Views;

public sealed partial class TimelineWindow : Window
{
    public MainViewModel ViewModel { get; } = new();
    private ComboBox? RangeComboBox;
    private readonly CoreApiClient _api = new(
        getAccessToken: Services.AuthService.Instance.GetAccessTokenAsync,
        refreshTokens: Services.CognitoAuthService.Instance.RefreshAsync);
    private readonly SettingsService _settings = new();
    private readonly PromptToastDisplayService _promptToast = PromptToastDisplayService.Instance;

    private TimelineViewportSettings _viewport = new(
        ScaleUnit: TimelineScaleUnit.Day,
        RangeMode: TimelineRangeMode.Day24,
        AnchorLocal: DateTimeOffset.Now.ToLocalTime());

    private bool _isUpdatingRangeCombo;
    private IReadOnlyList<TimelineRangeMode> _configuredModes = [];
    private readonly DispatcherQueueTimer _resizeDebounceTimer;
    private double _lastMonthCellWidth;
    private double _lastMonthCellHeight;
    // private double _lastWeekCellWidth; // Removed - week uses unified timeline
    // private double _lastWeekCellHeight; // Removed - week uses unified timeline
    private double _lastYearMonthWidth;
    private double _lastYearMonthHeight;
    private double _lastYearDayWidth;
    private double _lastLoggedMonthCellWidth = -1d;
    private double _lastLoggedMonthCellHeight = -1d;
    private int _monthInitLogCount;
    private bool _initialLayoutApplied;
    private bool _needsMonthInitialSizing;

    public TimelineWindow()
    {
        InitializeComponent();
        EnsureNamedElementsBound();
        WireToolbarControls();
        FloatingWindowHelper.Configure(this, TitleBarArea, 1100, 760);
        TimelineRootGrid.DataContext = ViewModel;
        _resizeDebounceTimer = DispatcherQueue.CreateTimer();
        _resizeDebounceTimer.Interval = TimeSpan.FromMilliseconds(120);
        _resizeDebounceTimer.IsRepeating = false;
        _resizeDebounceTimer.Tick += (_, _) =>
        {
            if (ViewModel.TimelineCanvasVisibility == Visibility.Visible)
            {
                SafeUpdateViewport(_viewport);
            }
        };
        TimelineCanvasHost.SizeChanged += OnTimelineCanvasHostSizeChanged;
        MonthCalendarHost.SizeChanged += (_, _) => ApplyCalendarCellDimensions();
        MonthCalendarHost.LayoutUpdated += OnMonthCalendarHostLayoutUpdated;
        WeekTimelineScrollViewer.SizeChanged += (_, _) => ApplyWeekDayColumnWidths();
        YearCalendarHost.SizeChanged += (_, _) => ApplyCalendarCellDimensions();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) => ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.TimelineBlockEditRequested += OnTimelineBlockEditRequested;
        ViewModel.TimelinePromptRequested += OnTimelinePromptRequested;
        Closed += (_, _) => ViewModel.TimelineBlockEditRequested -= OnTimelineBlockEditRequested;
        Closed += (_, _) => ViewModel.TimelinePromptRequested -= OnTimelinePromptRequested;
        Closed += (_, _) => MonthCalendarHost.LayoutUpdated -= OnMonthCalendarHostLayoutUpdated;

        ViewModel.TimelineCanvasWidth = Math.Max(320d, TimelineCanvasHost.ActualWidth);
        UpdateSelectionButtons();
        SetLoading(true);

        App.DebugLog($"[TimelineWindow] Initial viewport: ScaleUnit={_viewport.ScaleUnit}, RangeMode={_viewport.RangeMode}");
        App.DebugLog($"[TimelineWindow] TimelineCanvasVisibility={ViewModel.TimelineCanvasVisibility}");
        App.DebugLog($"[TimelineWindow] MonthCalendarVisibility={ViewModel.MonthCalendarVisibility}");
        App.DebugLog($"[TimelineWindow] WeekCalendarVisibility={ViewModel.WeekCalendarVisibility}");
        App.DebugLog($"[TimelineWindow] YearCalendarVisibility={ViewModel.YearCalendarVisibility}");
        App.DebugLog($"[TimelineWindow] TimelineBlocks count={ViewModel.TimelineBlocks.Count}");

        ViewModel.UpdateTimelineViewport(_viewport);
        _ = ViewModel.InitializeAsync();
        TimelineRootGrid.Loaded += OnWindowLoaded;
        Closed += (_, _) => TimelineRootGrid.Loaded -= OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialLayoutApplied)
        {
            return;
        }

        _initialLayoutApplied = true;
        EnsureInitialLayoutApplied();
        ScheduleCalendarReflow();
    }

    private void EnsureInitialLayoutApplied()
    {
        var width = Math.Max(320d, TimelineCanvasHost.ActualWidth);
        if (Math.Abs(width - ViewModel.TimelineCanvasWidth) > 0.5d)
        {
            ViewModel.TimelineCanvasWidth = width;
            ViewModel.UpdateTimelineViewport(_viewport);
        }

        ApplyCalendarCellDimensions();
        ApplyWeekDayColumnWidths();
    }

    private void EnsureNamedElementsBound()
    {
        if (Content is not FrameworkElement root)
        {
            return;
        }

        try
        {
            TitleBarArea ??= root.FindName("TitleBarArea") as Grid;
            TimelineRootGrid ??= root.FindName("TimelineRootGrid") as Grid;
            ToolbarPanel ??= root.FindName("ToolbarPanel") as Border;
            TimelineScrollViewer ??= root.FindName("TimelineScrollViewer") as ScrollViewer;
            MonthCalendarHost ??= root.FindName("MonthCalendarHost") as Grid;
            // WeekCalendarHost ??= root.FindName("WeekCalendarHost") as Grid; // Removed - replaced by WeekTimelineRoot
            WeekTimelineScrollViewer ??= root.FindName("WeekTimelineScrollViewer") as ScrollViewer;
            YearCalendarHost ??= root.FindName("YearCalendarHost") as ItemsControl;
            LoadingOverlay ??= root.FindName("LoadingOverlay") as Grid;
            TimelineCanvasHost ??= root.FindName("TimelineCanvasHost") as Grid;
        }
        catch (COMException ex)
        {
            App.DebugLog($"[TimelineWindow] EnsureNamedElementsBound skipped: {ex.Message}");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.MonthCalendarRows) or nameof(MainViewModel.MonthCalendarVisibility))
        {
            _needsMonthInitialSizing = ViewModel.MonthCalendarVisibility == Visibility.Visible;
            if (_needsMonthInitialSizing)
            {
                _lastMonthCellWidth = -1d;
                _lastMonthCellHeight = -1d;
                App.DebugLog("[TimelineWindow][MonthInit] Pending sizing requested by ViewModel update");
            }
        }

        if (e.PropertyName is nameof(MainViewModel.TimelineHourMarkers)
            or nameof(MainViewModel.TimelineBlocks)
            or nameof(MainViewModel.TimelineCanvasHeight)
            or nameof(MainViewModel.MonthCalendarRows)
            or nameof(MainViewModel.WeekCalendarDays)
            or nameof(MainViewModel.WeekTimelineColumns)
            or nameof(MainViewModel.WeekTimelineHourMarkers)
            or nameof(MainViewModel.WeekCanvasHeight)
            or nameof(MainViewModel.YearCalendarRows)
            or nameof(MainViewModel.MonthCalendarVisibility)
            or nameof(MainViewModel.WeekCalendarVisibility)
            or nameof(MainViewModel.YearCalendarVisibility)
            or nameof(MainViewModel.TimelineCanvasVisibility)
            or nameof(MainViewModel.TimelineViewport))
        {
            ApplyCalendarCellDimensions();
            ApplyWeekDayColumnWidths();
            ScheduleCalendarReflow();
            SetLoading(false);
        }
    }

    private void UpdateSelectionButtons()
    {
        App.DebugLog($"[TimelineWindow] UpdateSelectionButtons: Scale={_viewport.ScaleUnit}, Range={_viewport.RangeMode}");

        var syncPlan = TimelineRangeComboResolver.ResolvePlan(_viewport.ScaleUnit, _viewport.RangeMode, _configuredModes);
        if (RangeComboBox == null)
        {
            return;
        }
        if (syncPlan.ShouldRebuildOptions)
        {
            _isUpdatingRangeCombo = true;
            RangeComboBox.Items.Clear();
            foreach (var option in syncPlan.Options)
            {
                RangeComboBox.Items.Add(new ComboBoxItem
                {
                    Content = option.Label,
                    Tag = option.Mode,
                });
            }
            _configuredModes = syncPlan.Options.Select(option => option.Mode).ToArray();
            _isUpdatingRangeCombo = false;
        }

        if (RangeComboBox.SelectedIndex != syncPlan.SelectedIndex)
        {
            _isUpdatingRangeCombo = true;
            RangeComboBox.SelectedIndex = syncPlan.SelectedIndex;
            _isUpdatingRangeCombo = false;
        }
    }

    private void OnTimelineCanvasHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = Math.Max(320d, e.NewSize.Width);
        if (Math.Abs(width - ViewModel.TimelineCanvasWidth) < 0.5)
        {
            return;
        }

        ViewModel.TimelineCanvasWidth = width;
        _resizeDebounceTimer.Stop();
        _resizeDebounceTimer.Start();
    }

    private void OnNavigatePreviousClick(object sender, RoutedEventArgs e)
        => SafeUpdateViewport(_viewport with { AnchorLocal = ShiftAnchor(_viewport, -1) });

    private void OnNavigateTodayClick(object sender, RoutedEventArgs e)
        => SafeUpdateViewport(_viewport with { AnchorLocal = DateTimeOffset.Now.ToLocalTime() });

    private void OnNavigateNextClick(object sender, RoutedEventArgs e)
        => SafeUpdateViewport(_viewport with { AnchorLocal = ShiftAnchor(_viewport, 1) });

    private void OnViewDayClick(object sender, RoutedEventArgs e)
    {
        App.DebugLog("[TimelineWindow] OnViewDayClick called");
        SafeUpdateViewport(_viewport with { ScaleUnit = TimelineScaleUnit.Day, RangeMode = TimelineRangeMode.Day24 });
    }

    private void OnViewWeekClick(object sender, RoutedEventArgs e)
    {
        App.DebugLog("[TimelineWindow] OnViewWeekClick called");
        SafeUpdateViewport(_viewport with { ScaleUnit = TimelineScaleUnit.Week, RangeMode = TimelineRangeMode.Week1 });
    }

    private void OnViewMonthClick(object sender, RoutedEventArgs e)
    {
        App.DebugLog("[TimelineWindow] OnViewMonthClick called");
        _needsMonthInitialSizing = true;
        _lastMonthCellWidth = -1d;
        _lastMonthCellHeight = -1d;
        App.DebugLog("[TimelineWindow][MonthInit] Pending sizing requested by Month tab click");
        SafeUpdateViewport(_viewport with { ScaleUnit = TimelineScaleUnit.Month, RangeMode = TimelineRangeMode.Month1 });
        ScheduleCalendarReflow();
    }

    private void OnViewYearClick(object sender, RoutedEventArgs e)
    {
        App.DebugLog("[TimelineWindow] OnViewYearClick called");
        SafeUpdateViewport(_viewport with { ScaleUnit = TimelineScaleUnit.Month, RangeMode = TimelineRangeMode.Year1 });
    }

    private void OnRangeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingRangeCombo || sender is not ComboBox comboBox || comboBox.SelectedItem is not ComboBoxItem { Tag: TimelineRangeMode selectedMode })
        {
            return;
        }

        var nextScale = selectedMode switch
        {
            TimelineRangeMode.Day24 or TimelineRangeMode.AroundNow24 or TimelineRangeMode.SunriseToSunset or TimelineRangeMode.Custom => TimelineScaleUnit.Day,
            TimelineRangeMode.Week1 or TimelineRangeMode.Week2 or TimelineRangeMode.Week4 => TimelineScaleUnit.Week,
            _ => TimelineScaleUnit.Month,
        };

        SafeUpdateViewport(_viewport with
        {
            ScaleUnit = nextScale,
            RangeMode = selectedMode,
        });
    }

    private void WireToolbarControls()
    {
        if (ToolbarPanel?.Child is not Grid toolbarGrid)
        {
            return;
        }

        foreach (var button in EnumerateDescendants<Button>(toolbarGrid))
        {
            var column = Grid.GetColumn(button);
            switch (column)
            {
                case 0:
                    button.Click += OnNavigatePreviousClick;
                    break;
                case 1:
                    button.Click += OnNavigateTodayClick;
                    break;
                case 2:
                    button.Click += OnNavigateNextClick;
                    break;
                case 4:
                    button.Click += OnViewDayClick;
                    break;
                case 5:
                    button.Click += OnViewWeekClick;
                    break;
                case 6:
                    button.Click += OnViewMonthClick;
                    break;
                case 7:
                    button.Click += OnViewYearClick;
                    break;
                case 10:
                    button.Click += OnZoomOutClick;
                    break;
                case 11:
                    button.Click += OnZoomInClick;
                    break;
            }
        }

        RangeComboBox = EnumerateDescendants<ComboBox>(toolbarGrid).FirstOrDefault(combo => Grid.GetColumn(combo) == 9);
        if (RangeComboBox != null)
        {
            RangeComboBox.SelectionChanged += OnRangeSelectionChanged;
        }
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e)
        => SafeUpdateViewport(_viewport with { ZoomScale = Math.Min(_viewport.MaxZoomScale, _viewport.ZoomScale + 0.1) });

    private void OnZoomOutClick(object sender, RoutedEventArgs e)
        => SafeUpdateViewport(_viewport with { ZoomScale = Math.Max(_viewport.MinZoomScale, _viewport.ZoomScale - 0.1) });

    private void OnTimelineWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        ScrollViewer? scrollViewer = null;

        if (sender is ScrollViewer sv)
        {
            scrollViewer = sv;
        }
        else if (TimelineScrollViewer != null && ViewModel.TimelineCanvasVisibility == Visibility.Visible)
        {
            scrollViewer = TimelineScrollViewer;
        }
        else if (WeekTimelineScrollViewer != null && ViewModel.WeekCalendarVisibility == Visibility.Visible)
        {
            scrollViewer = WeekTimelineScrollViewer;
        }

        if (scrollViewer == null)
        {
            return;
        }

        var point = e.GetCurrentPoint(scrollViewer);
        if (point.Properties.IsHorizontalMouseWheel
            || !InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            return;
        }

        var delta = point.Properties.MouseWheelDelta > 0 ? 0.1 : -0.1;
        var oldZoom = _viewport.ZoomScale;
        var newZoom = Math.Clamp(_viewport.ZoomScale + delta, _viewport.MinZoomScale, _viewport.MaxZoomScale);
        if (Math.Abs(newZoom - oldZoom) < 0.0001)
        {
            e.Handled = true;
            return;
        }

        var oldCanvasHeight = scrollViewer == TimelineScrollViewer
            ? ViewModel.TimelineCanvasHeight
            : ViewModel.WeekCanvasHeight;
        double anchorOffset;
        try
        {
            anchorOffset = scrollViewer.VerticalOffset + point.Position.Y;
        }
        catch (COMException ex)
        {
            App.DebugLog($"[TimelineWindow] Failed to read vertical offset in wheel zoom: {ex.Message}");
            return;
        }

        var anchorRatio = oldCanvasHeight > 1 ? anchorOffset / oldCanvasHeight : 0.5;
        anchorRatio = Math.Clamp(anchorRatio, 0.0, 1.0);
        SafeUpdateViewport(_viewport with { ZoomScale = newZoom });

        var newCanvasHeight = scrollViewer == TimelineScrollViewer
            ? ViewModel.TimelineCanvasHeight
            : ViewModel.WeekCanvasHeight;
        var targetOffset = Math.Max(0, (newCanvasHeight * anchorRatio) - point.Position.Y);
        try
        {
            scrollViewer.ChangeView(null, targetOffset, null, disableAnimation: true);
        }
        catch (COMException ex)
        {
            App.DebugLog($"[TimelineWindow] ChangeView failed in wheel zoom: {ex.Message}");
        }

        e.Handled = true;
    }

    private async void OnTimelineBlockEditRequested(string tileId)
        => await OpenEditTileAsync(tileId);

    private async void OnTimelinePromptRequested(string tileId)
        => await RequestPromptForTileAsync(tileId);

    private async Task OpenEditTileAsync(string tileId)
    {
        if (string.IsNullOrWhiteSpace(tileId))
        {
            return;
        }

        try
        {
            var freshTile = await _api.GetEditableTileByIdAsync(tileId);
            if (freshTile == null)
            {
                return;
            }

            var editTileId = freshTile.Id;
            var createWindow = new CreateTileWindow(editTileId, freshTile);
            createWindow.Closed += (_, _) => _ = ViewModel.RefreshAsync(forcePublish: true);
            FloatingWindowHelper.CenterOnQuickPanelDisplay(createWindow, _settings.Current);
            createWindow.Activate();
        }
        catch (Exception ex)
        {
            App.DebugLog($"[TimelineWindow] Failed to open edit window: {ex.Message}");
        }
    }

    private async Task RequestPromptForTileAsync(string tileId)
    {
        try
        {
            var response = await _api.RequestPromptAsync(tileId);
            if (response?.Ok != true || response.Prompt == null)
            {
                return;
            }

            _promptToast.ShowPrompt(
                response.Prompt,
                5,
                async (actionId, stopAt) =>
                {
                    _promptToast.Hide();
                    var dispatch = await PromptActionDispatcher.ExecuteAsync(
                        _api,
                        response.Prompt,
                        actionId,
                        stopAt,
                        fallbackTileId: tileId,
                        defaultBreakMinutes: _settings.Current.DefaultBreakMinutes);
                    if (!dispatch.IsResolved)
                    {
                        App.DebugLog($"[TimelineWindow] Unknown prompt action: {actionId}");
                    }
                    else if (!string.IsNullOrWhiteSpace(dispatch.Error))
                    {
                        App.DebugLog($"[TimelineWindow] Prompt action failed: {dispatch.ResolvedActionId}, error: {dispatch.Error}");
                    }

                    await ViewModel.RefreshAsync();
                });
        }
        catch (Exception ex)
        {
            App.DebugLog($"[TimelineWindow] RequestPromptForTileAsync error: {ex.Message}");
        }
    }

    private void SafeUpdateViewport(TimelineViewportSettings viewport)
    {
        try
        {
            SetLoading(true);
            UpdateViewport(viewport);
        }
        catch (COMException ex)
        {
            App.DebugLog($"[TimelineWindow] Scope update failed: {ex.Message}");
            SetLoading(false);
        }
    }

    private void UpdateViewport(TimelineViewportSettings viewport)
    {
        _viewport = viewport;
        App.DebugLog($"[TimelineWindow] UpdateViewport: ScaleUnit={viewport.ScaleUnit}, RangeMode={viewport.RangeMode}");
        UpdateSelectionButtons();
        ViewModel.UpdateTimelineViewport(_viewport);
        ApplyCalendarCellDimensions();
        ScheduleCalendarReflow();
        ScrollTimelineToNow();
    }

    private void ScheduleCalendarReflow()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TimelineRootGrid?.UpdateLayout();
            ApplyCalendarCellDimensions();
            ApplyWeekDayColumnWidths();
        });
    }

    private void ScrollTimelineToNow()
    {
        if (TimelineScrollViewer == null
            || ViewModel.TimelineNowVisibility != Visibility.Visible
            || ViewModel.TimelineCanvasVisibility != Visibility.Visible)
        {
            return;
        }

        double target;
        try
        {
            target = Math.Max(0, ViewModel.TimelineNowTop - (TimelineScrollViewer.ViewportHeight * 0.35));
        }
        catch (COMException ex)
        {
            App.DebugLog($"[TimelineWindow] Failed to read viewport during scope sync: {ex.Message}");
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (TimelineScrollViewer == null
                || ViewModel.TimelineNowVisibility != Visibility.Visible
                || ViewModel.TimelineCanvasVisibility != Visibility.Visible)
            {
                return;
            }

            try
            {
                TimelineScrollViewer.ChangeView(null, target, null, disableAnimation: true);
            }
            catch (COMException ex)
            {
                App.DebugLog($"[TimelineWindow] ChangeView failed while syncing now marker: {ex.Message}");
            }
        });
    }

    private static DateTimeOffset ShiftAnchor(TimelineViewportSettings viewport, int direction)
    {
        var anchor = viewport.AnchorLocal == default ? DateTimeOffset.Now.ToLocalTime() : viewport.AnchorLocal.ToLocalTime();
        return viewport.RangeMode switch
        {
            TimelineRangeMode.Day24 or TimelineRangeMode.AroundNow24 or TimelineRangeMode.SunriseToSunset or TimelineRangeMode.Custom => anchor.AddDays(direction),
            TimelineRangeMode.Week1 => anchor.AddDays(7 * direction),
            TimelineRangeMode.Week2 => anchor.AddDays(14 * direction),
            TimelineRangeMode.Week4 => anchor.AddDays(28 * direction),
            TimelineRangeMode.Month1 => anchor.AddMonths(direction),
            TimelineRangeMode.Month3 => anchor.AddMonths(3 * direction),
            TimelineRangeMode.Month6 => anchor.AddMonths(6 * direction),
            TimelineRangeMode.Year1 => anchor.AddYears(direction),
            _ => anchor.AddMonths(direction),
        };
    }

    private void SetLoading(bool isLoading)
    {
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnMonthCalendarHostLayoutUpdated(object? sender, object e)
    {
        if (!_needsMonthInitialSizing || ViewModel.MonthCalendarVisibility != Visibility.Visible)
        {
            return;
        }

        var hasRealizedMonthCell = EnumerateDescendantBorders(MonthCalendarHost)
            .Any(border => border.Tag is string tag && string.Equals(tag, "MonthCell", StringComparison.Ordinal));
        if (!hasRealizedMonthCell)
        {
            return;
        }

        App.DebugLog($"[TimelineWindow][MonthInit] LayoutUpdated apply: host={MonthCalendarHost.ActualWidth:F1}x{MonthCalendarHost.ActualHeight:F1}");
        ApplyCalendarCellDimensions();
        _needsMonthInitialSizing = false;
        App.DebugLog("[TimelineWindow][MonthInit] Initial sizing completed");
    }

    private void ApplyCalendarCellDimensions()
    {
        var monthHostWidth = MonthCalendarHost.Parent is FrameworkElement monthParent && monthParent.ActualWidth > 0
            ? monthParent.ActualWidth
            : MonthCalendarHost.ActualWidth;
        var monthHostHeight = MonthCalendarHost.Parent is FrameworkElement monthParentForHeight && monthParentForHeight.ActualHeight > 0
            ? monthParentForHeight.ActualHeight
            : MonthCalendarHost.ActualHeight;
        var monthCellWidth = monthHostWidth > 0 ? Math.Max(110d, (monthHostWidth - (8d * 6d)) / 7d) : 0d;
        var monthCellHeight = monthHostHeight > 0 ? Math.Max(48d, (monthHostHeight - 80d) / 6d) : 0d;
        var monthCellBorders = ViewModel.MonthCalendarVisibility == Visibility.Visible
            ? EnumerateDescendantBorders(MonthCalendarHost)
                .Where(border => border.Tag is string tag && string.Equals(tag, "MonthCell", StringComparison.Ordinal))
                .ToArray()
            : [];
        var monthCellsRealized = monthCellBorders.Length > 0;

        // var weekCellWidth = WeekCalendarHost.ActualWidth > 0 ? Math.Max(110d, (WeekCalendarHost.ActualWidth - (8d * 6d)) / 7d) : 0d; // Removed - week uses unified timeline
        // var weekCellHeight = Math.Max(180d, ViewModel.WeekCanvasHeight + 48d); // Removed - week uses unified timeline
        var yearMonthWidth = YearCalendarHost.ActualWidth > 0 ? Math.Max(210d, (YearCalendarHost.ActualWidth - (10d * 3d)) / 4d) : 0d;
        var yearMonthHeight = YearCalendarHost.ActualHeight > 0 ? Math.Max(170d, (YearCalendarHost.ActualHeight - (10d * 2d)) / 3d) : 0d;
        var yearDayWidth = yearMonthWidth > 0 ? Math.Max(24d, (yearMonthWidth - 16d - (2d * 6d)) / 7d) : 0d;

        if (ViewModel.MonthCalendarVisibility == Visibility.Visible && (!monthCellsRealized || monthCellWidth <= 0d || monthCellHeight <= 0d))
        {
            App.DebugLog(
                $"[TimelineWindow][MonthInit] Skip apply before month cells ready: host={monthHostWidth:F1}x{monthHostHeight:F1}, cell={monthCellWidth:F1}x{monthCellHeight:F1}, cells={monthCellBorders.Length}");
            return;
        }

        if (Math.Abs(monthCellWidth - _lastMonthCellWidth) < 0.5
            && Math.Abs(monthCellHeight - _lastMonthCellHeight) < 0.5
            && Math.Abs(yearMonthWidth - _lastYearMonthWidth) < 0.5
            && Math.Abs(yearMonthHeight - _lastYearMonthHeight) < 0.5
            && Math.Abs(yearDayWidth - _lastYearDayWidth) < 0.5)
        {
            return;
        }

        if (ViewModel.MonthCalendarVisibility == Visibility.Visible
            && (Math.Abs(monthCellWidth - _lastLoggedMonthCellWidth) >= 0.5 || Math.Abs(monthCellHeight - _lastLoggedMonthCellHeight) >= 0.5 || _monthInitLogCount < 3))
        {
            _monthInitLogCount++;
            _lastLoggedMonthCellWidth = monthCellWidth;
            _lastLoggedMonthCellHeight = monthCellHeight;
            App.DebugLog(
                $"[TimelineWindow][MonthInit] Metrics#{_monthInitLogCount}: host={monthHostWidth:F1}x{monthHostHeight:F1}, cell={monthCellWidth:F1}x{monthCellHeight:F1}, cells={monthCellBorders.Length}, pending={_needsMonthInitialSizing}");
        }

        _lastMonthCellWidth = monthCellWidth;
        _lastMonthCellHeight = monthCellHeight;
        // _lastWeekCellWidth = weekCellWidth; // Removed - week uses unified timeline
        // _lastWeekCellHeight = weekCellHeight; // Removed - week uses unified timeline
        _lastYearMonthWidth = yearMonthWidth;
        _lastYearMonthHeight = yearMonthHeight;
        _lastYearDayWidth = yearDayWidth;

        if (ViewModel.MonthCalendarVisibility == Visibility.Visible && monthCellWidth > 0d && monthCellHeight > 0d)
        {
            foreach (var border in monthCellBorders)
            {
                border.Width = monthCellWidth;
                border.Height = monthCellHeight;
            }
        }

        // Week calendar now uses unified timeline - no cell dimension adjustment needed
        /*
        if (ViewModel.WeekCalendarVisibility == Visibility.Visible)
        {
            foreach (var border in EnumerateDescendantBorders(WeekCalendarHost))
            {
                if (border.Tag is string tag && string.Equals(tag, "WeekTimelineDayColumn", StringComparison.Ordinal))
                {
                    border.Width = weekCellWidth;
                    border.Height = weekCellHeight;
                }
            }
        }
        */

        if (ViewModel.YearCalendarVisibility == Visibility.Visible)
        {
            foreach (var border in EnumerateDescendantBorders(YearCalendarHost))
            {
                if (border.Tag is string tag && string.Equals(tag, "YearMonthCard", StringComparison.Ordinal))
                {
                    border.Width = yearMonthWidth;
                    border.Height = yearMonthHeight;
                    continue;
                }

                if (border.Tag is string dayTag && string.Equals(dayTag, "YearDayCell", StringComparison.Ordinal))
                {
                    border.Width = yearDayWidth;
                }
            }
        }

        ApplyWeekDayColumnWidths();
    }

    private void ApplyWeekDayColumnWidths()
    {
        if (WeekTimelineScrollViewer is null || ViewModel.WeekCalendarVisibility != Visibility.Visible)
        {
            return;
        }

        var availableWidth = WeekTimelineScrollViewer.ActualWidth;
        if (availableWidth <= 0)
        {
            return;
        }

        const int dayCount = 7;
        const double gap = 8d;
        var totalGap = (dayCount - 1) * gap;
        var width = Math.Max(110d, (availableWidth - totalGap) / dayCount);
        if (Math.Abs(ViewModel.WeekDayColumnWidth - width) > 0.5d)
        {
            ViewModel.WeekDayColumnWidth = width;
            ViewModel.ReflowWeekTimelineColumnsForWidth();
        }
    }

    private static IEnumerable<Border> EnumerateDescendantBorders(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Border border)
            {
                yield return border;
            }

            foreach (var descendant in EnumerateDescendantBorders(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<TControl> EnumerateDescendants<TControl>(DependencyObject root) where TControl : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TControl control)
            {
                yield return control;
            }

            foreach (var nested in EnumerateDescendants<TControl>(child))
            {
                yield return nested;
            }
        }
    }
}
