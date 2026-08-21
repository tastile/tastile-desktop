using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using TastileDesktop.Models;
using TastileDesktop.Resources;

namespace TastileDesktop.Views.CreateTile.Bodies;

public sealed partial class DetailedBody : UserControl, ICreateTileBody
{
    public event EventHandler? StateChanged;
#pragma warning disable CS0067 // Event never used in DetailedBody (WorkHoursBox/WorkMinutesBox raise StateChanged instead)
    public event EventHandler? DurationChanged;
#pragma warning restore CS0067

    private bool _useStartAt;
    private bool _useEndAt;
    private bool _recurrenceValidFromActive;
    private bool _recurrenceValidToActive;
    private bool _recurrenceUseStartAt = true;
    private bool _recurrenceUseEndAt = true;
    private bool _maximizeActive;
    private string _tileKind = "work";
    private string _objectiveMode = "finish_once";
    private string _recurrenceFrequency = "daily";
    private readonly HashSet<int> _recurrenceWeekdays = [];

    public DetailedBody()
    {
        InitializeComponent();
        InitTabSelectors();
        PopulateMonthlyWeekdayOptions();
        InitWeekdayStates();
        InitWeekdayLabels();
        InitAccentStates();
        UpdateVisibility();
    }

    private void InitWeekdayLabels()
    {
        WeekdaySun.Content = Strings.Get("CreateTile.WeekdaySun");
        WeekdayMon.Content = Strings.Get("CreateTile.WeekdayMon");
        WeekdayTue.Content = Strings.Get("CreateTile.WeekdayTue");
        WeekdayWed.Content = Strings.Get("CreateTile.WeekdayWed");
        WeekdayThu.Content = Strings.Get("CreateTile.WeekdayThu");
        WeekdayFri.Content = Strings.Get("CreateTile.WeekdayFri");
        WeekdaySat.Content = Strings.Get("CreateTile.WeekdaySat");
    }

    private void InitTabSelectors()
    {
        KindSelector.ItemsSource = new[] { Strings.Get("CreateTile.KindTask"), Strings.Get("CreateTile.KindLabelTile") };
        ModeSelector.ItemsSource = new[] { Strings.Get("CreateTile.CompletionModeNormal"), Strings.Get("CreateTile.CompletionModeRecurring") };
        FreqSelector.ItemsSource = new[] { Strings.Get("CreateTile.FrequencyDaily"), Strings.Get("CreateTile.FrequencyWeekly"), Strings.Get("CreateTile.FrequencyMonthly") };
        KindSelector.SelectedIndex = 0;
        ModeSelector.SelectedIndex = 0;
        FreqSelector.SelectedIndex = 0;
    }

    private void PopulateMonthlyWeekdayOptions()
    {
        MonthlyWeekdayComboBox.Items.Clear();
        var weekdayKeys = new[] { "CreateTile.WeekdaySun", "CreateTile.WeekdayMon", "CreateTile.WeekdayTue", "CreateTile.WeekdayWed", "CreateTile.WeekdayThu", "CreateTile.WeekdayFri", "CreateTile.WeekdaySat" };
        foreach (var key in weekdayKeys) MonthlyWeekdayComboBox.Items.Add(new ComboBoxItem { Content = Strings.Get(key) });
    }

    private void InitWeekdayStates()
    {
        SetWeekday(WeekdaySun, _recurrenceWeekdays.Contains(0));
        SetWeekday(WeekdayMon, _recurrenceWeekdays.Contains(1));
        SetWeekday(WeekdayTue, _recurrenceWeekdays.Contains(2));
        SetWeekday(WeekdayWed, _recurrenceWeekdays.Contains(3));
        SetWeekday(WeekdayThu, _recurrenceWeekdays.Contains(4));
        SetWeekday(WeekdayFri, _recurrenceWeekdays.Contains(5));
        SetWeekday(WeekdaySat, _recurrenceWeekdays.Contains(6));
    }

