using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TastileDesktop.Models;
using TastileDesktop.Resources;

namespace TastileDesktop.Views.CreateTile.Bodies;

public sealed partial class RecurringBody : UserControl, ICreateTileBody
{
    public event EventHandler? StateChanged;
    public event EventHandler? DurationChanged;

    private static readonly (CreateTileRepeatMode Mode, string Key)[] RepeatOptions =
    {
        (CreateTileRepeatMode.Once, "CreateTile.RepeatModeOnce"),
        (CreateTileRepeatMode.Daily, "CreateTile.RepeatModeDaily"),
        (CreateTileRepeatMode.Weekly, "CreateTile.RepeatModeWeekly"),
        (CreateTileRepeatMode.Monthly, "CreateTile.RepeatModeMonthly"),
        (CreateTileRepeatMode.Interval, "CreateTile.RepeatModeInterval"),
    };

    private static readonly (string Unit, string Key)[] IntervalUnits =
    {
        ("min", "CreateTile.IntervalUnitMin"),
        ("hour", "CreateTile.IntervalUnitHour"),
        ("day", "CreateTile.IntervalUnitDay"),
    };

    private static readonly (int Week, string Key)[] MonthWeeks =
    {
        (1, "CreateTile.MonthlyWeekFirst"),
        (2, "CreateTile.MonthlyWeekSecond"),
        (3, "CreateTile.MonthlyWeekThird"),
        (4, "CreateTile.MonthlyWeekFourth"),
        (5, "CreateTile.MonthlyLastWeek"),
    };

    private readonly Sections.DateTimeRow _firstOccurrence = new();
    private readonly Sections.DateTimeRow _dailyStart = new();
    private readonly Sections.DateTimeRow _dailyEnd = new();
    private readonly Sections.DateTimeRow _timeOfDay = new();
    private readonly TextBlock _hintText = new();

