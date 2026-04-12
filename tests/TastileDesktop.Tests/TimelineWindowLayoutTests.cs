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

        Assert.Contains("ItemsSource=\"{Binding TimelineHourMarkers, Mode=OneWay}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding TimelineBlocks, Mode=OneWay}\"", xaml);
        Assert.Contains("Height=\"{Binding TimelineCanvasHeight, Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("SyncTimelineItemsBindings()", source);
        Assert.DoesNotContain("ItemsSource=\"{x:Bind ViewModel.TimelineHourMarkers, Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("ItemsSource=\"{x:Bind ViewModel.TimelineBlocks, Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void TimelineWindow_UsesCanvasItemContainerBindings_ForMarkerAndBlockPositioning()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("TranslateTransform Y=\"{Binding Top}\"", xaml);
        Assert.Contains("TranslateTransform X=\"{Binding Left}\" Y=\"{Binding Top}\"", xaml);
    }

    [Fact]
    public void TimelineWindow_UsesButtonStatusAffordance_InsteadOfPassiveIcon()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("Command=\"{Binding StatusCommand}\"", xaml);
        Assert.Contains("Command=\"{Binding EditCommand}\"", xaml);
        Assert.DoesNotContain("Click=\"OnTimelineBlockStatusClick\"", xaml);
        Assert.DoesNotContain("Click=\"OnTimelineBlockEditClick\"", xaml);
        Assert.DoesNotContain("ToolTipService.ToolTip=\"{x:Bind StatusIconToolTip}\"", xaml);
    }

    [Fact]
    public void TimelineWindow_StatusClick_DelegatesLifecycleDecisionToResolver()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("TimelineBlockEditRequested += OnTimelineBlockEditRequested", source);
        Assert.Contains("OnTimelineBlockEditRequested", source);
        Assert.Contains("OpenEditTileAsync", source);
        Assert.Contains("var editTileId = freshTile.Id;", source);
        Assert.Contains("new CreateTileWindow(editTileId, freshTile)", source);
        Assert.Contains("createWindow.Closed += (_, _) => _ = ViewModel.RefreshAsync(forcePublish: true);", source);
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
    public void TimelineWindow_MonthEntry_ShowsTitleAndDurationOnSingleLine()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("Text=\"{Binding Title}\"", xaml);
        Assert.Contains("Grid.Column=\"2\" Margin=\"0,0,2,0\" Text=\"{Binding DurationText}\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Right\"", xaml);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Stretch\" />", xaml);
        Assert.Contains("Command=\"{Binding StatusCommand}\"", xaml);
    }

    [Fact]
    public void TimelineWindow_AppliesInitialLayoutWithoutManualResize()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("TimelineRootGrid.Loaded += OnWindowLoaded;", source);
        Assert.Contains("EnsureInitialLayoutApplied();", source);
        Assert.Contains("ScheduleCalendarReflow();", source);
        Assert.Contains("ViewModel.UpdateTimelineViewport(_viewport);", source);
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
        Assert.Contains("Content=\"←\"", xaml);
        Assert.Contains("Content=\"{Binding TimelineAnchorLabel, Mode=OneWay}\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"現在時点へ戻る\"", xaml);
        Assert.Contains("Content=\"→\"", xaml);
        Assert.Contains("Content=\"Day\"", xaml);
        Assert.Contains("Content=\"Week\"", xaml);
        Assert.Contains("Content=\"Month\"", xaml);
        Assert.Contains("Content=\"Year\"", xaml);
        Assert.Contains("Text=\"{Binding TimelineCompactRangeLabel, Mode=OneWay}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding MonthCalendarRows, Mode=OneWay}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Cells}\"", xaml);
        Assert.DoesNotContain("ItemsSource=\"{x:Bind ViewModel.MonthCalendarRows, Mode=OneWay}\"", xaml);
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

        Assert.Contains("ScaleUnit: TimelineScaleUnit.Day", source);
        Assert.Contains("RangeMode: TimelineRangeMode.Day24", source);
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
        Assert.Contains("ItemsSource=\"{Binding MonthCalendarRows, Mode=OneWay}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Cells}\"", xaml);
        Assert.Contains("<RowDefinition Height=\"*\" />", xaml);
        Assert.Contains("MonthCalendarHost.LayoutUpdated += OnMonthCalendarHostLayoutUpdated;", source);
        Assert.Contains("MonthCalendarHost.SizeChanged += (_, _) => ApplyCalendarCellDimensions();", source);
        Assert.Contains("_lastMonthCellWidth = -1d;", source);
        Assert.Contains("_lastMonthCellHeight = -1d;", source);
        Assert.Contains("Skip apply before month cells ready", source);
        Assert.Contains("border.Width = monthCellWidth;", source);
    }

    [Fact]
    public void TimelineWindow_ShowsSelectedTopTabAndDebouncesResizeRefresh()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("x:Name=\"DayViewToggle\"", xaml);
        Assert.DoesNotContain("x:Name=\"WeekViewToggle\"", xaml);
        Assert.DoesNotContain("x:Name=\"MonthViewToggle\"", xaml);
        Assert.DoesNotContain("x:Name=\"YearViewToggle\"", xaml);
        Assert.DoesNotContain("DayViewToggle.IsChecked", source);
        Assert.DoesNotContain("YearViewToggle.IsChecked", source);
        Assert.Contains("UpdateSelectionButtons: Scale=", source);
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
    public void TimelineWindow_WeekView_UsesUnifiedTimeGrid_WithResponsiveDayColumns()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var sourceVmPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "ViewModels", "MainViewModel.cs"));
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);
        var sourceVm = File.ReadAllText(sourceVmPath);

        Assert.Contains("ItemsSource=\"{Binding WeekTimelineColumns, Mode=OneWay}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Blocks}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding WeekTimelineHourMarkers, Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("Tag=\"WeekTimelineDayColumn\"", xaml);
        Assert.Contains("StackPanel Orientation=\"Horizontal\" Spacing=\"8\"", xaml);
        Assert.DoesNotContain("x:Name=\"WeekLaneGuidesGrid\"", xaml);
        Assert.Contains("Background=\"Transparent\"", xaml);
        Assert.Contains("Height=\"{Binding DataContext.WeekCanvasHeight, ElementName=WeekTimelineColumnsHost, Mode=OneWay}\"", xaml);
        Assert.Contains("WeekCalendarVisibility", sourceVm);
        Assert.Contains("BuildWeekTimelineColumns", sourceVm);
        Assert.DoesNotContain("WeekTimelineBlocks", sourceVm);
        Assert.Contains("WeekTimelineHourMarkers", sourceVm);
        Assert.Contains("WeekCanvasHeight", sourceVm);
        Assert.Contains("ApplyCalendarCellDimensions()", source);
        Assert.Contains("ApplyWeekDayColumnWidths()", source);
    }

    [Fact]
    public void TimelineWindow_WeekView_AvoidsXBindConnectorRegression()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("ItemsSource=\"{Binding WeekTimelineColumns, Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("ItemsSource=\"{x:Bind ViewModel.WeekTimelineColumns, Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("Text=\"{x:Bind TimelineNowLabel, Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("TranslateTransform Y=\"{x:Bind TimelineNowTop, Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void TimelineWindow_WeekLaneShells_DoNotPaintOverTimeGrid()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("x:Key=\"TransparentWeekLaneItemContainerStyle\"", xaml);
        Assert.Contains("TargetType=\"ContentPresenter\"", xaml);
        Assert.Contains("<Setter Property=\"Background\" Value=\"Transparent\" />", xaml);
        Assert.Contains("<Setter Property=\"BorderBrush\" Value=\"Transparent\" />", xaml);
        Assert.Contains("<Setter Property=\"BorderThickness\" Value=\"0\" />", xaml);
        Assert.Contains("ItemContainerStyle=\"{StaticResource TransparentWeekLaneItemContainerStyle}\"", xaml);
        Assert.Contains("<Grid Width=\"{Binding DataContext.WeekDayColumnWidth, ElementName=WeekTimelineColumnsHost, Mode=OneWay}\" Background=\"Transparent\">", xaml);
        Assert.Contains("<Grid Width=\"{Binding DataContext.WeekDayColumnWidth, ElementName=WeekTimelineColumnsHost, Mode=OneWay}\" Height=\"{Binding DataContext.WeekCanvasHeight, ElementName=WeekTimelineColumnsHost, Mode=OneWay}\" Background=\"Transparent\">", xaml);
        Assert.DoesNotContain("<Grid ColumnSpacing=\"8\" IsHitTestVisible=\"False\"", xaml);
        Assert.DoesNotContain("<Border Grid.Column=\"0\" Background=\"{StaticResource AppSurface0Brush}\"", xaml);
        Assert.DoesNotContain("Width=\"{Binding DataContext.WeekDayColumnWidth, ElementName=WeekTimelineColumnsHost, Mode=OneWay}\" Padding=\"6,4\" Background=\"{StaticResource AppSurface1Brush}\"", xaml);
        Assert.DoesNotContain("<Border Background=\"Transparent\" BorderBrush=\"Transparent\" BorderThickness=\"0\" CornerRadius=\"6\" Opacity=\"0\" />", xaml);
    }

    [Fact]
    public void TimelineWindow_WeekView_ShowsCurrentTimeBar()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("Width=\"{Binding ActualWidth, ElementName=WeekTimelineColumnsHost}\" Visibility=\"{Binding TimelineNowVisibility, Mode=OneWay}\"", xaml);
        Assert.Contains("TranslateTransform Y=\"{Binding TimelineNowTop, Mode=OneWay}\"", xaml);
        Assert.Contains("Text=\"{Binding TimelineNowLabel, Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void TimelineWindow_RebindsNamedElements_WhenXamlConnectorLeavesFieldsNull()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("EnsureNamedElementsBound();", source);
        Assert.Contains("WireToolbarControls();", source);
        Assert.Contains("private void EnsureNamedElementsBound()", source);
        Assert.Contains("root.FindName(\"TimelineCanvasHost\")", source);
        Assert.DoesNotContain("root.FindName(\"RangeComboBox\")", source);
        Assert.Contains("RangeComboBox = EnumerateDescendants<ComboBox>(toolbarGrid)", source);
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
        Assert.Contains("ItemsSource=\"{Binding YearCalendarRows, Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("ItemsSource=\"{x:Bind ViewModel.YearCalendarRows, Mode=OneWay}\"", xaml);
        Assert.Contains("Tag=\"YearMonthCard\"", xaml);
        Assert.Contains("YearCalendarVisibility", sourceVm);
        Assert.Contains("BuildYearMonthRows", sourceVm);
        Assert.Contains("Take(4)", sourceService);
    }

    [Fact]
    public void TimelineWindow_CalendarCells_ExpandInBothWidthAndHeight()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var source = File.ReadAllText(sourcePath);
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("ApplyCalendarCellDimensions()", source);
        Assert.Contains("border.Width =", source);
        Assert.Contains("border.Height =", source);
        Assert.Contains("MonthCalendarHost.ActualHeight", source);
        Assert.Contains("MonthCalendarHost.ActualWidth", source);
        Assert.Contains("ItemsSource=\"{Binding MonthCalendarRows, Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("WeekCalendarHost.ActualHeight", source);
        Assert.Contains("Week calendar now uses unified timeline", source);
        Assert.Contains("YearCalendarHost.ActualHeight", source);
    }

    [Fact]
    public void MainViewModel_ViewportUpdate_ForcesTimelineRepublish()
    {
        var sourceVmPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "ViewModels", "MainViewModel.cs"));
        var sourceVm = File.ReadAllText(sourceVmPath);

        Assert.Contains("public void UpdateTimelineViewport(TimelineViewportSettings viewport)", sourceVm);
        Assert.Contains("_ = _pollingService.PollAsync(forcePublish: true);", sourceVm);
    }
}