    private void InitAccentStates()
    {
        UseStartAtButton.IsChecked = _useStartAt;
        UseEndAtButton.IsChecked = _useEndAt;
        MaximizeButton.IsChecked = _maximizeActive;
        SplitAllowButton.IsChecked = true;
        SplitKeepButton.IsChecked = false;
        RecurrenceUseStartAtButton.IsChecked = _recurrenceUseStartAt;
        RecurrenceUseEndAtButton.IsChecked = _recurrenceUseEndAt;
        RecurrenceValidFromButton.IsChecked = _recurrenceValidFromActive;
        RecurrenceValidToButton.IsChecked = _recurrenceValidToActive;
    }

    private static void SetWeekday(ToggleButton btn, bool active) => btn.IsChecked = active;

    public void ApplyState(CreateTileFormState state)
    {
        _tileKind = state.TileKind ?? "work";
        _objectiveMode = state.ObjectiveMode ?? "finish_once";
        _recurrenceFrequency = state.RecurrenceFrequency ?? "daily";
        _useStartAt = state.UseStartAt;
        _useEndAt = state.UseEndAt;
        _recurrenceValidFromActive = state.RecurrenceValidFromEnabled;
        _recurrenceValidToActive = state.RecurrenceValidToEnabled;
        _recurrenceUseStartAt = state.RecurrenceUseStartAt;
        _recurrenceUseEndAt = state.RecurrenceUseEndAt;
        _maximizeActive = _objectiveMode == "maximize_within_interval";

        KindSelector.SelectedIndex = _tileKind == "label" ? 1 : 0;
        if (_objectiveMode == "recurring") ModeSelector.SelectedIndex = 1;
        else ModeSelector.SelectedIndex = 0;
        FreqSelector.SelectedIndex = _recurrenceFrequency switch
        {
            "weekly" => 1,
            "monthly" => 2,
            _ => 0,
        };
        MaximizeButton.IsChecked = _maximizeActive;
        SplitAllowButton.IsChecked = state.BreakSplitsWork;
        SplitKeepButton.IsChecked = !state.BreakSplitsWork;
        UseStartAtButton.IsChecked = _useStartAt;
        UseEndAtButton.IsChecked = _useEndAt;
        RecurrenceUseStartAtButton.IsChecked = _recurrenceUseStartAt;
        RecurrenceUseEndAtButton.IsChecked = _recurrenceUseEndAt;
        RecurrenceValidFromButton.IsChecked = _recurrenceValidFromActive;
        RecurrenceValidToButton.IsChecked = _recurrenceValidToActive;

        if (state.StartAt.HasValue)
        {
            StartDatePicker.Date = state.StartAt.Value;
            StartTimePicker.Time = state.StartAt.Value.TimeOfDay;
        }
        if (state.EndAt.HasValue)
        {
            EndDatePicker.Date = state.EndAt.Value;
            EndTimePicker.Time = state.EndAt.Value.TimeOfDay;
        }
        if (state.RecurrenceStartTime.HasValue) RecurrenceStartTimePicker.Time = state.RecurrenceStartTime.Value;
        if (state.RecurrenceEndTime.HasValue) RecurrenceEndTimePicker.Time = state.RecurrenceEndTime.Value;
        if (state.RecurrenceValidFromDate.HasValue) RecurrenceValidFromDatePicker.Date = state.RecurrenceValidFromDate.Value;
        if (state.RecurrenceValidToDate.HasValue) RecurrenceValidToDatePicker.Date = state.RecurrenceValidToDate.Value;
        WorkHoursBox.Value = state.WorkHours ?? 0;
        WorkMinutesBox.Value = state.WorkMinutes ?? 0;
        if (state.RecurrenceInterval is int interval) RecurrenceIntervalBox.Value = interval;
        if (state.RecurrenceMonthlyWeek is int mweek) MonthlyWeekBox.Value = mweek;
        MonthlyWeekdayComboBox.SelectedIndex = Math.Clamp(state.RecurrenceMonthlyWeekday ?? 0, 0, 6);
        _recurrenceWeekdays.Clear();
        if (state.RecurrenceWeekdays is { Count: > 0 })
        {
            foreach (var d in state.RecurrenceWeekdays) _recurrenceWeekdays.Add(d);
        }
        else
        {
            _recurrenceWeekdays.Add((int)DateTimeOffset.Now.DayOfWeek);
        }
        InitWeekdayStates();

        ProjectRow.Project = state.Project ?? string.Empty;
        ProjectRow.Tags = state.Tags is null ? new List<string>() : new List<string>(state.Tags);
        ProjectRow.Swatches = new List<string> { "#3b82f6", "#10b981", "#a855f7", "#f59e0b", "#ef4444", "#6b7280" };
        ProjectRow.SelectedColor = state.ColorHex;
        MemoSection.Memo = state.Memo ?? string.Empty;

        UpdateVisibility();
    }

