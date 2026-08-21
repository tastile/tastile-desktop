using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TastileDesktop.Models;
using TastileDesktop.Resources;

namespace TastileDesktop.Views.CreateTile.Bodies;

public sealed partial class TaskBody : UserControl, ICreateTileBody
{
    public event EventHandler? StateChanged;
    public event EventHandler? DurationChanged;

    public TaskBody()
    {
        InitializeComponent();
        DurationLabel.Text = Strings.Get("CreateTile.DurationLabel");
        SplitAllowButton.Content = Strings.Get("CreateTile.SplitAllow");
        SplitKeepButton.Content = Strings.Get("CreateTile.SplitKeep");
        DurationSelect.MinutesChanged += (_, _) =>
        {
            DurationChanged?.Invoke(this, EventArgs.Empty);
            StateChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public void ApplyState(CreateTileFormState state)
    {
        var minutes = ((state.WorkHours ?? 0) * 60) + (state.WorkMinutes ?? 0);
        if (minutes > 0) DurationSelect.Minutes = minutes;
        SplitAllowButton.IsChecked = state.BreakSplitsWork;
        SplitKeepButton.IsChecked = !state.BreakSplitsWork;
        if (state.StartAt.HasValue)
        {
            DueDatePicker.Date = state.StartAt.Value;
            DueTimePicker.Time = state.StartAt.Value.TimeOfDay;
        }
        ProjectRow.Project = state.Project ?? string.Empty;
        ProjectRow.Tags = state.Tags is null ? new List<string>() : new List<string>(state.Tags);
        ProjectRow.Swatches = new List<string> { "#3b82f6", "#10b981", "#a855f7", "#f59e0b", "#ef4444", "#6b7280" };
        ProjectRow.SelectedColor = state.ColorHex;
        MemoSection.Memo = state.Memo ?? string.Empty;
    }

    public void WriteState(CreateTileFormState state)
    {
        var total = DurationSelect.Minutes;
        state.WorkHours = total / 60;
        state.WorkMinutes = total % 60;
        state.DurationManuallyEdited = true;
        state.BreakSplitsWork = SplitAllowButton.IsChecked == true;
        if (DueDatePicker.Date != default)
        {
            var local = DueDatePicker.Date.LocalDateTime.Date.Add(DueTimePicker.Time == default ? TimeSpan.Zero : DueTimePicker.Time);
            state.StartAt = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
            state.UseStartAt = true;
        }
        else
        {
            state.StartAt = null;
            state.UseStartAt = false;
        }
        state.Project = ProjectRow.Project;
        state.Tags = ProjectRow.Tags is null ? new List<string>() : new List<string>(ProjectRow.Tags);
        state.ColorHex = ProjectRow.SelectedColor;
        state.Memo = MemoSection.Memo;
        state.WorkflowKind = CreateTileWorkflowKind.Task;
    }

    private void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private void OnSplitAllowClick(object sender, RoutedEventArgs e)
    {
        SplitAllowButton.IsChecked = true;
        SplitKeepButton.IsChecked = false;
        NotifyStateChanged();
    }

    private void OnSplitKeepClick(object sender, RoutedEventArgs e)
    {
        SplitAllowButton.IsChecked = false;
        SplitKeepButton.IsChecked = true;
        NotifyStateChanged();
    }

    private void OnDueDateChanged(object sender, DatePickerValueChangedEventArgs e) => NotifyStateChanged();
    private void OnDueTimeChanged(object sender, TimePickerValueChangedEventArgs e) => NotifyStateChanged();
}
