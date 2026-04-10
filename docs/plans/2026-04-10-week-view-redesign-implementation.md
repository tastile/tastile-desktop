# Week View Redesign Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Redesign Week view to display 7 parallel vertical timelines with detailed card-style tile blocks, synchronized scrolling and zooming.

**Architecture:** Single ScrollViewer wrapping 7 columns, each column contains HourMarkers (time labels + grid lines) and TimelineBlocks (Canvas-based tile cards). Now indicator only shows in today's column.

**Tech Stack:** WinUI 3, C# / .NET 10, CommunityToolkit.Mvvm

---

## Task 1: Update TimelineWeekColumnViewModel to include IsToday property

**Files:**
- Modify: `src/TastileDesktop/ViewModels/MainViewModel.cs:98-104`

**Step 1: Add IsToday property to TimelineWeekColumnViewModel**

```csharp
public sealed class TimelineWeekColumnViewModel : ObservableObject
{
    public int DayOfWeekIndex { get; set; }
    public string DayLabel { get; set; } = string.Empty;
    public string DayNumber { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public IReadOnlyList<TimelineAbsoluteBlockViewModel> Blocks { get; set; } = [];
}
```

**Step 2: Update BuildWeekTimelineColumns call to set IsToday**

Modify the WeekTimelineColumns initialization in `OnTimelineChanged` method (around line 1344-1380):

```csharp
var todayLocal = DateTimeOffset.Now.ToLocalTime();
WeekTimelineColumns = new ObservableCollection<TimelineWeekColumnViewModel>(
    weekTimelineColumns.Select(col => new TimelineWeekColumnViewModel
    {
        DayOfWeekIndex = col.DayOfWeekIndex,
        DayLabel = col.DayLabel,
        DayNumber = weekCells.ElementAtOrDefault(col.DayOfWeekIndex)?.DayNumber ?? string.Empty,
        IsToday = IsTodayColumn(col.DayOfWeekIndex, TimelineViewport.AnchorLocal, todayLocal),
        Blocks = col.Blocks.Select(block => new TimelineAbsoluteBlockViewModel
        {
            // ... existing block mapping ...
        }).ToArray(),
    }));

// Add helper method (outside OnTimelineChanged, inside MainViewModel class):
private static bool IsTodayColumn(int dayOfWeekIndex, DateTimeOffset anchorLocal, DateTimeOffset todayLocal)
{
    var weekStart = GetWeekStart(anchorLocal);
    var columnDate = weekStart.AddDays(dayOfWeekIndex);
    return columnDate.Date == todayLocal.Date;
}

private static DateTimeOffset GetWeekStart(DateTimeOffset date)
{
    var dayOfWeek = (int)date.DayOfWeek;
    // Adjust so Monday = 0, Sunday = 6
    var adjustedDay = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
    return date.Date.AddDays(-adjustedDay);
}
```

**Step 3: Update WeekCalendarVisibility property to use new layout**

In `MainViewModel.cs` around line 655-660, keep existing visibility logic.

**Step 4: Commit**

