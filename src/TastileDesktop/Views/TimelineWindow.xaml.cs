using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Input;
using TastileDesktop.Models;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;
using Windows.Foundation;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace TastileDesktop.Views;

public sealed partial class TimelineWindow : Window
{
    private sealed record RangeOption(TimelineRangeMode Mode, string Label);

    public MainViewModel ViewModel { get; } = new();
    private readonly CoreApiClient _api = new();
    private readonly PromptToastDisplayService _promptToast = PromptToastDisplayService.Instance;
    private TimelineViewportSettings _viewport = new(
        ScaleUnit: TimelineScaleUnit.Day,
        RangeMode: TimelineRangeMode.Day24,
        AnchorLocal: DateTimeOffset.Now.ToLocalTime());
    private bool _isUpdatingRangeCombo;
    private IReadOnlyList<RangeOption> _rangeOptions = [];

    public TimelineWindow()
    {
        InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 920, 700);
        var now = DateTimeOffset.Now.ToLocalTime();
        var start = now;
        var end = now.AddHours(1);
        CustomStartDatePicker.Date = new DateTimeOffset(start.Year, start.Month, start.Day, 0, 0, 0, start.Offset);
        CustomStartTimePicker.Time = now.TimeOfDay;
        CustomEndDatePicker.Date = new DateTimeOffset(end.Year, end.Month, end.Day, 0, 0, 0, end.Offset);
        CustomEndTimePicker.Time = end.TimeOfDay;
        UpdateSelectionButtons();
        TimelineCanvasHost.SizeChanged += OnTimelineCanvasHostSizeChanged;
        ViewModel.TimelineCanvasWidth = Math.Max(280d, TimelineCanvasHost.ActualWidth);
        ViewModel.UpdateTimelineViewport(_viewport);
        _ = ViewModel.InitializeAsync();
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e) => UpdateViewport(_viewport with { ZoomScale = Math.Min(_viewport.MaxZoomScale, _viewport.ZoomScale + 0.1) });
    private void OnZoomOutClick(object sender, RoutedEventArgs e) => UpdateViewport(_viewport with { ZoomScale = Math.Max(_viewport.MinZoomScale, _viewport.ZoomScale - 0.1) });

    private void OnTimelineWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer) return;
        var point = e.GetCurrentPoint(scrollViewer);
        if (!point.Properties.IsHorizontalMouseWheel && (InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)))
        {
            var delta = point.Properties.MouseWheelDelta > 0 ? 0.1 : -0.1;
            var oldZoom = _viewport.ZoomScale;
            var newZoom = Math.Clamp(_viewport.ZoomScale + delta, _viewport.MinZoomScale, _viewport.MaxZoomScale);
            if (Math.Abs(newZoom - oldZoom) < 0.0001)
            {
                e.Handled = true;
                return;
            }

            var oldCanvasHeight = ViewModel.TimelineCanvasHeight;
            var anchorOffset = scrollViewer.VerticalOffset + point.Position.Y;
            var anchorRatio = oldCanvasHeight > 1 ? anchorOffset / oldCanvasHeight : 0.5;
            anchorRatio = Math.Clamp(anchorRatio, 0.0, 1.0);

            UpdateViewport(_viewport with { ZoomScale = newZoom });

            var newCanvasHeight = ViewModel.TimelineCanvasHeight;
            var targetOffset = Math.Max(0, (newCanvasHeight * anchorRatio) - point.Position.Y);
            scrollViewer.ChangeView(null, targetOffset, null, disableAnimation: true);
            e.Handled = true;
        }
    }

    private void UpdateViewport(TimelineViewportSettings viewport)
    {
        _viewport = viewport with { AnchorLocal = DateTimeOffset.Now.ToLocalTime() };
        UpdateSelectionButtons();
        ViewModel.UpdateTimelineViewport(_viewport);
        ScrollTimelineToNow();
    }

    private void UpdateSelectionButtons()
    {
        ScaleComboBox.SelectedIndex = _viewport.ScaleUnit switch
        {
            TimelineScaleUnit.Day => 0,
            TimelineScaleUnit.Week => 1,
            _ => 2,
        };

        _isUpdatingRangeCombo = true;
        _rangeOptions = ResolveRangeOptions(_viewport.ScaleUnit);
        RangeComboBox.Items.Clear();
        foreach (var option in _rangeOptions)
        {
            RangeComboBox.Items.Add(new ComboBoxItem
            {
                Content = option.Label,
                Tag = option.Mode,
            });
        }

        var selectedIndex = -1;
        for (var i = 0; i < _rangeOptions.Count; i++)
        {
            if (_rangeOptions[i].Mode == _viewport.RangeMode)
            {
                selectedIndex = i;
                break;
            }
        }
        if (selectedIndex < 0 || selectedIndex >= _rangeOptions.Count)
        {
            selectedIndex = 0;
        }
        RangeComboBox.SelectedIndex = selectedIndex;
        _isUpdatingRangeCombo = false;
    }

    private void OnScaleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScaleComboBox.SelectedIndex < 0) return;
        var next = ScaleComboBox.SelectedIndex switch
        {
            0 => TimelineScaleUnit.Day,
            1 => TimelineScaleUnit.Week,
            _ => TimelineScaleUnit.Month,
        };
        if (next != _viewport.ScaleUnit)
        {
            var nextOptions = ResolveRangeOptions(next);
            var nextRangeMode = nextOptions.Any(option => option.Mode == _viewport.RangeMode)
                ? _viewport.RangeMode
                : nextOptions[0].Mode;
            UpdateViewport(_viewport with
            {
                ScaleUnit = next,
                RangeMode = nextRangeMode,
            });
        }
    }

    private void OnRangeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingRangeCombo || RangeComboBox.SelectedIndex < 0) return;
        var selectedMode = RangeComboBox.SelectedItem switch
        {
            ComboBoxItem { Tag: TimelineRangeMode mode } => mode,
            _ => _rangeOptions.ElementAtOrDefault(RangeComboBox.SelectedIndex)?.Mode ?? TimelineRangeMode.Day24,
        };

        if (selectedMode != _viewport.RangeMode)
        {
            UpdateViewport(_viewport with { RangeMode = selectedMode });
        }
    }

    private void OnApplyCustomRangeClick(object sender, RoutedEventArgs e)
    {
        var startDate = CustomStartDatePicker.Date.LocalDateTime.Date + CustomStartTimePicker.Time;
        var endDate = CustomEndDatePicker.Date.LocalDateTime.Date + CustomEndTimePicker.Time;
        var start = new DateTimeOffset(startDate, TimeZoneInfo.Local.GetUtcOffset(startDate));
        var end = new DateTimeOffset(endDate, TimeZoneInfo.Local.GetUtcOffset(endDate));
        UpdateViewport(_viewport with
        {
            ScaleUnit = TimelineScaleUnit.Day,
            RangeMode = TimelineRangeMode.Custom,
            CustomStartLocal = start,
            CustomEndLocal = end,
        });
    }

    private void OnTimelineCanvasHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = Math.Max(280d, e.NewSize.Width);
        if (Math.Abs(width - ViewModel.TimelineCanvasWidth) < 0.5) return;
        ViewModel.TimelineCanvasWidth = width;
        ViewModel.UpdateTimelineViewport(_viewport);
        ScrollTimelineToNow();
    }

    private void ScrollTimelineToNow()
    {
        if (TimelineScrollViewer == null || ViewModel.TimelineNowVisibility != Visibility.Visible)
        {
            return;
        }

        var target = Math.Max(0, ViewModel.TimelineNowTop - (TimelineScrollViewer.ViewportHeight * 0.35));
        DispatcherQueue.TryEnqueue(() =>
        {
            if (TimelineScrollViewer == null || ViewModel.TimelineNowVisibility != Visibility.Visible)
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

    private static IReadOnlyList<RangeOption> ResolveRangeOptions(TimelineScaleUnit scaleUnit)
    {
        return scaleUnit switch
        {
            TimelineScaleUnit.Day =>
            [
                new RangeOption(TimelineRangeMode.Day24, "24h"),
                new RangeOption(TimelineRangeMode.AroundNow24, "±12h"),
                new RangeOption(TimelineRangeMode.SunriseToSunset, "Sun"),
                new RangeOption(TimelineRangeMode.Custom, "Custom"),
            ],
            TimelineScaleUnit.Week =>
            [
                new RangeOption(TimelineRangeMode.Week1, "1w"),
                new RangeOption(TimelineRangeMode.Week2, "2w"),
                new RangeOption(TimelineRangeMode.Week4, "4w"),
            ],
            _ =>
            [
                new RangeOption(TimelineRangeMode.Month1, "1m"),
                new RangeOption(TimelineRangeMode.Month3, "3m"),
                new RangeOption(TimelineRangeMode.Month6, "6m"),
                new RangeOption(TimelineRangeMode.Year1, "1y"),
            ],
        };
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
                        var dispatch = await PromptActionDispatcher.ExecuteAsync(
                            _api,
                            response.Prompt,
                            actionId,
                            stopAt,
                            fallbackTileId: tileId);
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
        }
        catch (Exception ex)
        {
            App.DebugLog($"[TimelineWindow] RequestPromptForTileAsync error: {ex.Message}");
        }
    }
}
