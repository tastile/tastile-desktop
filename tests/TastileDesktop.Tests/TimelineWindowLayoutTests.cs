using System.IO;
using Xunit;

namespace TastileDesktop.Tests;

public sealed class TimelineWindowLayoutTests
{
    [Fact]
    public void TimelineWindow_UsesClassicBinding_ForRootItemsControlsToAvoidXBindConnectorCrash()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("x:Name=\"HourMarkersItemsControl\"", xaml);
        Assert.Contains("x:Name=\"TimelineBlocksItemsControl\"", xaml);
        Assert.Contains("SyncTimelineItemsBindings()", source);
        Assert.Contains("HourMarkersItemsControl.ItemsSource = ViewModel.TimelineHourMarkers;", source);
        Assert.Contains("TimelineBlocksItemsControl.ItemsSource = ViewModel.TimelineBlocks;", source);
        Assert.DoesNotContain("ItemsSource=\"{x:Bind ViewModel.TimelineHourMarkers, Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("ItemsSource=\"{x:Bind ViewModel.TimelineBlocks, Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void TimelineWindow_UsesCanvasItemContainerBindings_ForMarkerAndBlockPositioning()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("x:Name=\"HourMarkersItemsControl\"", xaml);
        Assert.Contains("x:Name=\"TimelineBlocksItemsControl\"", xaml);