```bash
git add src/TastileDesktop/ViewModels/MainViewModel.cs
git commit -m "feat: add IsToday property to WeekTimelineColumnViewModel

- Add IsToday property to identify today's column
- Update BuildWeekTimelineColumns to set IsToday based on date
- Add helper methods IsTodayColumn and GetWeekStart

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 2: Update XAML - Replace WeekCalendarHost with WeekTimelineRoot

**Files:**
- Modify: `src/TastileDesktop/Views/TimelineWindow.xaml:181-224`

**Step 1: Replace WeekCalendarHost Grid with WeekTimelineRoot**

Find the WeekCalendarHost Grid (around line 181) and replace entire section with:

```xml
<!-- Week Timeline View -->
<ItemsControl x:Name="WeekTimelineRoot" Visibility="{Binding WeekCalendarVisibility, Mode=OneWay}" ItemsSource="{x:Bind ViewModel.WeekTimelineColumns, Mode=OneWay}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <StackPanel Orientation="Horizontal" />
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate x:DataType="viewModels:TimelineWeekColumnViewModel">
            <Grid Width="180" MinWidth="180" MaxWidth="300" Margin="0,0,8,0">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                </Grid.RowDefinitions>

                <!-- Column Header -->
                <Border Grid.Row="0" Padding="8" Background="{StaticResource AppSurface1Brush}" BorderBrush="{StaticResource AppBorderBrush}" BorderThickness="1" CornerRadius="8,8,0,0">
                    <StackPanel Spacing="2">
                        <TextBlock Text="{Binding DayNumber}" FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource AppForegroundBrush}" HorizontalAlignment="Center" />
                        <TextBlock Text="{Binding DayLabel}" FontSize="11" Foreground="{StaticResource AppForegroundMutedBrush}" HorizontalAlignment="Center" />
                    </StackPanel>
                </Border>

                <!-- Timeline with Hour Markers and Blocks -->
                <Grid Grid.Row="1" Background="{StaticResource AppSurface0Brush}" BorderBrush="{StaticResource AppBorderBrush}" BorderThickness="1,0,1,1" CornerRadius="0,0,8,8">
                    <ItemsControl ItemsSource="{x:Bind ViewModel.WeekTimelineColumns, Mode=OneWay}">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <Canvas />
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Grid Width="{Binding ActualWidth, ElementName=WeekTimelineRoot}">
                                    <Grid.RenderTransform>
                                        <TranslateTransform Y="{Binding Top}" />
                                    </Grid.RenderTransform>
                                    <Rectangle Width="{Binding ActualWidth, ElementName=WeekTimelineRoot}" Height="1" Fill="{StaticResource AppBorderBrush}" />
                                    <TextBlock Text="{Binding Label}" Foreground="{StaticResource AppForegroundMutedBrush}" FontSize="10" Margin="4,-14,0,0" />
                                </Grid>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>

                    <ItemsControl ItemsSource="{Binding Blocks}">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <Canvas />
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                        <ItemsControl.ItemTemplate>
                            <DataTemplate x:DataType="viewModels:TimelineAbsoluteBlockViewModel">
                                <Border Width="{Binding Width}" Height="{Binding Height}" Padding="10,6" CornerRadius="6" Background="{Binding Fill}" BorderBrush="{Binding BorderBrush}" BorderThickness="1">
                                    <Border.RenderTransform>
                                        <TranslateTransform X="{Binding Left}" Y="{Binding Top}" />
                                    </Border.RenderTransform>
                                    <Grid ColumnSpacing="12">
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="Auto" />
                                            <ColumnDefinition Width="*" />
                                            <ColumnDefinition Width="Auto" />
                                            <ColumnDefinition Width="Auto" />
                                        </Grid.ColumnDefinitions>
                                        <Button Grid.Column="0" Width="28" Height="28" Padding="0" Margin="0,0,2,0" Background="Transparent" BorderThickness="0" Tag="{Binding TileId}" Click="OnTimelineBlockStatusClick">
                                            <FontIcon FontFamily="Segoe Fluent Icons" Glyph="{Binding StatusIconGlyph}" Foreground="{Binding StatusForegroundBrush}" FontSize="14" VerticalAlignment="Center" />
                                        </Button>
                                        <StackPanel Grid.Column="1" Spacing="2" VerticalAlignment="Center">
                                            <TextBlock Text="{Binding Title}" Foreground="{Binding ForegroundBrush}" FontWeight="SemiBold" TextTrimming="CharacterEllipsis" />
                                            <TextBlock Text="{Binding TimeRangeText}" Foreground="{Binding SecondaryForegroundBrush}" FontSize="12" />
                                            <TextBlock Text="{Binding KindLabel}" Foreground="{Binding SecondaryForegroundBrush}" FontSize="11" Visibility="{Binding KindLabelVisibility}" />
                                        </StackPanel>
                                        <Button Grid.Column="2" Width="28" Height="28" Padding="0" Margin="0,0,4,0" Background="Transparent" BorderThickness="0" Tag="{Binding TileId}" Click="OnTimelineBlockEditClick">
                                            <FontIcon FontFamily="Segoe Fluent Icons" Glyph="&#xE70F;" Foreground="{Binding SecondaryForegroundBrush}" FontSize="13" VerticalAlignment="Center" />
                                        </Button>
                                        <TextBlock Grid.Column="3" Text="{Binding DurationText}" Foreground="{Binding SecondaryForegroundBrush}" VerticalAlignment="Center" />
                                    </Grid>
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>

                    <!-- Now Indicator (only shows in today's column) -->
                    <Canvas Width="180" Visibility="{x:Bind IsToday, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}">
                        <Canvas.RenderTransform>
                            <TranslateTransform Y="{x:Bind ViewModel.TimelineNowTop, Mode=OneWay}" />
                        </Canvas.RenderTransform>
                        <Rectangle Canvas.Top="0" Width="180" Height="2" Fill="{StaticResource AppPrimaryBrush}" />
                        <Ellipse Canvas.Left="-4" Canvas.Top="-3" Width="8" Height="8" Fill="{StaticResource AppPrimaryBrush}" />
                        <TextBlock Canvas.Top="-14" Width="180" TextAlignment="Right" Padding="0,0,4,0" Text="{x:Bind ViewModel.TimelineNowLabel, Mode=OneWay}" Foreground="{StaticResource AppPrimaryBrush}" FontSize="11" />
                    </Canvas>
                </Grid>
            </Grid>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

**Step 2: Update TimelineWindow.xaml.cs - Remove WeekCalendarHost binding**

In `TimelineWindow.xaml.cs` around line 57, remove or comment out the WeekCalendarHost.SizeChanged event handler.

**Step 3: Update EnsureNamedElementsBound - Remove WeekCalendarHost reference**

In `TimelineWindow.xaml.cs` around line 91-92, remove the WeekCalendarHost line:

```csharp
// WeekCalendarHost ??= root.FindName("WeekCalendarHost") as Grid; // Removed - replaced by WeekTimelineRoot
```

**Step 4: Update ApplyCalendarCellDimensions - Skip week logic**

In `TimelineWindow.xaml.cs` around line 506-516, comment out or remove the week cell dimension logic since week now uses the unified timeline.

**Step 5: Commit**

```bash
git add src/TastileDesktop/Views/TimelineWindow.xaml src/TastileDesktop/Views/TimelineWindow.xaml.cs
git commit -m "feat: replace WeekCalendarHost with WeekTimelineRoot

- Replace calendar-based week view with 7-column timeline layout
- Each column shows HourMarkers and TimelineBlocks like Day view
- Now indicator only shows in today's column (IsToday property)
- Remove week-specific calendar dimension logic
- Each column min-width 180px, max-width 300px, scales to fill space

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 3: Update MonthCalendarResolver to support week timeline columns

**Files:**
- Modify: `src/TastileDesktop/Services/MonthCalendarResolver.cs`

**Step 1: Add BuildWeekTimelineColumns method**

Find the MonthCalendarResolver class and add/modify the BuildWeekTimelineColumns method to set IsToday:

```csharp
public static List<TimelineWeekColumn> BuildWeekTimelineColumns(
    IReadOnlyList<TimelineItem> items,
    DateTimeOffset anchorLocal,
    double hoursPerPixel)
{
    var todayLocal = DateTimeOffset.Now.ToLocalTime();
    var weekStart = GetWeekStart(anchorLocal);
    var columns = new List<TimelineWeekColumn>();

    for (int i = 0; i < 7; i++)
    {
        var dayDate = weekStart.AddDays(i);
        var dayItems = items
            .Where(item => IsItemOnDate(item, dayDate))
            .ToList();

        var blocks = ResolveDayBlocks(dayItems, dayDate, hoursPerPixel);

        columns.Add(new TimelineWeekColumn
        {
            DayOfWeekIndex = i,
            DayLabel = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }[i],
            DayNumber = $"{dayDate.Month}/{dayDate.Day}",
            IsToday = dayDate.Date == todayLocal.Date,
            Blocks = blocks
        });
    }

    return columns;
}