    public RecurringBody()
    {
        InitializeComponent();
        DurationLabel.Text = Strings.Get("CreateTile.DurationLabel");
        RepeatUntilLabel.Text = Strings.Get("CreateTile.RepeatEndLabel");
        RepeatLabel.Text = Strings.Get("CreateTile.RepeatModeOnce");
        BuildRepeatCombo();
        BuildIntervalUnits();
        BuildMonthlyKind();
        BuildMonthWeeks();
        BuildWeekdays();
        BuildTimeModels();
        BuildTimeRow();
        DurationSelect.MinutesChanged += (_, _) =>
        {
            DurationChanged?.Invoke(this, EventArgs.Empty);
            StateChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public void ApplyState(CreateTileFormState state)
    {
        var mode = state.RepeatMode;
        if (state.ObjectiveMode == "recurring")
        {
            mode = state.RecurrenceFrequency switch
            {
                "weekly" => CreateTileRepeatMode.Weekly,
                "monthly" => CreateTileRepeatMode.Monthly,
                _ => CreateTileRepeatMode.Daily,
            };
            if (state.IntervalValue.HasValue && state.IntervalUnit is not null)
            {
                mode = CreateTileRepeatMode.Interval;
            }
        }
        SetRepeatMode(mode);

        if (mode == CreateTileRepeatMode.Weekly)
        {
            SetWeekdays(state.RecurrenceWeekdays ?? new List<int> { (int)DateTimeOffset.Now.DayOfWeek });
        }
        if (mode == CreateTileRepeatMode.Interval)
        {
            IntervalValueBox.Value = state.IntervalValue ?? 30;
            var unit = state.IntervalUnit ?? "min";
            for (var i = 0; i < IntervalUnitCombo.Items.Count; i++)
            {
                if (IntervalUnitCombo.Items[i] is ComboBoxItem item && item.Tag is string u && u == unit)
                {
                    IntervalUnitCombo.SelectedIndex = i;
                    break;
                }
            }
        }
        if (mode == CreateTileRepeatMode.Monthly)
        {
            MonthlyKindCombo.SelectedIndex = state.MonthlyKind == CreateTileMonthlyKind.ByDay ? 0 : 1;
            if (state.MonthlyDayOfMonth.HasValue) MonthlyDayBox.Value = state.MonthlyDayOfMonth.Value;
            if (state.MonthlyWeekOfMonth.HasValue)
            {
                for (var i = 0; i < MonthlyWeekOfMonthCombo.Items.Count; i++)
                {
                    if (MonthlyWeekOfMonthCombo.Items[i] is ComboBoxItem item && item.Tag is int w && w == state.MonthlyWeekOfMonth.Value)
                    {
                        MonthlyWeekOfMonthCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (state.MonthlyWeekday.HasValue) MonthlyWeekdayCombo.SelectedIndex = Math.Clamp(state.MonthlyWeekday.Value, 0, 6);
        }

        if (state.TimeModel == CreateTileTimeModel.DurationOnly) TimeModelCombo.SelectedIndex = 2;
        else if (state.TimeModel == CreateTileTimeModel.WindowWithDuration) TimeModelCombo.SelectedIndex = 1;
        else TimeModelCombo.SelectedIndex = 0;

        if (state.RecurringEndDate.HasValue) RepeatUntilDate.Date = state.RecurringEndDate.Value;
        RepeatUntilToggle.IsOn = state.RecurringEndDate.HasValue;

        var minutes = ((state.WorkHours ?? 0) * 60) + (state.WorkMinutes ?? 0);
        if (minutes > 0) DurationSelect.Minutes = minutes;

        ProjectRow.Project = state.Project ?? string.Empty;
        ProjectRow.Tags = state.Tags is null ? new List<string>() : new List<string>(state.Tags);
        ProjectRow.Swatches = new List<string> { "#5e6ad2", "#10b981", "#a855f7", "#f59e0b", "#ef4444", "#6b7280" };
        ProjectRow.SelectedColor = state.ColorHex;
        MemoSection.Memo = state.Memo ?? string.Empty;

        OnRepeatChanged(this, null!);
    }

    public void WriteState(CreateTileFormState state)
    {
        var mode = CurrentRepeatMode();
        state.RepeatMode = mode;
        state.ObjectiveMode = "recurring";

        if (mode == CreateTileRepeatMode.Daily)
        {
            state.RecurrenceFrequency = "daily";
            state.RecurrenceInterval = 1;
        }
        else if (mode == CreateTileRepeatMode.Weekly)
        {
            state.RecurrenceFrequency = "weekly";
            state.RecurrenceInterval = 1;
            state.RecurrenceWeekdays = SelectedWeekdays();
        }
        else if (mode == CreateTileRepeatMode.Monthly)
        {
            state.RecurrenceFrequency = "monthly";
            state.RecurrenceInterval = 1;
            if (MonthlyKindCombo.SelectedIndex == 0)
            {
                state.MonthlyKind = CreateTileMonthlyKind.ByDay;
                state.MonthlyDayOfMonth = (int)Math.Clamp(MonthlyDayBox.Value, 1, 31);
                state.MonthlyWeekOfMonth = null;
                state.MonthlyWeekday = null;
            }
            else
            {
                state.MonthlyKind = CreateTileMonthlyKind.ByWeekday;
                if (MonthlyWeekOfMonthCombo.SelectedItem is ComboBoxItem item && item.Tag is int week)
                {
                    state.MonthlyWeekOfMonth = week;
                }
                state.MonthlyWeekday = MonthlyWeekdayCombo.SelectedIndex;
                state.MonthlyDayOfMonth = null;
            }
        }
        else if (mode == CreateTileRepeatMode.Interval)
        {
            state.IntervalValue = (int)Math.Max(1, IntervalValueBox.Value);
            if (IntervalUnitCombo.SelectedItem is ComboBoxItem unit && unit.Tag is string u)
            {
                state.IntervalUnit = u;
            }
        }
        else
        {
            state.ObjectiveMode = "finish_once";
        }

        if (TimeModelCombo.SelectedIndex == 2) state.TimeModel = CreateTileTimeModel.DurationOnly;
        else if (TimeModelCombo.SelectedIndex == 1) state.TimeModel = CreateTileTimeModel.WindowWithDuration;
        else state.TimeModel = CreateTileTimeModel.FixedWindow;

        if (RepeatUntilToggle.IsOn) state.RecurringEndDate = RepeatUntilDate.Date;
        else state.RecurringEndDate = null;

        var total = DurationSelect.Minutes;
        state.WorkHours = total / 60;
        state.WorkMinutes = total % 60;
        state.DurationManuallyEdited = true;

        state.Project = ProjectRow.Project;
        state.Tags = ProjectRow.Tags is null ? new List<string>() : new List<string>(ProjectRow.Tags);
        state.ColorHex = ProjectRow.SelectedColor;
        state.Memo = MemoSection.Memo;
        state.WorkflowKind = CreateTileWorkflowKind.Recurring;
    }

    private void BuildRepeatCombo()
    {
        RepeatCombo.Items.Clear();
        for (var i = 0; i < RepeatOptions.Length; i++)
        {
            var (mode, key) = RepeatOptions[i];
            RepeatCombo.Items.Add(new ComboBoxItem
            {
                Content = Strings.Get(key),
                Tag = mode,
            });
        }
        RepeatCombo.SelectedIndex = 0;
    }

    private void BuildIntervalUnits()
    {
        IntervalUnitCombo.Items.Clear();
        foreach (var (unit, key) in IntervalUnits)
        {
            IntervalUnitCombo.Items.Add(new ComboBoxItem
            {
                Content = Strings.Get(key),
                Tag = unit,
            });
        }
        IntervalUnitCombo.SelectedIndex = 0;
    }

    private void BuildMonthlyKind()
    {
        MonthlyKindCombo.Items.Clear();
        MonthlyKindCombo.Items.Add(new ComboBoxItem { Content = Strings.Get("CreateTile.MonthlyByDay"), Tag = "by_day" });
        MonthlyKindCombo.Items.Add(new ComboBoxItem { Content = Strings.Get("CreateTile.MonthlyByWeekday"), Tag = "by_weekday" });
        MonthlyKindCombo.SelectedIndex = 0;
    }

    private void BuildMonthWeeks()
    {
        MonthlyWeekOfMonthCombo.Items.Clear();
        foreach (var (week, key) in MonthWeeks)
        {
            MonthlyWeekOfMonthCombo.Items.Add(new ComboBoxItem
            {
                Content = Strings.Get(key),
                Tag = week,
            });
        }
        MonthlyWeekOfMonthCombo.SelectedIndex = 0;

        MonthlyWeekdayCombo.Items.Clear();
        var weekdayKeys = new[] { "CreateTile.WeekdaySun", "CreateTile.WeekdayMon", "CreateTile.WeekdayTue", "CreateTile.WeekdayWed", "CreateTile.WeekdayThu", "CreateTile.WeekdayFri", "CreateTile.WeekdaySat" };
        foreach (var key in weekdayKeys)
        {
            MonthlyWeekdayCombo.Items.Add(new ComboBoxItem { Content = Strings.Get(key) });
        }
        MonthlyWeekdayCombo.SelectedIndex = 0;
    }

    private void BuildWeekdays()
    {
        WeekdayChipsHost.Children.Clear();
        var weekdayKeys = new[] { "CreateTile.WeekdaySun", "CreateTile.WeekdayMon", "CreateTile.WeekdayTue", "CreateTile.WeekdayWed", "CreateTile.WeekdayThu", "CreateTile.WeekdayFri", "CreateTile.WeekdaySat" };
        for (var i = 0; i < weekdayKeys.Length; i++)
        {
            var chip = new ToggleButton
            {
                Content = Strings.Get(weekdayKeys[i]),
                Tag = i,
                Padding = new Thickness(8, 2, 8, 2),
                MinHeight = 28,
                CornerRadius = new CornerRadius(14),
                Style = Application.Current?.Resources["SelectorButtonStyle"] as Style,
            };
            chip.Click += OnWeekdayChipClick;
            WeekdayChipsHost.Children.Add(chip);
        }
    }

    private void BuildTimeModels()
    {
        TimeModelCombo.Items.Clear();
        TimeModelCombo.Items.Add(new ComboBoxItem { Content = Strings.Get("CreateTile.TimeModelFixedWindow") });
        TimeModelCombo.Items.Add(new ComboBoxItem { Content = Strings.Get("CreateTile.TimeModelWindowWithDuration") });
        TimeModelCombo.Items.Add(new ComboBoxItem { Content = Strings.Get("CreateTile.TimeModelDurationOnly") });
        TimeModelCombo.SelectedIndex = 0;
    }

    private void BuildTimeRow()
    {
        TimeRowHost.Children.Clear();
        _dailyStart.TimeChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
        _dailyStart.DateChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
        _dailyEnd.TimeChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
        _dailyEnd.DateChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
        _timeOfDay.TimeChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
        _timeOfDay.DateChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateTimeRow()
    {
        TimeRowHost.Children.Clear();
        var mode = CurrentRepeatMode();
        var model = TimeModelCombo.SelectedIndex == 2 ? CreateTileTimeModel.DurationOnly
            : TimeModelCombo.SelectedIndex == 1 ? CreateTileTimeModel.WindowWithDuration
            : CreateTileTimeModel.FixedWindow;

        if (model == CreateTileTimeModel.DurationOnly)
        {
            _hintText.Text = Strings.Get("CreateTile.TimeModelDurationOnlyHint");
            _hintText.Margin = new Thickness(20, 6, 20, 6);
            _hintText.Foreground = new SolidColorBrush(Colors.Gray);
            TimeRowHost.Children.Add(_hintText);
            return;
        }

        if (mode == CreateTileRepeatMode.Interval)
        {
            TimeRowHost.Children.Add(_timeOfDay);
        }
        else if (mode == CreateTileRepeatMode.Daily)
        {
            TimeRowHost.Children.Add(_dailyStart);
            TimeRowHost.Children.Add(_dailyEnd);
        }
        else
        {
            TimeRowHost.Children.Add(_timeOfDay);
        }
    }

    private void SetRepeatMode(CreateTileRepeatMode mode)
    {
        for (var i = 0; i < RepeatCombo.Items.Count; i++)
        {
            if (RepeatCombo.Items[i] is ComboBoxItem item && item.Tag is CreateTileRepeatMode m && m == mode)
            {
                RepeatCombo.SelectedIndex = i;
                return;
            }
        }
    }

    private CreateTileRepeatMode CurrentRepeatMode()
    {
        if (RepeatCombo.SelectedItem is ComboBoxItem item && item.Tag is CreateTileRepeatMode m) return m;
        return CreateTileRepeatMode.Once;
    }

    private void SetWeekdays(IList<int> days)
    {
        for (var i = 0; i < WeekdayChipsHost.Children.Count; i++)
        {
            if (WeekdayChipsHost.Children[i] is ToggleButton btn && btn.Tag is int bit)
            {
                btn.IsChecked = days.Contains(bit);
            }
        }
    }

    private List<int> SelectedWeekdays()
    {
        var list = new List<int>();
        foreach (var child in WeekdayChipsHost.Children)
        {
            if (child is ToggleButton btn && btn.Tag is int bit && btn.IsChecked == true)
            {
                list.Add(bit);
            }
        }
        if (list.Count == 0) list.Add((int)DateTimeOffset.Now.DayOfWeek);
        return list;
    }

    private void OnWeekdayChipClick(object sender, RoutedEventArgs e) => StateChanged?.Invoke(this, EventArgs.Empty);

    private void OnRepeatChanged(object sender, SelectionChangedEventArgs e)
    {
        var mode = CurrentRepeatMode();
        WeekdayPanel.Visibility = mode == CreateTileRepeatMode.Weekly ? Visibility.Visible : Visibility.Collapsed;
        IntervalPanel.Visibility = mode == CreateTileRepeatMode.Interval ? Visibility.Visible : Visibility.Collapsed;
        MonthlyPanel.Visibility = mode == CreateTileRepeatMode.Monthly ? Visibility.Visible : Visibility.Collapsed;
        UpdateMonthlyKindControls();
        UpdateTimeRow();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnIntervalValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => StateChanged?.Invoke(this, EventArgs.Empty);
    private void OnIntervalUnitChanged(object sender, SelectionChangedEventArgs e) => StateChanged?.Invoke(this, EventArgs.Empty);

    private void OnMonthlyKindChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateMonthlyKindControls();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateMonthlyKindControls()
    {
        var byDay = MonthlyKindCombo.SelectedIndex == 0;
        MonthlyDayBox.Visibility = byDay ? Visibility.Visible : Visibility.Collapsed;
        MonthlyWeekOfMonthCombo.Visibility = byDay ? Visibility.Collapsed : Visibility.Visible;
        MonthlyWeekdayCombo.Visibility = byDay ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnMonthlyDayChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => StateChanged?.Invoke(this, EventArgs.Empty);
    private void OnMonthlyWeekChanged(object sender, SelectionChangedEventArgs e) => StateChanged?.Invoke(this, EventArgs.Empty);
    private void OnMonthlyWeekdayChanged(object sender, SelectionChangedEventArgs e) => StateChanged?.Invoke(this, EventArgs.Empty);
    private void OnTimeModelChanged(object sender, SelectionChangedEventArgs e) => UpdateTimeRow();

    private void OnRepeatUntilToggled(object sender, RoutedEventArgs e)
    {
        RepeatUntilDate.IsEnabled = RepeatUntilToggle.IsOn;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
