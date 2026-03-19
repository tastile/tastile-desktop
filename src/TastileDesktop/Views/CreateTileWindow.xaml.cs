using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using TastileDesktop.Services;
using Windows.UI;

namespace TastileDesktop.Views;

/// <summary>
/// Create Tile window matching Web dashboard design.
/// </summary>
public sealed partial class CreateTileWindow : Window
{
    private readonly CoreApiClient _api = new();
    private string _tileKind = "work"; // work, label
    private string _objectiveMode = "finish_once"; // finish_once, recurring
    private string _recurrenceFrequency = "daily"; // daily, weekly, monthly
    private bool _breakSplitsWork = true;

    public CreateTileWindow()
    {
        this.InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 520, 800);

        // Initialize UI state
        UpdateKindButtons();
        UpdateModeButtons();
        UpdateRecurrenceButtons();
        UpdateSplitButtons();
        UpdateRecurrenceVisibility();
        UpdateWorkPanelsVisibility();

        // Set default dates/times
        StartDatePicker.Date = DateTimeOffset.Now;
        StartTimePicker.Time = TimeSpan.FromHours(DateTime.Now.Hour).Add(TimeSpan.FromMinutes(DateTime.Now.Minute));
        EndDatePicker.Date = DateTimeOffset.Now;
        EndTimePicker.Time = TimeSpan.FromHours(DateTime.Now.Hour + 1).Add(TimeSpan.FromMinutes(DateTime.Now.Minute));
    }

    #region Event Handlers

    private void OnTitleChanged(object sender, TextChangedEventArgs e)
    {
        // Auto-generate title if empty (similar to web)
        if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
        {
            TitleTextBox.PlaceholderText = GetSuggestedTitle();
        }
    }

    private void OnKindWorkClick(object sender, RoutedEventArgs e)
    {
        _tileKind = "work";
        UpdateKindButtons();
        UpdateWorkPanelsVisibility();
    }

    private void OnKindLabelClick(object sender, RoutedEventArgs e)
    {
        _tileKind = "label";
        UpdateKindButtons();
        UpdateWorkPanelsVisibility();
    }

    private void OnModeFinishClick(object sender, RoutedEventArgs e)
    {
        _objectiveMode = "finish_once";
        UpdateModeButtons();
        UpdateRecurrenceVisibility();
    }

    private void OnModeRecurringClick(object sender, RoutedEventArgs e)
    {
        _objectiveMode = "recurring";
        UpdateModeButtons();
        UpdateRecurrenceVisibility();
    }

    private void OnFreqDailyClick(object sender, RoutedEventArgs e)
    {
        _recurrenceFrequency = "daily";
        UpdateRecurrenceButtons();
    }

    private void OnFreqWeeklyClick(object sender, RoutedEventArgs e)
    {
        _recurrenceFrequency = "weekly";
        UpdateRecurrenceButtons();
    }

    private void OnFreqMonthlyClick(object sender, RoutedEventArgs e)
    {
        _recurrenceFrequency = "monthly";
        UpdateRecurrenceButtons();
    }

    private void OnUseStartAtClick(object sender, RoutedEventArgs e)
    {
        StartDatePanel.Visibility = UseStartAtToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnUseEndAtClick(object sender, RoutedEventArgs e)
    {
        EndDatePanel.Visibility = UseEndAtToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSplitAllowClick(object sender, RoutedEventArgs e)
    {
        _breakSplitsWork = true;
        UpdateSplitButtons();
    }

    private void OnSplitKeepClick(object sender, RoutedEventArgs e)
    {
        _breakSplitsWork = false;
        UpdateSplitButtons();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private async void OnCreateClick(object sender, RoutedEventArgs e)
    {
        if (!ValidateForm()) return;

        // TODO: Call API to create tile
        var result = await CreateTileAsync();
        
        if (result)
        {
            this.Close();
        }
    }

    #endregion

    #region UI Updates

    private void UpdateKindButtons()
    {
        SetButtonActive(KindWorkButton, _tileKind == "work");
        SetButtonActive(KindLabelButton, _tileKind == "label");
    }

    private void UpdateModeButtons()
    {
        SetButtonActive(ModeFinishButton, _objectiveMode == "finish_once");
        SetButtonActive(ModeRecurringButton, _objectiveMode == "recurring");
    }

    private void UpdateRecurrenceButtons()
    {
        SetButtonActive(FreqDailyButton, _recurrenceFrequency == "daily");
        SetButtonActive(FreqWeeklyButton, _recurrenceFrequency == "weekly");
        SetButtonActive(FreqMonthlyButton, _recurrenceFrequency == "monthly");

        // Update suffix text
        var interval = (int)RecurrenceIntervalBox.Value;
        RecurrenceSuffixText.Text = _recurrenceFrequency switch
        {
            "daily" => interval == 1 ? "day" : "days",
            "weekly" => interval == 1 ? "week" : "weeks",
            "monthly" => interval == 1 ? "month" : "months",
            _ => "days"
        };
    }

    private void UpdateSplitButtons()
    {
        SetButtonActive(SplitAllowButton, _breakSplitsWork);
        SetButtonActive(SplitKeepButton, !_breakSplitsWork);
    }

    private void UpdateRecurrenceVisibility()
    {
        RecurrencePanel.Visibility = _objectiveMode == "recurring" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateWorkPanelsVisibility()
    {
        var isWork = _tileKind == "work";
        WorkTargetPanel.Visibility = isWork ? Visibility.Visible : Visibility.Collapsed;
        SplitPanel.Visibility = isWork ? Visibility.Visible : Visibility.Collapsed;
        
        // For label tiles, hide objective mode selection (labels don't have objectives)
        if (!isWork)
        {
            _objectiveMode = "finish_once";
            UpdateModeButtons();
            UpdateRecurrenceVisibility();
        }
    }

    private void SetButtonActive(Button button, bool active)
    {
        if (active)
        {
            button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        }
        else
        {
            button.Style = null;
            button.Background = (SolidColorBrush)Application.Current.Resources["ControlFillColorDefaultBrush"];
        }
    }

    #endregion

    #region Helpers

    private string GetSuggestedTitle()
    {
        if (_tileKind == "label")
        {
            return "Period label";
        }

        var hours = (int)WorkHoursBox.Value;
        var minutes = (int)WorkMinutesBox.Value;
        var duration = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";

        if (_objectiveMode == "recurring")
        {
            return $"Recurring task ({duration})";
        }

        return $"Task ({duration})";
    }

    private bool ValidateForm()
    {
        var title = TitleTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ShowError("Please enter a title.");
            return false;
        }

        // Validate temporal order
        if (UseStartAtToggle.IsChecked == true && UseEndAtToggle.IsChecked == true)
        {
            var start = StartDatePicker.Date.Date.Add(StartTimePicker.Time);
            var end = EndDatePicker.Date.Date.Add(EndTimePicker.Time);
            if (end <= start)
            {
                ShowError("End time must be after start time.");
                return false;
            }
        }

        // Validate work target for work tiles
        if (_tileKind == "work" && _objectiveMode != "recurring")
        {
            var hasSchedule = UseStartAtToggle.IsChecked == true || UseEndAtToggle.IsChecked == true;
            if (hasSchedule)
            {
                var hours = (int)WorkHoursBox.Value;
                var minutes = (int)WorkMinutesBox.Value;
                if (hours == 0 && minutes == 0)
                {
                    ShowError("Please set a work target duration.");
                    return false;
                }
            }
        }

        // Validate recurrence
        if (_objectiveMode == "recurring")
        {
            var interval = (int)RecurrenceIntervalBox.Value;
            if (interval <= 0)
            {
                ShowError("Recurrence interval must be at least 1.");
                return false;
            }
        }

        ErrorTextBlock.Visibility = Visibility.Collapsed;
        return true;
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }

    private async System.Threading.Tasks.Task<bool> CreateTileAsync()
    {
        try
        {
            var title = TitleTextBox.Text.Trim();
            var nextAction = BuildNextAction();
            var doneDefinition = BuildDoneDefinition();
            var memoText = string.IsNullOrWhiteSpace(MemoTextBox.Text) ? null : MemoTextBox.Text.Trim();

            var result = await _api.CreateTileAsync(title, nextAction, doneDefinition);
            if (result == null)
            {
                ShowError("Daemon did not return a response.");
                return false;
            }

            if (!result.Ok)
            {
                ShowError(result.Error ?? "Failed to create tile.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(memoText) && !string.IsNullOrWhiteSpace(result.TileId))
            {
                var memoResult = await _api.AttachMemoAsync(result.TileId, memoText);
                if (memoResult != null && !memoResult.Ok)
                {
                    ShowError(memoResult.Error ?? "Tile created, but memo attach failed.");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            ShowError($"Failed to create tile: {ex.Message}");
            return false;
        }
    }

    private string? BuildNextAction()
    {
        if (_tileKind == "work")
        {
            var hours = (int)WorkHoursBox.Value;
            var minutes = (int)WorkMinutesBox.Value;
            return hours > 0 || minutes > 0
                ? $"Work target: {hours}h {minutes}m"
                : null;
        }

        return null;
    }

    private string? BuildDoneDefinition()
    {
        var parts = new[]
        {
            !string.IsNullOrWhiteSpace(ProjectTextBox.Text) ? $"Project: {ProjectTextBox.Text.Trim()}" : null,
            !string.IsNullOrWhiteSpace(TagsTextBox.Text) ? $"Tags: {TagsTextBox.Text.Trim()}" : null,
            _objectiveMode == "recurring" ? $"Recurring {_recurrenceFrequency} x{(int)RecurrenceIntervalBox.Value}" : null,
        };

        var result = string.Join(" / ", parts.Where(static p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    #endregion
}