private static bool IsItemOnDate(TimelineItem item, DateTimeOffset targetDate)
{
    if (string.IsNullOrWhiteSpace(item.StartAt))
        return false;

    if (DateTimeOffset.TryParse(item.StartAt, out var startAt))
    {
        return startAt.Date == targetDate.Date;
    }

    return false;
}

private static List<TimelineBlock> ResolveDayBlocks(
    List<TimelineItem> dayItems,
    DateTimeOffset dayDate,
    double hoursPerPixel)
{
    var blocks = new List<TimelineBlock>();
    const double laneGap = 4d;

    // Group overlapping items into lanes
    var lanes = new List<List<TimelineBlock>>();
    foreach (var item in dayItems)
    {
        if (string.IsNullOrWhiteSpace(item.StartAt) || string.IsNullOrWhiteSpace(item.EndAt))
            continue;

        if (!DateTimeOffset.TryParse(item.StartAt, out var startAt) ||
            !DateTimeOffset.TryParse(item.EndAt, out var endAt))
            continue;

        var startMinutes = (startAt - dayDate.Date).TotalMinutes;
        var endMinutes = (endAt - dayDate.Date).TotalMinutes;
        var durationMinutes = endAt - startAt;

        var top = startMinutes / 60.0 * hoursPerPixel;
        var height = durationMinutes.TotalMinutes / 60.0 * hoursPerPixel;

        // Find a lane that doesn't overlap
        int laneIndex = 0;
        for (; laneIndex < lanes.Count; laneIndex++)
        {
            var lane = lanes[laneIndex];
            var lastBlock = lane.LastOrDefault();
            if (lastBlock == null || lastBlock.Top + lastBlock.Height <= top)
            {
                break;
            }
        }

        // Add new lane if needed
        while (laneIndex >= lanes.Count)
        {
            lanes.Add(new List<TimelineBlock>());
        }

        var block = new TimelineBlock
        {
            TileId = item.Id,
            Title = item.Title,
            StartLabel = startAt.ToString("HH:mm"),
            EndLabel = endAt.ToString("HH:mm"),
            DurationLabel = $"{(int)durationMinutes.TotalMinutes}m",
            Kind = item.Kind ?? "task",
            IsActive = item.IsActive,
            IsDone = item.IsDone,
            Top = top,
            Height = Math.Max(24, height),
            Left = 0,
            Width = 100, // Will be set by column width
            Lane = laneIndex,
            TotalLanes = lanes.Count,
            IsFullWidth = false
        };

        lanes[laneIndex].Add(block);
        blocks.Add(block);
    }

    // Update lane counts after all items placed
    foreach (var block in blocks)
    {
        block.TotalLanes = lanes.Count;
    }

    return blocks;
}