        Assert.Contains("TranslateTransform Y=\"{Binding Top}\"", xaml);
        Assert.Contains("TranslateTransform X=\"{Binding Left}\" Y=\"{Binding Top}\"", xaml);
    }

    [Fact]
    public void TimelineWindow_UsesButtonStatusAffordance_InsteadOfPassiveIcon()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("Click=\"OnTimelineBlockStatusClick\"", xaml);
        Assert.Contains("Click=\"OnTimelineBlockEditClick\"", xaml);
        Assert.DoesNotContain("ToolTipService.ToolTip=\"{x:Bind StatusIconToolTip}\"", xaml);
    }

    [Fact]
    public void TimelineWindow_StatusClick_DelegatesLifecycleDecisionToResolver()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("var lifecycle = block?.Lifecycle;", source);
        Assert.Contains("TimelineStatusActionResolver.Resolve(tileId, lifecycle)", source);
        Assert.Contains("private async void OnTimelineBlockEditClick", source);
        Assert.Contains("new CreateTileWindow(tileId, freshTile)", source);
        Assert.DoesNotContain("if (lifecycle == \"done\")", source);
    }

    [Fact]
    public void TimelineWindow_CentersBlockTextAndHidesScheduledKindLabel()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("StackPanel Grid.Column=\"1\" Spacing=\"2\" VerticalAlignment=\"Center\"", xaml);
        Assert.Contains("Text=\"{Binding DurationText}\" Foreground=\"{Binding SecondaryForegroundBrush}\" VerticalAlignment=\"Center\"", xaml);
        Assert.Contains("Visibility=\"{Binding KindLabelVisibility}\"", xaml);
    }

    [Fact]
    public void TimelineWindow_ToolbarControls_UseSafeUpdatePath_WithComGuard()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("SafeUpdateViewport(_viewport with", source);
        Assert.Contains("private void SafeUpdateViewport(TimelineViewportSettings viewport)", source);
        Assert.Contains("[TimelineWindow] Scope update failed:", source);
        Assert.Contains("OnNavigatePreviousClick", source);
        Assert.Contains("OnNavigateTodayClick", source);
        Assert.Contains("OnNavigateNextClick", source);
        Assert.Contains("OnViewDayClick", source);
        Assert.Contains("OnViewWeekClick", source);
        Assert.Contains("OnViewMonthClick", source);
        Assert.Contains("OnZoomInClick", source);
        Assert.Contains("OnZoomOutClick", source);
        Assert.Contains("SafeUpdateViewport(_viewport with { ZoomScale", source);
        Assert.Contains("[TimelineWindow] Failed to read vertical offset in wheel zoom:", source);
    }

    [Fact]
    public void TimelineWindow_RangeCombo_RebuildsWhenViewChanges()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("TimelineRangeComboResolver.ResolvePlan(", source);
        Assert.Contains("if (syncPlan.ShouldRebuildOptions)", source);
        Assert.Contains("RangeComboBox.SelectedIndex != syncPlan.SelectedIndex", source);
    }

    [Fact]
    public void TimelineWindow_HasGoogleLikeMonthGridSurface()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("x:Name=\"ToolbarPanel\"", xaml);
        Assert.Contains("Click=\"OnNavigatePreviousClick\"", xaml);
        Assert.Contains("Click=\"OnNavigateTodayClick\"", xaml);
        Assert.Contains("Click=\"OnNavigateNextClick\"", xaml);
        Assert.Contains("Click=\"OnViewDayClick\"", xaml);
        Assert.Contains("Click=\"OnViewWeekClick\"", xaml);
        Assert.Contains("Click=\"OnViewMonthClick\"", xaml);
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.MonthCalendarRows, Mode=OneWay}\"", xaml);
        Assert.Contains("Text=\"Mon\"", xaml);
        Assert.Contains("Text=\"Tue\"", xaml);
        Assert.Contains("Text=\"Wed\"", xaml);
        Assert.Contains("Text=\"Thu\"", xaml);
        Assert.Contains("Text=\"Fri\"", xaml);
        Assert.Contains("Text=\"Sat\"", xaml);
        Assert.Contains("Text=\"Sun\"", xaml);
    }

    [Fact]
    public void TimelineWindow_DefaultsToMonthScale()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("ScaleUnit: TimelineScaleUnit.Month", source);
        Assert.Contains("RangeMode: TimelineRangeMode.Month1", source);
    }

    [Fact]
    public void TimelineWindow_NoLongerUsesLegacyTopBottomExpanders()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.DoesNotContain("Quick controls", xaml);
        Assert.DoesNotContain("Advanced controls", xaml);
        Assert.DoesNotContain("CustomStartDatePicker", xaml);
        Assert.DoesNotContain("CustomEndDatePicker", xaml);
    }

    [Fact]
    public void TimelineWindow_UsesThemeResources_InsteadOfInlineColors()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("Background=\"{StaticResource AppBackgroundBrush}\"", xaml);
        Assert.Contains("Foreground=\"{StaticResource AppForegroundBrush}\"", xaml);
        Assert.Contains("Background=\"{StaticResource AppSurface1Brush}\"", xaml);
        Assert.DoesNotContain("Fill=\"#", xaml);
        Assert.DoesNotContain("Background=\"#", xaml);
    }

    [Fact]
    public void TimelineWindow_MonthCells_AreResponsiveToWindowWidth()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("Tag=\"MonthCell\"", xaml);
        Assert.DoesNotContain("Width=\"150\"", xaml);
        Assert.Contains("ApplyCalendarCellDimensions()", source);
        Assert.Contains("border.Width = monthCellWidth;", source);
    }

    [Fact]
    public void TimelineWindow_ShowsSelectedTopTabAndDebouncesResizeRefresh()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("x:Name=\"DayViewToggle\"", xaml);
        Assert.Contains("x:Name=\"WeekViewToggle\"", xaml);
        Assert.Contains("x:Name=\"MonthViewToggle\"", xaml);
        Assert.Contains("x:Name=\"YearViewToggle\"", xaml);
        Assert.Contains("DayViewToggle.IsChecked", source);
        Assert.Contains("YearViewToggle.IsChecked", source);
        Assert.Contains("DispatcherQueue.CreateTimer()", source);
        Assert.Contains("_resizeDebounceTimer.Start();", source);
    }

    [Fact]
    public void TimelineWindow_HasLoadingOverlay_ForSlowCalendarFetch()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("x:Name=\"LoadingOverlay\"", xaml);
        Assert.Contains("Loading calendar...", xaml);
        Assert.Contains("SetLoading(true);", source);
        Assert.Contains("SetLoading(false);", source);
    }

    [Fact]
    public void TimelineWindow_WeekView_UsesSevenParallelDayColumns()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var sourceVmPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "ViewModels", "MainViewModel.cs"));
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);
        var sourceVm = File.ReadAllText(sourceVmPath);

        Assert.Contains("x:Name=\"WeekCalendarHost\"", xaml);
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.WeekCalendarDays, Mode=OneWay}\"", xaml);
        Assert.Contains("Tag=\"WeekCell\"", xaml);
        Assert.Contains("WeekCalendarVisibility", sourceVm);
        Assert.Contains("BuildWeekRow", sourceVm);
        Assert.Contains("ApplyCalendarCellDimensions()", source);
    }

    [Fact]
    public void TimelineWindow_YearView_ShowsTwelveMonths_InFourByThreeGrid()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var sourceVmPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "ViewModels", "MainViewModel.cs"));
        var sourceServicePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Services", "MonthCalendarResolver.cs"));
        var xaml = File.ReadAllText(xamlPath);
        var sourceVm = File.ReadAllText(sourceVmPath);
        var sourceService = File.ReadAllText(sourceServicePath);

        Assert.Contains("x:Name=\"YearCalendarHost\"", xaml);
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.YearCalendarRows, Mode=OneWay}\"", xaml);
        Assert.Contains("Tag=\"YearMonthCard\"", xaml);
        Assert.Contains("YearCalendarVisibility", sourceVm);
        Assert.Contains("BuildYearMonthRows", sourceVm);
        Assert.Contains("Take(4)", sourceService);
    }

    [Fact]
    public void TimelineWindow_CalendarCells_ExpandInBothWidthAndHeight()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("ApplyCalendarCellDimensions()", source);
        Assert.Contains("border.Width =", source);
        Assert.Contains("border.Height =", source);
        Assert.Contains("MonthCalendarHost.ActualHeight", source);
        Assert.Contains("WeekCalendarHost.ActualHeight", source);
        Assert.Contains("YearCalendarHost.ActualHeight", source);
    }
}