    public void WriteState(CreateTileFormState state)
    {
        _tileKind = KindSelector.SelectedIndex == 0 ? "work" : "label";
        _objectiveMode = ModeSelector.SelectedIndex == 0 ? "finish_once" : "recurring";
        _recurrenceFrequency = FreqSelector.SelectedIndex switch { 1 => "weekly", 2 => "monthly", _ => "daily" };

        state.TileKind = _tileKind;
        state.ObjectiveMode = _objectiveMode;
        state.RecurrenceFrequency = _recurrenceFrequency;
        state.UseStartAt = UseStartAtButton.IsChecked == true;
        state.UseEndAt = UseEndAtButton.IsChecked == true;
        if (state.UseStartAt) state.StartAt = Combine(StartDatePicker.Date, StartTimePicker.Time);
        else state.StartAt = null;
        if (state.UseEndAt) state.EndAt = Combine(EndDatePicker.Date, EndTimePicker.Time);
        else state.EndAt = null;
        state.RecurrenceUseStartAt = RecurrenceUseStartAtButton.IsChecked == true;
        state.RecurrenceUseEndAt = RecurrenceUseEndAtButton.IsChecked == true;
        state.RecurrenceStartTime = state.RecurrenceUseStartAt ? RecurrenceStartTimePicker.Time : null;
        state.RecurrenceEndTime = state.RecurrenceUseEndAt ? RecurrenceEndTimePicker.Time : null;
        state.RecurrenceInterval = (int)Math.Max(1, RecurrenceIntervalBox.Value);
        state.RecurrenceWeekdays = new List<int>(_recurrenceWeekdays);
        state.RecurrenceMonthlyWeek = (int)Math.Max(1, MonthlyWeekBox.Value);
        state.RecurrenceMonthlyWeekday = Math.Clamp(MonthlyWeekdayComboBox.SelectedIndex, 0, 6);
        state.RecurrenceValidFromEnabled = _recurrenceValidFromActive;
        state.RecurrenceValidToEnabled = _recurrenceValidToActive;
        state.RecurrenceValidFromDate = state.RecurrenceValidFromEnabled ? RecurrenceValidFromDatePicker.Date : null;
        state.RecurrenceValidToDate = state.RecurrenceValidToEnabled ? RecurrenceValidToDatePicker.Date : null;
        state.WorkHours = (int)Math.Max(0, WorkHoursBox.Value);
        state.WorkMinutes = (int)Math.Max(0, WorkMinutesBox.Value);
        state.DurationManuallyEdited = (state.WorkHours > 0 || state.WorkMinutes > 0);
        state.BreakSplitsWork = SplitAllowButton.IsChecked == true;
        state.Project = ProjectRow.Project;
        state.Tags = ProjectRow.Tags is null ? new List<string>() : new List<string>(ProjectRow.Tags);
        state.ColorHex = ProjectRow.SelectedColor;
        state.Memo = MemoSection.Memo;
        state.WorkflowKind = CreateTileWorkflowKind.Detailed;
    }