private static DateTimeOffset GetWeekStart(DateTimeOffset date)
{
    var dayOfWeek = (int)date.DayOfWeek;
    // Adjust so Monday = 0, Sunday = 6
    var adjustedDay = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
    return date.Date.AddDays(-adjustedDay);
}
```

**Step 2: Update TimelineWeekColumn model to include IsToday**

Find or add the TimelineWeekColumn class in MonthCalendarResolver.cs:

```csharp
public sealed class TimelineWeekColumn
{
    public int DayOfWeekIndex { get; set; }
    public string DayLabel { get; set; } = string.Empty;
    public string DayNumber { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public IReadOnlyList<TimelineBlock> Blocks { get; set; } = [];
}
```

**Step 3: Commit**

```bash
git add src/TastileDesktop/Services/MonthCalendarResolver.cs
git commit -m "feat: update BuildWeekTimelineColumns with IsToday support

- Add IsToday property to TimelineWeekColumn model
- Implement IsItemOnDate to check if item belongs to target date
- Add ResolveDayBlocks to create timeline blocks with lane layout
- Add GetWeekStart helper to calculate Monday start of week
- Each column now contains properly positioned timeline blocks

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 4: Update TimelineWindow.xaml.cs for synchronized scrolling

**Files:**
- Modify: `src/TastileDesktop/Views/TimelineWindow.xaml.cs`

**Step 1: Add WeekTimelineScrollViewer field**

Add after TimelineScrollViewer field (around line 90):

```csharp
private ScrollViewer? TimelineScrollViewer;
private ScrollViewer? WeekTimelineScrollViewer;
```

**Step 2: Update EnsureNamedElementsBound to bind WeekTimelineScrollViewer**

Add inside the try block (around line 90):

```csharp
WeekTimelineScrollViewer ??= root.FindName("WeekTimelineScrollViewer") as ScrollViewer;
```

**Step 3: Update XAML to wrap WeekTimelineRoot in ScrollViewer**

In `TimelineWindow.xaml`, wrap the WeekTimelineRoot ItemsControl in a ScrollViewer. Modify the section from Task 2:

```xml
<!-- Week Timeline View -->
<ScrollViewer x:Name="WeekTimelineScrollViewer" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled" Visibility="{Binding WeekCalendarVisibility, Mode=OneWay}">
    <ItemsControl x:Name="WeekTimelineRoot" ItemsSource="{x:Bind ViewModel.WeekTimelineColumns, Mode=OneWay}">
        <!-- ... rest of ItemsControl content ... -->
    </ItemsControl>
</ScrollViewer>
```

**Step 4: Update OnTimelineWheelChanged to handle WeekTimelineScrollViewer**

Modify the method (around line 241) to handle both scroll viewers:

```csharp
private void OnTimelineWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
{
    ScrollViewer? scrollViewer = null;
    var canvasHost = sender as Grid;

    if (sender is ScrollViewer sv)
    {
        scrollViewer = sv;
    }
    else if (TimelineScrollViewer != null && TimelineCanvasVisibility == Visibility.Visible)
    {
        scrollViewer = TimelineScrollViewer;
    }
    else if (WeekTimelineScrollViewer != null && WeekCalendarVisibility == Visibility.Visible)
    {
        scrollViewer = WeekTimelineScrollViewer;
    }

    if (scrollViewer == null)
    {
        return;
    }

    // ... rest of the method remains the same ...
}
```

**Step 5: Commit**

```bash
git add src/TastileDesktop/Views/TimelineWindow.xaml src/TastileDesktop/Views/TimelineWindow.xaml.cs
git commit -m "feat: add synchronized scrolling for Week timeline

- Add WeekTimelineScrollViewer field and binding
- Wrap WeekTimelineRoot in ScrollViewer for unified scrolling
- Update OnTimelineWheelChanged to handle both Day and Week scroll viewers
- All 7 week columns scroll together vertically

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 5: Update zoom functionality for Week view

**Files:**
- Modify: `src/TastileDesktop/Views/TimelineWindow.xaml.cs`
- Modify: `src/TastileDesktop/ViewModels/MainViewModel.cs`

**Step 1: Update MainViewModel OnTimelineChanged to set WeekCanvasHeight correctly**

In `OnTimelineChanged` method (around line 1341-1349), ensure WeekCanvasHeight uses the correct zoom scale:

```csharp
var hoursPerPixel = TimelineViewport.ScaleUnit == TimelineScaleUnit.Week
    ? (TimelineViewport.PixelsPerHourBase * TimelineViewport.ZoomScale)
    : 120d;
var weekTimelineColumns = MonthCalendarResolver.BuildWeekTimelineColumns(
    timeline?.Items ?? [],
    TimelineViewport.AnchorLocal,
    hoursPerPixel);
WeekCanvasHeight = 24 * hoursPerPixel;
```

**Step 2: Update WeekTimelineRoot to use WeekCanvasHeight for total height**

In `TimelineWindow.xaml`, modify the WeekTimelineScrollViewer to set the content height. Actually, since each column has its own Grid and Canvas, the height is determined by the blocks. We need to ensure the ScrollViewer content has the correct height.

**Step 3: Test build**

```bash
dotnet build ./src/TastileDesktop/TastileDesktop.csproj -r win-x64
```

Expected: Build succeeds with no errors.

**Step 4: Commit**

```bash
git add src/TastileDesktop/Views/TimelineWindow.xaml.cs src/TastileDesktop/ViewModels/MainViewModel.cs
git commit -m "feat: update zoom functionality for Week view

- Ensure WeekCanvasHeight uses PixelsPerHourBase * ZoomScale
- Week view now responds correctly to zoom in/out
- All 7 columns scale uniformly with zoom level

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 6: Add HourMarkers to each week column

**Files:**
- Modify: `src/TastileDesktop/Views/TimelineWindow.xaml`
- Modify: `src/TastileDesktop/ViewModels/MainViewModel.cs`

**Step 1: Update WeekTimelineColumnViewModel to include HourMarkers**

In `MainViewModel.cs`, modify TimelineWeekColumnViewModel (around line 98):

```csharp
public sealed class TimelineWeekColumnViewModel : ObservableObject
{
    public int DayOfWeekIndex { get; set; }
    public string DayLabel { get; set; } = string.Empty;
    public string DayNumber { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public IReadOnlyList<TimelineAbsoluteBlockViewModel> Blocks { get; set; } = [];
    public IReadOnlyList<TimelineHourMarkerViewModel> HourMarkers { get; set; } = [];
}
```

**Step 2: Update MainViewModel OnTimelineChanged to populate HourMarkers for week**

In `OnTimelineChanged` method (around line 1344-1380), add HourMarkers population:

```csharp
var weekTimelineColumns = MonthCalendarResolver.BuildWeekTimelineColumns(
    timeline?.Items ?? [],
    TimelineViewport.AnchorLocal,
    hoursPerPixel);

var hourMarkers = Enumerable.Range(0, 25).Select(hour => new TimelineHourMarkerViewModel
{
    Label = $"{hour}:00",
    Top = hour * hoursPerPixel,
}).ToArray();

WeekTimelineColumns = new ObservableCollection<TimelineWeekColumnViewModel>(
    weekTimelineColumns.Select(col => new TimelineWeekColumnViewModel
    {
        DayOfWeekIndex = col.DayOfWeekIndex,
        DayLabel = col.DayLabel,
        DayNumber = weekCells.ElementAtOrDefault(col.DayOfWeekIndex)?.DayNumber ?? string.Empty,
        IsToday = IsTodayColumn(col.DayOfWeekIndex, TimelineViewport.AnchorLocal, todayLocal),
        HourMarkers = hourMarkers,
        Blocks = col.Blocks.Select(block => new TimelineAbsoluteBlockViewModel
        {
            // ... existing block mapping ...
        }).ToArray(),
    }));
```

**Step 3: Update XAML to display HourMarkers in each column**

In `TimelineWindow.xaml`, modify the WeekTimelineRoot ItemsControl template (inside the DataTemplate for TimelineWeekColumnViewModel), find the Timeline Grid section and add HourMarkers:

```xml
<!-- Timeline with Hour Markers and Blocks -->
<Grid Grid.Row="1" Background="{StaticResource AppSurface0Brush}" BorderBrush="{StaticResource AppBorderBrush}" BorderThickness="1,0,1,1" CornerRadius="0,0,8,8"
      Height="{x:Bind ViewModel.WeekCanvasHeight, Mode=OneWay}">
    <ItemsControl ItemsSource="{Binding HourMarkers}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <Canvas />
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate x:DataType="viewModels:TimelineHourMarkerViewModel">
                <Grid Width="180">
                    <Grid.RenderTransform>
                        <TranslateTransform Y="{Binding Top}" />
                    </Grid.RenderTransform>
                    <Rectangle Width="180" Height="1" Fill="{StaticResource AppBorderBrush}" />
                    <TextBlock Text="{Binding Label}" Foreground="{StaticResource AppForegroundMutedBrush}" FontSize="10" Margin="4,-14,0,0" />
                </Grid>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>

    <ItemsControl ItemsSource="{Binding Blocks}">
        <!-- ... existing Blocks template ... -->
    </ItemsControl>

    <!-- Now Indicator ... -->
</Grid>
```

**Step 4: Test build**

```bash
dotnet build ./src/TastileDesktop/TastileDesktop.csproj -r win-x64
```

Expected: Build succeeds with no errors.

**Step 5: Commit**

```bash
git add src/TastileDesktop/Views/TimelineWindow.xaml src/TastileDesktop/ViewModels/MainViewModel.cs
git commit -m "feat: add HourMarkers to each Week timeline column

- Add HourMarkers property to TimelineWeekColumnViewModel
- Generate hour markers (0:00-24:00) for each week column
- Display grid lines and time labels in each column like Day view
- Hour markers scale with zoom level

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 7: Final testing and cleanup

**Files:**
- Test: Manual verification in running application

**Step 1: Run application**

```bash
dotnet run --project ./src/TastileDesktop/TastileDesktop.csproj
```

**Step 2: Verify Week view functionality**

Checklist:
- [ ] Week view shows 7 columns (Mon-Sun)
- [ ] Each column header shows date + day (e.g., "4/7（月）")
- [ ] Time markers (0:00, 1:00, ...) show on each column with grid lines
- [ ] Tile blocks display as detailed cards with title, time, icon buttons
- [ ] Vertical scrolling is synchronized across all 7 columns
- [ ] Zoom in/out affects all columns uniformly
- [ ] Now indicator only shows in today's column
- [ ] Columns expand to fill available width (min 180px)

**Step 3: Fix any issues found**

If issues found, document and fix.

**Step 4: Update design document with any changes**

**Step 5: Final commit**

```bash
git add docs/plans/2026-04-10-week-view-redesign.md
git commit -m "docs: update design document based on implementation

- Update with any changes made during implementation
- Document final state of Week view redesign

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Summary

This plan implements the Week view redesign with:
1. 7 parallel vertical timelines in synchronized columns
2. Time markers and grid lines on each column
3. Detailed card-style tile blocks (same as Day view)
4. Synchronized vertical scrolling and zooming
5. Now indicator only in today's column
6. Responsive column widths (min 180px, expand to fill)

Total tasks: 7
Estimated time: 30-45 minutes
