using Microsoft.UI.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace TastileDesktop.Views;

public sealed partial class TimelineWindow : Window
{
    public MainViewModel ViewModel { get; } = new();
    private readonly CoreApiClient _api = new();
    private readonly SettingsService _settings = new();
    private readonly PromptToastDisplayService _promptToast = PromptToastDisplayService.Instance;

    private TimelineViewportSettings _viewport = new(
        ScaleUnit: TimelineScaleUnit.Month,
        RangeMode: TimelineRangeMode.Month1,
        AnchorLocal: DateTimeOffset.Now.ToLocalTime());

    private bool _isUpdatingRangeCombo;
    private IReadOnlyList<TimelineRangeMode> _configuredModes = [];
    private readonly DispatcherQueueTimer _resizeDebounceTimer;
    private double _lastMonthCellWidth;
    private double _lastMonthCellHeight;
    private double _lastWeekCellWidth;
    private double _lastWeekCellHeight;
    private double _lastYearMonthWidth;
    private double _lastYearMonthHeight;
    private double _lastYearDayWidth;

    public TimelineWindow()
    {
        InitializeComponent();
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
        WeekCalendarHost.SizeChanged += (_, _) => ApplyCalendarCellDimensions();
        YearCalendarHost.SizeChanged += (_, _) => ApplyCalendarCellDimensions();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) => ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        ViewModel.TimelineCanvasWidth = Math.Max(320d, TimelineCanvasHost.ActualWidth);
        UpdateSelectionButtons();
        SyncTimelineItemsBindings();
        SetLoading(true);
        ViewModel.UpdateTimelineViewport(_viewport);
        _ = ViewModel.InitializeAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.TimelineHourMarkers)
            or nameof(MainViewModel.TimelineBlocks)
            or nameof(MainViewModel.TimelineCanvasHeight)
            or nameof(MainViewModel.MonthCalendarRows)
            or nameof(MainViewModel.WeekCalendarDays)
            or nameof(MainViewModel.YearCalendarRows))
        {
            SyncTimelineItemsBindings();
            ApplyCalendarCellDimensions();
            SetLoading(false);
        }
    }

    private void SyncTimelineItemsBindings()
    {
        HourMarkersItemsControl.ItemsSource = ViewModel.TimelineHourMarkers;
        TimelineBlocksItemsControl.ItemsSource = ViewModel.TimelineBlocks;
        HourMarkersItemsControl.Height = ViewModel.TimelineCanvasHeight;
        TimelineBlocksItemsControl.Height = ViewModel.TimelineCanvasHeight;
    }

    private void UpdateSelectionButtons()
    {
        DayViewToggle.IsChecked = _viewport.ScaleUnit == TimelineScaleUnit.Day;
        WeekViewToggle.IsChecked = _viewport.ScaleUnit == TimelineScaleUnit.Week;
        MonthViewToggle.IsChecked = _viewport.ScaleUnit == TimelineScaleUnit.Month && _viewport.RangeMode != TimelineRangeMode.Year1;
        YearViewToggle.IsChecked = _viewport.RangeMode == TimelineRangeMode.Year1;

        var syncPlan = TimelineRangeComboResolver.ResolvePlan(_viewport.ScaleUnit, _viewport.RangeMode, _configuredModes);
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
        => SafeUpdateViewport(_viewport with { ScaleUnit = TimelineScaleUnit.Day, RangeMode = TimelineRangeMode.Day24 });

    private void OnViewWeekClick(object sender, RoutedEventArgs e)
        => SafeUpdateViewport(_viewport with { ScaleUnit = TimelineScaleUnit.Week, RangeMode = TimelineRangeMode.Week1 });

    private void OnViewMonthClick(object sender, RoutedEventArgs e)
        => SafeUpdateViewport(_viewport with { ScaleUnit = TimelineScaleUnit.Month, RangeMode = TimelineRangeMode.Month1 });

    private void OnViewYearClick(object sender, RoutedEventArgs e)
        => SafeUpdateViewport(_viewport with { ScaleUnit = TimelineScaleUnit.Month, RangeMode = TimelineRangeMode.Year1 });

    private void OnRangeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingRangeCombo || RangeComboBox.SelectedItem is not ComboBoxItem { Tag: TimelineRangeMode selectedMode })
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

    private void OnZoomInClick(object sender, RoutedEventArgs e)
        => SafeUpdateViewport(_viewport with { ZoomScale = Math.Min(_viewport.MaxZoomScale, _viewport.ZoomScale + 0.1) });

    private void OnZoomOutClick(object sender, RoutedEventArgs e)
        => SafeUpdateViewport(_viewport with { ZoomScale = Math.Max(_viewport.MinZoomScale, _viewport.ZoomScale - 0.1) });

    private void OnTimelineWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
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

        var oldCanvasHeight = ViewModel.TimelineCanvasHeight;
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

        var newCanvasHeight = ViewModel.TimelineCanvasHeight;
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

    private async void OnTimelineBlockStatusClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tileId || string.IsNullOrWhiteSpace(tileId))
        {
            return;
        }

        var block = button.DataContext as TimelineAbsoluteBlockViewModel;
        var lifecycle = block?.Lifecycle;
        var decision = TimelineStatusActionResolver.Resolve(tileId, lifecycle);
        if (decision.Kind != TimelineStatusActionKind.RequestPrompt || string.IsNullOrWhiteSpace(decision.TileId))
        {
            return;
        }

        await RequestPromptForTileAsync(decision.TileId);
    }

    private async void OnTimelineBlockEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tileId || string.IsNullOrWhiteSpace(tileId))
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

            var createWindow = new CreateTileWindow(tileId, freshTile);
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
        UpdateSelectionButtons();
        ViewModel.UpdateTimelineViewport(_viewport);
        SyncTimelineItemsBindings();
        ApplyCalendarCellDimensions();
        ScrollTimelineToNow();
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

    private void ApplyCalendarCellDimensions()
    {
        var monthCellWidth = MonthCalendarHost.ActualWidth > 0 ? Math.Max(110d, (MonthCalendarHost.ActualWidth - (8d * 6d)) / 7d) : 0d;
        var monthCellHeight = MonthCalendarHost.ActualHeight > 0 ? Math.Max(80d, (MonthCalendarHost.ActualHeight - 80d) / 6d) : 0d;
        var weekCellWidth = WeekCalendarHost.ActualWidth > 0 ? Math.Max(110d, (WeekCalendarHost.ActualWidth - (8d * 6d)) / 7d) : 0d;
        var weekCellHeight = WeekCalendarHost.ActualHeight > 0 ? Math.Max(140d, WeekCalendarHost.ActualHeight - 56d) : 0d;
        var yearMonthWidth = YearCalendarHost.ActualWidth > 0 ? Math.Max(210d, (YearCalendarHost.ActualWidth - (10d * 3d)) / 4d) : 0d;
        var yearMonthHeight = YearCalendarHost.ActualHeight > 0 ? Math.Max(170d, (YearCalendarHost.ActualHeight - (10d * 2d)) / 3d) : 0d;
        var yearDayWidth = yearMonthWidth > 0 ? Math.Max(24d, (yearMonthWidth - 16d - (2d * 6d)) / 7d) : 0d;

        if (Math.Abs(monthCellWidth - _lastMonthCellWidth) < 0.5
            && Math.Abs(monthCellHeight - _lastMonthCellHeight) < 0.5
            && Math.Abs(weekCellWidth - _lastWeekCellWidth) < 0.5
            && Math.Abs(weekCellHeight - _lastWeekCellHeight) < 0.5
            && Math.Abs(yearMonthWidth - _lastYearMonthWidth) < 0.5
            && Math.Abs(yearMonthHeight - _lastYearMonthHeight) < 0.5
            && Math.Abs(yearDayWidth - _lastYearDayWidth) < 0.5)
        {
            return;
        }

        _lastMonthCellWidth = monthCellWidth;
        _lastMonthCellHeight = monthCellHeight;
        _lastWeekCellWidth = weekCellWidth;
        _lastWeekCellHeight = weekCellHeight;
        _lastYearMonthWidth = yearMonthWidth;
        _lastYearMonthHeight = yearMonthHeight;
        _lastYearDayWidth = yearDayWidth;

        if (ViewModel.MonthCalendarVisibility == Visibility.Visible)
        {
            foreach (var border in EnumerateDescendantBorders(MonthCalendarHost))
            {
                if (border.Tag is string tag && string.Equals(tag, "MonthCell", StringComparison.Ordinal))
                {
                    border.Width = monthCellWidth;
                    border.Height = monthCellHeight;
                }
            }
        }

        if (ViewModel.WeekCalendarVisibility == Visibility.Visible)
        {
            foreach (var border in EnumerateDescendantBorders(WeekCalendarHost))
            {
                if (border.Tag is string tag && string.Equals(tag, "WeekCell", StringComparison.Ordinal))
                {
                    border.Width = weekCellWidth;
                    border.Height = weekCellHeight;
                }
            }
        }

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
}