    private static DateTimeOffset? Combine(DateTimeOffset? date, TimeSpan time)
    {
        if (!date.HasValue) return null;
        var local = date.Value.LocalDateTime.Date.Add(time);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    private void OnKindChanged(object? sender, int index)
    {
        _tileKind = index == 0 ? "work" : "label";
        UpdateVisibility();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnModeChanged(object? sender, int index)
    {
        _objectiveMode = index == 0 ? "finish_once" : "recurring";
        UpdateVisibility();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnFreqChanged(object? sender, int index)
    {
        _recurrenceFrequency = index switch { 1 => "weekly", 2 => "monthly", _ => "daily" };
        RecurrenceSuffix.Text = _recurrenceFrequency switch
        {
            "weekly" => Strings.Get("CreateTile.RecurrenceSuffixWeekly"),
            "monthly" => Strings.Get("CreateTile.RecurrenceSuffixMonthly"),
            _ => Strings.Get("CreateTile.RecurrenceSuffixDaily"),
        };
        WeeklyDaysGrid.Visibility = _recurrenceFrequency == "weekly" ? Visibility.Visible : Visibility.Collapsed;
        MonthlyPatternGrid.Visibility = _recurrenceFrequency == "monthly" ? Visibility.Visible : Visibility.Collapsed;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        _maximizeActive = MaximizeButton.IsChecked == true;
        _objectiveMode = _maximizeActive ? "maximize_within_interval" : "finish_once";
        UpdateVisibility();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnUseStartAtClick(object sender, RoutedEventArgs e)
    {
        _useStartAt = UseStartAtButton.IsChecked == true;
        UpdateVisibility();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnUseEndAtClick(object sender, RoutedEventArgs e)
    {
        _useEndAt = UseEndAtButton.IsChecked == true;
        UpdateVisibility();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSplitAllowClick(object sender, RoutedEventArgs e)
    {
        SplitAllowButton.IsChecked = true;
        SplitKeepButton.IsChecked = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSplitKeepClick(object sender, RoutedEventArgs e)
    {
        SplitAllowButton.IsChecked = false;
        SplitKeepButton.IsChecked = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnRecurrenceUseStartAtClick(object sender, RoutedEventArgs e)
    {
        _recurrenceUseStartAt = RecurrenceUseStartAtButton.IsChecked == true;
        UpdateVisibility();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnRecurrenceUseEndAtClick(object sender, RoutedEventArgs e)
    {
        _recurrenceUseEndAt = RecurrenceUseEndAtButton.IsChecked == true;
        UpdateVisibility();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnRecurrenceValidFromClick(object sender, RoutedEventArgs e)
    {
        _recurrenceValidFromActive = RecurrenceValidFromButton.IsChecked == true;
        UpdateVisibility();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnRecurrenceValidToClick(object sender, RoutedEventArgs e)
    {
        _recurrenceValidToActive = RecurrenceValidToButton.IsChecked == true;
        UpdateVisibility();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWeekdayClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button || button.Tag is not string raw || !int.TryParse(raw, out var day)) return;
        if (_recurrenceWeekdays.Contains(day))
        {
            _recurrenceWeekdays.Remove(day);
            if (_recurrenceWeekdays.Count == 0) { _recurrenceWeekdays.Add(day); SetWeekday(button, true); return; }
            SetWeekday(button, false);
        }
        else
        {
            _recurrenceWeekdays.Add(day);
            SetWeekday(button, true);
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateVisibility()
    {
        var isLabel = _tileKind == "label";
        var isRecurring = _objectiveMode == "recurring";
        var showMaximize = !isLabel && !isRecurring && _useEndAt;
        StartDatePanel.Visibility = !isRecurring && _useStartAt ? Visibility.Visible : Visibility.Collapsed;
        EndDatePanel.Visibility = !isRecurring && _useEndAt ? Visibility.Visible : Visibility.Collapsed;
        RecurringSchedulePanel.Visibility = isRecurring ? Visibility.Visible : Visibility.Collapsed;
        RecurrenceValidityPanel.Visibility = isRecurring ? Visibility.Visible : Visibility.Collapsed;
        RecurringWindowGrid.Visibility = (_recurrenceUseStartAt || _recurrenceUseEndAt) ? Visibility.Visible : Visibility.Collapsed;
        RecurrenceValidityGrid.Visibility = _recurrenceValidFromActive || _recurrenceValidToActive ? Visibility.Visible : Visibility.Collapsed;
        MaximizeButton.Visibility = showMaximize ? Visibility.Visible : Visibility.Collapsed;
        WorkTargetPanel.Visibility = isLabel ? Visibility.Collapsed : Visibility.Visible;
    }
}
