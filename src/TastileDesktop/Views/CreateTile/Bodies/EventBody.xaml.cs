using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TastileDesktop.Models;
using TastileDesktop.Resources;

namespace TastileDesktop.Views.CreateTile.Bodies;

public sealed partial class EventBody : UserControl, ICreateTileBody
{
    public event EventHandler? StateChanged;
#pragma warning disable CS0067 // Event never used in EventBody (no internal duration control)
    public event EventHandler? DurationChanged;
#pragma warning restore CS0067

    public EventBody()
    {
        InitializeComponent();
        AllDayLabel.Text = Strings.Get("CreateTile.AllDay");
        StartDateTime.TimeVisible = true;
        EndDateTime.TimeVisible = true;
    }

    public void ApplyState(CreateTileFormState state)
    {
        if (state.StartAt.HasValue)
        {
            StartDateTime.DateValue = state.StartAt.Value;
            StartDateTime.TimeValue = state.StartAt.Value.TimeOfDay;
        }
        if (state.EndAt.HasValue)
        {
            EndDateTime.DateValue = state.EndAt.Value;
            EndDateTime.TimeValue = state.EndAt.Value.TimeOfDay;
        }
        AllDayToggle.IsOn = state.TimeOfDayMode == CreateTileTimeOfDayMode.AllDay;
        ProjectRow.Project = state.Project ?? string.Empty;
        ProjectRow.Tags = state.Tags is null ? new List<string>() : new List<string>(state.Tags);
        ProjectRow.Swatches = new List<string> { "#3b82f6", "#10b981", "#a855f7", "#f59e0b", "#ef4444", "#6b7280" };
        ProjectRow.SelectedColor = state.ColorHex;
        MemoSection.Memo = state.Memo ?? string.Empty;
    }

    public void WriteState(CreateTileFormState state)
    {
        var startAt = Combine(StartDateTime.DateValue, StartDateTime.TimeValue);
        var endAt = Combine(EndDateTime.DateValue, EndDateTime.TimeValue);
        state.StartAt = startAt;
        state.EndAt = endAt;
        state.UseStartAt = startAt.HasValue;
        state.UseEndAt = endAt.HasValue;
        state.TimeOfDayMode = AllDayToggle.IsOn ? CreateTileTimeOfDayMode.AllDay : CreateTileTimeOfDayMode.Range;
        state.Project = ProjectRow.Project;
        state.Tags = ProjectRow.Tags is null ? new List<string>() : new List<string>(ProjectRow.Tags);
        state.ColorHex = ProjectRow.SelectedColor;
        state.Memo = MemoSection.Memo;
        state.WorkflowKind = CreateTileWorkflowKind.Event;
    }

    private static DateTimeOffset? Combine(DateTimeOffset? date, TimeSpan? time)
    {
        if (!date.HasValue) return null;
        var local = date.Value.LocalDateTime.Date.Add(time ?? TimeSpan.Zero);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    private void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private void OnStartDateChanged(object sender, DateTimeOffset? e) => NotifyStateChanged();
    private void OnStartTimeChanged(object sender, TimeSpan? e) => NotifyStateChanged();
    private void OnEndDateChanged(object sender, DateTimeOffset? e) => NotifyStateChanged();
    private void OnEndTimeChanged(object sender, TimeSpan? e) => NotifyStateChanged();
    private void OnAllDayToggled(object sender, RoutedEventArgs e)
    {
        var allDay = AllDayToggle.IsOn;
        StartDateTime.TimeVisible = !allDay;
        EndDateTime.TimeVisible = !allDay;
        NotifyStateChanged();
    }
}
