using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Input;
using TastileDesktop.Models;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;
using Windows.Foundation;

namespace TastileDesktop.Views;

public sealed partial class TimelineWindow : Window
{
    public MainViewModel ViewModel { get; } = new();
    private TimelineViewportSettings _viewport = new(
        ScaleUnit: TimelineScaleUnit.Day,
        RangeMode: TimelineRangeMode.Day24,
        AnchorLocal: DateTimeOffset.Now.ToLocalTime());

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
        RangeComboBox.SelectedIndex = _viewport.RangeMode switch
        {
            TimelineRangeMode.Day24 => 0,
            TimelineRangeMode.AroundNow24 => 1,
            _ => 2,
        };
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
        if (next != _viewport.ScaleUnit) UpdateViewport(_viewport with { ScaleUnit = next });
    }

    private void OnRangeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RangeComboBox.SelectedIndex < 0) return;
        var next = RangeComboBox.SelectedIndex switch
        {
            0 => TimelineRangeMode.Day24,
            1 => TimelineRangeMode.AroundNow24,
            _ => TimelineRangeMode.SunriseToSunset,
        };
        if (next != _viewport.RangeMode) UpdateViewport(_viewport with { RangeMode = next });
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
        TimelineScrollViewer.ChangeView(null, target, null, disableAnimation: true);
    }
}
