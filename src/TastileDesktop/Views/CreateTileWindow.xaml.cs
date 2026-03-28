using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Views;

public sealed partial class CreateTileWindow : Window
{
    private const string CreateCanceledErrorCode = "__create_canceled__";
    private static readonly SolidColorBrush ManualAdjustHighlightBrush = new(Windows.UI.Color.FromArgb(255, 255, 193, 7));
    private static void Log(string msg)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "tastile-desktop.log");
            File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private readonly CoreApiClient _api = new();
    private readonly PromptToastDisplayService _promptToast = PromptToastDisplayService.Instance;
    private readonly bool _isJapanese = CreateTileParityResolver.IsJapanese();
    private CreateTileCatalog _catalog = new([], [], []);
    private readonly HashSet<int> _recurrenceWeekdays = [];
    private bool _titleEdited;
    private bool _applyingSuggestedTitle;
    private bool _durationManuallyEdited;
    private bool _syncingAutoDuration;
    private bool _projectInputFocused;
    private bool _tagInputFocused;
    private string _tileKind = "work";
    private string _objectiveMode = "finish_once";
    private string _recurrenceFrequency = "daily";
    private bool _breakSplitsWork = true;
    private bool _maximizeActive;
    private bool _useStartAt;
    private bool _useEndAt;
    private bool _recurrenceValidFromActive;
    private bool _recurrenceValidToActive;
    private bool _recurrenceUseStartAt = true;
    private bool _recurrenceUseEndAt = true;

    public CreateTileWindow()
    {
        try
        {
            Log("CreateTileWindow ctor start");
            InitializeComponent();
            Log("CreateTileWindow after InitializeComponent");
            FloatingWindowHelper.Configure(this, TitleBarArea, 560, 860);
            Log("CreateTileWindow after Configure");
            WireDynamicHandlers();
            InitTabSelectors();
            Log("CreateTileWindow after InitTabSelectors");

            var now = DateTime.Now;
            RecurrenceIntervalBox.Value = 1;
            MonthlyWeekBox.Value = 1;
            PopulateMonthlyWeekdayOptions();
            WorkHoursBox.Value = 0;
            WorkMinutesBox.Value = 25;
            StartDatePicker.Date = DateTimeOffset.Now;
            StartTimePicker.Time = new TimeSpan(now.Hour, now.Minute, 0);
            EndDatePicker.Date = DateTimeOffset.Now;
            EndTimePicker.Time = new TimeSpan(now.AddHours(1).Hour, now.AddHours(1).Minute, 0);
            RecurrenceStartTimePicker.Time = new TimeSpan(now.Hour, now.Minute, 0);
            RecurrenceEndTimePicker.Time = new TimeSpan(now.AddHours(1).Hour, now.AddHours(1).Minute, 0);
            RecurrenceValidFromDatePicker.Date = DateTimeOffset.Now;
            RecurrenceValidToDatePicker.Date = DateTimeOffset.Now;
            MonthlyWeekdayComboBox.SelectedIndex = now.DayOfWeek switch
            {
                DayOfWeek.Sunday => 0,
                DayOfWeek.Monday => 1,
                DayOfWeek.Tuesday => 2,
                DayOfWeek.Wednesday => 3,
                DayOfWeek.Thursday => 4,
                DayOfWeek.Friday => 5,
                _ => 6,
            };
            _recurrenceWeekdays.Add((int)now.DayOfWeek);

            InitWeekdayStates();
            InitAccentStates();
            InitTabSelectorStates();
            Log("CreateTileWindow after InitTabSelectorStates");
            UpdateVisibility();
            SyncAutoDurationFromSchedule();
            RefreshSuggestedTitle();
            _ = LoadCatalogAsync();
            Log("CreateTileWindow ctor end");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreateTileWindow] CRASH: {ex}");
            Log($"CRASH in ctor: {ex}");
            throw;
        }
    }

    private void InitTabSelectorStates()
    {
        KindSelector.SelectedIndex = 0;
        ModeSelector.SelectedIndex = 0;
        FreqSelector.SelectedIndex = 0;
    }

    private void InitWeekdayStates()
    {
        SetWeekdayChecked(WeekdaySun, _recurrenceWeekdays.Contains(0));
        SetWeekdayChecked(WeekdayMon, _recurrenceWeekdays.Contains(1));
        SetWeekdayChecked(WeekdayTue, _recurrenceWeekdays.Contains(2));
        SetWeekdayChecked(WeekdayWed, _recurrenceWeekdays.Contains(3));
        SetWeekdayChecked(WeekdayThu, _recurrenceWeekdays.Contains(4));
        SetWeekdayChecked(WeekdayFri, _recurrenceWeekdays.Contains(5));
        SetWeekdayChecked(WeekdaySat, _recurrenceWeekdays.Contains(6));
    }

    private void InitAccentStates()
    {
        UseStartAtButton.IsChecked = _useStartAt;
        UseEndAtButton.IsChecked = _useEndAt;
        MaximizeButton.IsChecked = _maximizeActive;
        SplitAllowButton.IsChecked = _breakSplitsWork;
        SplitKeepButton.IsChecked = !_breakSplitsWork;
        RecurrenceUseStartAtButton.IsChecked = _recurrenceUseStartAt;
        RecurrenceUseEndAtButton.IsChecked = _recurrenceUseEndAt;
        RecurrenceValidFromButton.IsChecked = _recurrenceValidFromActive;
        RecurrenceValidToButton.IsChecked = _recurrenceValidToActive;
        InitWeekdayStates();
    }

    private static void SetWeekdayChecked(ToggleButton btn, bool active)
    {
        btn.IsChecked = active;
    }

    private void WireDynamicHandlers()
    {
        TitleTextBox.TextChanged += OnTitleTextChanged;
        ProjectTextBox.TextChanged += OnProjectTextChanged;
        ProjectTextBox.KeyDown += OnProjectTextBoxKeyDown;
        ProjectTextBox.GotFocus += OnProjectTextBoxGotFocus;
        ProjectTextBox.LostFocus += OnProjectTextBoxLostFocus;
        TagTextBox.TextChanged += OnTagTextChanged;
        TagTextBox.KeyDown += OnTagTextBoxKeyDown;
        TagTextBox.GotFocus += OnTagTextBoxGotFocus;
        TagTextBox.LostFocus += OnTagTextBoxLostFocus;
        RecurrenceIntervalBox.ValueChanged += OnRecurrenceIntervalChanged;
        MonthlyWeekBox.ValueChanged += (_, _) => OnScheduleBoundChanged(MonthlyWeekBox, EventArgs.Empty);
        MonthlyWeekdayComboBox.SelectionChanged += OnMonthlyWeekdayChanged;
        StartDatePicker.DateChanged += (_, _) => OnScheduleBoundChanged(StartDatePicker, EventArgs.Empty);
        StartTimePicker.TimeChanged += (_, _) => OnScheduleBoundChanged(StartTimePicker, EventArgs.Empty);
        EndDatePicker.DateChanged += (_, _) => OnScheduleBoundChanged(EndDatePicker, EventArgs.Empty);
        EndTimePicker.TimeChanged += (_, _) => OnScheduleBoundChanged(EndTimePicker, EventArgs.Empty);
        RecurrenceStartTimePicker.TimeChanged += (_, _) => OnScheduleBoundChanged(RecurrenceStartTimePicker, EventArgs.Empty);
        RecurrenceEndTimePicker.TimeChanged += (_, _) => OnScheduleBoundChanged(RecurrenceEndTimePicker, EventArgs.Empty);
        RecurrenceValidFromDatePicker.DateChanged += (_, _) => OnScheduleBoundChanged(RecurrenceValidFromDatePicker, EventArgs.Empty);
        RecurrenceValidToDatePicker.DateChanged += (_, _) => OnScheduleBoundChanged(RecurrenceValidToDatePicker, EventArgs.Empty);
    }

    private void InitTabSelectors()
    {
        KindSelector.ItemsSource = new[] { "タスク", "ラベル" };
        ModeSelector.ItemsSource = new[] { "通常", "定期" };
        FreqSelector.ItemsSource = new[] { "日次", "週次", "月次" };
    }

    private async Task LoadCatalogAsync()
    {
        try
        {
            var tiles = await _api.GetTilesAsync();
            _catalog = CreateTileParityResolver.DeriveCatalog(tiles?.Tiles ?? []);
        }
        catch
        {
            _catalog = new CreateTileCatalog([], [], []);
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            RefreshTitleSuggestions();
            RefreshProjectSuggestions();
            RefreshTagSuggestions();
        });
    }

    private void OnKindSelectionChanged(object? sender, int index)
    {
        _tileKind = index == 0 ? "work" : "label";
        if (_objectiveMode != "recurring" && _objectiveMode != "maximize_within_interval")
        {
            _objectiveMode = "finish_once";
            ModeSelector.SelectedIndex = 0;
        }
        UpdateVisibility();
        SyncAutoDurationFromSchedule();
        RefreshSuggestedTitle();
    }

    private void OnModeSelectionChanged(object? sender, int index)
    {
        _objectiveMode = index == 0 ? "finish_once" : "recurring";
        UpdateVisibility();
        SyncAutoDurationFromSchedule();
        RefreshSuggestedTitle();
    }

    private void OnFreqSelectionChanged(object? sender, int index)
    {
        _recurrenceFrequency = index switch { 1 => "weekly", 2 => "monthly", _ => "daily" };
        RecurrenceSuffixText.Text = _recurrenceFrequency switch
        {
            "weekly" => "週ごと",
            "monthly" => "か月ごと",
            _ => "日ごと",
        };
        WeeklyDaysGrid.Visibility = _recurrenceFrequency == "weekly" ? Visibility.Visible : Visibility.Collapsed;
        MonthlyPatternGrid.Visibility = _recurrenceFrequency == "monthly" ? Visibility.Visible : Visibility.Collapsed;
        SyncAutoDurationFromSchedule();
        RefreshSuggestedTitle();
    }

    private void OnRecurrenceUseStartAtClick(object sender, RoutedEventArgs e)
    {
        _recurrenceUseStartAt = RecurrenceUseStartAtButton.IsChecked == true;
        UpdateVisibility();
        SyncAutoDurationFromSchedule();
        RefreshSuggestedTitle();
    }

    private void OnRecurrenceUseEndAtClick(object sender, RoutedEventArgs e)
    {
        _recurrenceUseEndAt = RecurrenceUseEndAtButton.IsChecked == true;
        UpdateVisibility();
        SyncAutoDurationFromSchedule();
        RefreshSuggestedTitle();
    }

    private void OnSplitAllowClick(object sender, RoutedEventArgs e)
    {
        _breakSplitsWork = true;
        SplitAllowButton.IsChecked = true;
        SplitKeepButton.IsChecked = false;
        UpdateVisibility();
    }

    private void OnSplitKeepClick(object sender, RoutedEventArgs e)
    {
        _breakSplitsWork = false;
        SplitAllowButton.IsChecked = false;
        SplitKeepButton.IsChecked = true;
        UpdateVisibility();
    }

    private void OnUseStartAtClick(object sender, RoutedEventArgs e)
    {
        _useStartAt = UseStartAtButton.IsChecked == true;
        if (_objectiveMode == "maximize_within_interval" && !_useEndAt)
        {
            _objectiveMode = "finish_once";
            ModeSelector.SelectedIndex = 0;
        }
        UpdateVisibility();
        SyncAutoDurationFromSchedule();
        RefreshSuggestedTitle();
    }

    private void OnUseEndAtClick(object sender, RoutedEventArgs e)
    {
        _useEndAt = UseEndAtButton.IsChecked == true;
        if (_objectiveMode == "maximize_within_interval" && !_useEndAt)
        {
            _objectiveMode = "finish_once";
            ModeSelector.SelectedIndex = 0;
            _maximizeActive = false;
            MaximizeButton.IsChecked = false;
        }
        UpdateVisibility();
        SyncAutoDurationFromSchedule();
        RefreshSuggestedTitle();
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        _maximizeActive = MaximizeButton.IsChecked == true;
        if (_maximizeActive)
        {
            _objectiveMode = "maximize_within_interval";
        }
        else
        {
            _objectiveMode = "finish_once";
        }
        UpdateVisibility();
        SyncAutoDurationFromSchedule();
        RefreshSuggestedTitle();
    }

    private void OnWeekdayClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button || button.Tag is not string raw || !int.TryParse(raw, out var day)) return;
        if (_recurrenceWeekdays.Contains(day))
        {
            _recurrenceWeekdays.Remove(day);
            if (_recurrenceWeekdays.Count == 0) { _recurrenceWeekdays.Add(day); SetWeekdayChecked(button, true); return; }
            SetWeekdayChecked(button, false);
        }
        else
        {
            _recurrenceWeekdays.Add(day);
            SetWeekdayChecked(button, true);
        }
    }

    private void OnRecurrenceValidFromClick(object sender, RoutedEventArgs e)
    {
        _recurrenceValidFromActive = RecurrenceValidFromButton.IsChecked == true;
        UpdateVisibility();
        SyncAutoDurationFromSchedule();
    }

    private void OnRecurrenceValidToClick(object sender, RoutedEventArgs e)
    {
        _recurrenceValidToActive = RecurrenceValidToButton.IsChecked == true;
        UpdateVisibility();
        SyncAutoDurationFromSchedule();
    }

    private void UpdateVisibility()
    {
        var isLabel = _tileKind == "label";
        var isRecurring = _objectiveMode == "recurring";
        var showMaximize = !isLabel && !isRecurring && _useEndAt;

        TimingPanel.Visibility = Visibility.Visible;
        StartDatePanel.Visibility = !isRecurring && _useStartAt ? Visibility.Visible : Visibility.Collapsed;
        EndDatePanel.Visibility = !isRecurring && _useEndAt ? Visibility.Visible : Visibility.Collapsed;
        RecurringSchedulePanel.Visibility = isRecurring ? Visibility.Visible : Visibility.Collapsed;
        RecurrenceValidityPanel.Visibility = isRecurring ? Visibility.Visible : Visibility.Collapsed;
        RecurringWindowGrid.Visibility = (_recurrenceUseStartAt || _recurrenceUseEndAt) ? Visibility.Visible : Visibility.Collapsed;
        RecurrenceValidityGrid.Visibility = _recurrenceValidFromActive || _recurrenceValidToActive ? Visibility.Visible : Visibility.Collapsed;
        RecurrenceValidFromDatePicker.Visibility = _recurrenceValidFromActive ? Visibility.Visible : Visibility.Collapsed;
        RecurrenceValidToDatePicker.Visibility = _recurrenceValidToActive ? Visibility.Visible : Visibility.Collapsed;
        RecurrenceStartTimePicker.Visibility = _recurrenceUseStartAt ? Visibility.Visible : Visibility.Collapsed;
        RecurrenceEndTimePicker.Visibility = _recurrenceUseEndAt ? Visibility.Visible : Visibility.Collapsed;
        MaximizeButton.Visibility = showMaximize ? Visibility.Visible : Visibility.Collapsed;
        WorkTargetPanel.Visibility = isLabel ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RefreshSuggestedTitle()
    {
        var suggestion = CreateTileParityResolver.GetSuggestedTitle(BuildDraft(), _isJapanese);
        TitleTextBox.PlaceholderText = suggestion;
        if (_titleEdited) return;
        _applyingSuggestedTitle = true;
        TitleTextBox.Text = suggestion;
        _applyingSuggestedTitle = false;
    }

    private void RefreshTitleSuggestions()
    {
        var query = TitleTextBox.Text?.Trim() ?? string.Empty;
        var items = _catalog.ExistingTitles
            .Where(title => title.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .Take(6)
            .ToList();
        PopulateTitleSuggestionPanel(items, value =>
        {
            TitleTextBox.Text = value;
            _titleEdited = true;
        });
    }

    private void PopulateTitleSuggestionPanel(IReadOnlyList<string> items, Action<string> onClick)
    {
        TitleSuggestionPanel.Children.Clear();
        var visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        TitleSuggestionPanel.Visibility = visibility;
        TitleSuggestionScrollViewer.Visibility = visibility;
        foreach (var item in items)
        {
            var button = CreateSuggestionButton(item, (_, _) => onClick(item));
            ApplyChipButtonStyle(button);
            TitleSuggestionPanel.Children.Add(button);
        }
    }

    private void RefreshProjectSuggestions()
    {
        var query = ProjectTextBox.Text?.Trim() ?? string.Empty;
        var items = _catalog.ExistingProjects
            .Where(project => project.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .Take(8)
            .ToList();
        PopulateDropdownPanel(ProjectSuggestionPanel, ProjectSuggestionBorder, items, CommitProjectSelection, _projectInputFocused, query, _isJapanese ? $"新規作成: {query}" : $"Create \"{query}\"");
    }

    private void RefreshTagSuggestions()
    {
        var query = TagTextBox.Text?.Trim() ?? string.Empty;
        var items = _catalog.ExistingTags
            .Where(tag => tag.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .Where(tag => !CurrentTags().Contains(tag, StringComparer.CurrentCultureIgnoreCase))
            .Take(8)
            .ToList();
        PopulateDropdownPanel(TagSuggestionPanel, TagSuggestionBorder, items, AddTag, _tagInputFocused, query, _isJapanese ? $"新規追加: #{query}" : $"Add new #{query}", "#");
    }

    private void PopulateDropdownPanel(Panel panel, FrameworkElement host, IReadOnlyList<string> items, Action<string> onClick, bool isFocused, string query, string createLabel, string prefix = "")
    {
        panel.Children.Clear();
        foreach (var item in items)
        {
            panel.Children.Add(CreateSuggestionButton($"{prefix}{item}", (_, _) => onClick(item)));
        }
        if (!string.IsNullOrWhiteSpace(query) && !items.Any(item => string.Equals(item, query, StringComparison.CurrentCultureIgnoreCase)))
        {
            panel.Children.Add(CreateSuggestionButton(createLabel, (_, _) => onClick(query)));
        }
        var visibility = isFocused && panel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        panel.Visibility = visibility;
        host.Visibility = visibility;
    }

    private Button CreateSuggestionButton(string content, RoutedEventHandler onClick)
    {
        var button = new Button { Content = content };
        ApplySuggestionButtonStyle(button);
        button.Click += onClick;
        return button;
    }

    private CreateTileDraft BuildDraft()
    {
        var startDate = _useStartAt ? Combine(StartDatePicker.Date, StartTimePicker.Time) : null;
        var endDate = _useEndAt ? Combine(EndDatePicker.Date, EndTimePicker.Time) : null;
        return new CreateTileDraft(
            Title: TitleTextBox.Text,
            TileKind: _tileKind,
            ObjectiveMode: _objectiveMode,
            UseStartAt: _useStartAt,
            UseEndAt: _useEndAt,
            StartAt: startDate,
            EndAt: endDate,
            RecurrenceFrequency: _recurrenceFrequency,
            RecurrenceInterval: (int)Math.Max(1, RecurrenceIntervalBox.Value),
            RecurrenceWeekdays: _recurrenceWeekdays.OrderBy(static value => value).ToList(),
            RecurrenceMonthlyWeek: (int)Math.Max(1, MonthlyWeekBox.Value),
            RecurrenceMonthlyWeekday: Math.Clamp(MonthlyWeekdayComboBox.SelectedIndex, 0, 6),
            RecurrenceUseStartAt: _recurrenceUseStartAt,
            RecurrenceUseEndAt: _recurrenceUseEndAt,
            RecurrenceStartTime: _recurrenceUseStartAt ? RecurrenceStartTimePicker.Time : null,
            RecurrenceEndTime: _recurrenceUseEndAt ? RecurrenceEndTimePicker.Time : null,
            RecurrenceValidFromEnabled: _recurrenceValidFromActive,
            RecurrenceValidToEnabled: _recurrenceValidToActive,
            RecurrenceValidFromDate: _recurrenceValidFromActive ? RecurrenceValidFromDatePicker.Date : null,
            RecurrenceValidToDate: _recurrenceValidToActive ? RecurrenceValidToDatePicker.Date : null,
            WorkHours: (int)Math.Max(0, WorkHoursBox.Value),
            WorkMinutes: (int)Math.Max(0, WorkMinutesBox.Value),
            BreakSplitsWork: _breakSplitsWork,
            Project: ProjectTextBox.Text,
            Tags: CurrentTags(),
            Memo: MemoTextBox.Text);
    }

    private bool TryBuildRequest(out CreateTileRequest request)
    {
        var draft = BuildDraft();
        var title = string.IsNullOrWhiteSpace(draft.Title)
            ? CreateTileParityResolver.GetSuggestedTitle(draft, _isJapanese)
            : draft.Title.Trim();
        var workMinutes = ((draft.WorkHours ?? 0) * 60) + (draft.WorkMinutes ?? 0);
        var hasAnyTemporalConstraint = draft.UseStartAt || draft.UseEndAt;
        var isRecurring = draft.ObjectiveMode == "recurring";

        if (draft.UseStartAt && draft.UseEndAt && draft.StartAt.HasValue && draft.EndAt.HasValue && draft.EndAt <= draft.StartAt)
        {
            request = null!;
            ShowError(_isJapanese ? "日時の前後関係を確認してください。" : "Check the start and end order.");
            return false;
        }

        if (draft.ObjectiveMode == "recurring"
            && draft.RecurrenceUseStartAt && draft.RecurrenceUseEndAt
            && draft.RecurrenceStartTime.HasValue && draft.RecurrenceEndTime.HasValue
            && draft.RecurrenceEndTime <= draft.RecurrenceStartTime)
        {
            request = null!;
            ShowError(_isJapanese ? "定期実行の終了時刻は開始時刻より後にしてください。" : "Recurring end time must be after start time.");
            return false;
        }

        if (draft.ObjectiveMode == "recurring" && draft.RecurrenceInterval <= 0)
        {
            request = null!;
            ShowError(_isJapanese ? "定期実行の間隔は 1 以上にしてください。" : "Recurrence interval must be at least 1.");
            return false;
        }

        if (draft.TileKind == "work" && !isRecurring && hasAnyTemporalConstraint && workMinutes <= 0)
        {
            request = null!;
            ShowError(_isJapanese ? "開始または終了を指定した場合は所要時間が必要です。" : "Duration is required when start or end is specified.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            request = null!;
            ShowError(_isJapanese ? "タイトルを入力してください。" : "Enter a title.");
            return false;
        }

        ErrorTextBlock.Visibility = Visibility.Collapsed;
        request = CreateTileParityResolver.BuildRequest(draft with { Title = title }, _isJapanese);
        return true;
    }

    private static void ApplySuggestionButtonStyle(Button button)
    {
        button.MinHeight = 32;
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Padding = new Thickness(10, 6, 10, 6);
    }

    private static void ApplyChipButtonStyle(Button button)
    {
        button.MinHeight = 28;
        button.Padding = new Thickness(10, 4, 10, 4);
        button.Margin = new Thickness(0, 0, 8, 8);
    }

    private static DateTimeOffset? Combine(DateTimeOffset? date, TimeSpan time)
    {
        if (!date.HasValue) return null;
        var local = date.Value.LocalDateTime.Date.Add(time);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    private List<string> CurrentTags()
        => SelectedTagsPanel.Children.OfType<Button>()
            .Select(static button => button.Tag?.ToString())
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .Select(static tag => tag!).ToList();

    private void CommitProjectSelection(string? rawProject)
    {
        var normalized = rawProject?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            ProjectTextBox.Text = string.Empty;
            RefreshProjectSuggestions();
            return;
        }
        var match = _catalog.ExistingProjects.FirstOrDefault(project => string.Equals(project, normalized, StringComparison.CurrentCultureIgnoreCase));
        ProjectTextBox.Text = match ?? normalized;
        RefreshProjectSuggestions();
    }

    private void SyncAutoDurationFromSchedule()
    {
        if (_durationManuallyEdited) return;
        var autoDuration = CreateTileParityResolver.GetAutoDurationMinutes(BuildDraft());
        if (!autoDuration.HasValue || autoDuration.Value <= 0) return;
        _syncingAutoDuration = true;
        WorkHoursBox.Value = autoDuration.Value / 60;
        WorkMinutesBox.Value = autoDuration.Value % 60;
        _syncingAutoDuration = false;
    }

    private void PopulateMonthlyWeekdayOptions()
    {
        MonthlyWeekdayComboBox.Items.Clear();
        var labels = _isJapanese
            ? new[] { "日", "月", "火", "水", "木", "金", "土" }
            : new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        foreach (var label in labels) MonthlyWeekdayComboBox.Items.Add(new ComboBoxItem { Content = label });
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }

    private void OnTitleTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_applyingSuggestedTitle) return;
        _titleEdited = !string.IsNullOrWhiteSpace(TitleTextBox.Text);
        RefreshSuggestedTitle();
        RefreshTitleSuggestions();
    }

    private void OnProjectTextChanged(object sender, TextChangedEventArgs e) => RefreshProjectSuggestions();
    private void OnProjectTextBoxKeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == Windows.System.VirtualKey.Enter) CommitProjectSelection(ProjectTextBox.Text); }
    private void OnProjectTextBoxGotFocus(object sender, RoutedEventArgs e) { _projectInputFocused = true; RefreshProjectSuggestions(); }
    private async void OnProjectTextBoxLostFocus(object sender, RoutedEventArgs e) { await Task.Delay(100); _projectInputFocused = false; RefreshProjectSuggestions(); }
    private void OnTagTextChanged(object sender, TextChangedEventArgs e) => RefreshTagSuggestions();
    private void OnTagTextBoxGotFocus(object sender, RoutedEventArgs e) { _tagInputFocused = true; RefreshTagSuggestions(); }
    private async void OnTagTextBoxLostFocus(object sender, RoutedEventArgs e) { await Task.Delay(100); _tagInputFocused = false; RefreshTagSuggestions(); }
    private void OnTagTextBoxKeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == Windows.System.VirtualKey.Enter) AddTag(TagTextBox.Text); }

    private void AddTag(string? rawTag)
    {
        var tag = rawTag?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tag)) return;
        if (CurrentTags().Contains(tag, StringComparer.CurrentCultureIgnoreCase)) { TagTextBox.Text = string.Empty; return; }
        Button? button = null;
        button = CreateSuggestionButton($"#{tag} ×", (_, _) => { if (button != null) SelectedTagsPanel.Children.Remove(button); RefreshTagSuggestions(); });
        ApplyChipButtonStyle(button);
        button.Tag = tag;
        SelectedTagsPanel.Children.Add(button);
        TagTextBox.Text = string.Empty;
        RefreshTagSuggestions();
    }

    private void OnRecurrenceIntervalChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        SyncAutoDurationFromSchedule();
        RefreshSuggestedTitle();
    }

    private void OnMonthlyWeekdayChanged(object sender, SelectionChangedEventArgs e)
    {
        SyncAutoDurationFromSchedule();
        RefreshSuggestedTitle();
    }

    private void OnWorkDurationChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_syncingAutoDuration) _durationManuallyEdited = true;
        RefreshSuggestedTitle();
    }

    private void OnScheduleBoundChanged(object sender, object e)
    {
        SyncAutoDurationFromSchedule();
        RefreshSuggestedTitle();
    }


    private async Task<bool> EnsureTileQuotaAvailableAsync()
    {
        try
        {
            var quota = await _api.GetTileQuotaAsync();
            if (quota == null)
            {
                ShowError(_isJapanese ? "タイル上限を確認できませんでした。" : "Could not verify tile limit.");
                return false;
            }

            if (quota.LimitReached)
            {
                ShowError(_isJapanese
                    ? "無料プランの上限（100タイル）に達しました。不要なタイルを整理するか、プランをアップグレードしてください。"
                    : "Free plan limit reached (100 tiles). Archive tiles or upgrade your plan.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            ShowError((_isJapanese ? "タイル上限の確認に失敗しました: " : "Failed to validate tile limit: ") + ex.Message);
            return false;
        }
    }

    private async void OnCreateClick(object sender, RoutedEventArgs e)
    {
        if (!TryBuildRequest(out var request)) return;
        if (!await EnsureTileQuotaAvailableAsync()) return;
        try
        {
            var result = await TryCreateWithConflictResolutionAsync(request);
            if (result == null) { ShowError(_isJapanese ? "Daemon から応答がありません。" : "Daemon did not return a response."); return; }
            if (!result.Ok && string.Equals(result.Error, CreateCanceledErrorCode, StringComparison.Ordinal)) { return; }
            if (!result.Ok) { ShowError(result.Error ?? (_isJapanese ? "タイルの作成に失敗しました。" : "Failed to create tile.")); return; }
            Close();
        }
        catch (Exception ex)
        {
            ShowError((_isJapanese ? "タイルの作成に失敗しました: " : "Failed to create tile: ") + ex.Message);
        }
    }

    private async Task<CommandResponse?> TryCreateWithConflictResolutionAsync(CreateTileRequest request)
    {
        var result = await _api.CreateTileAsync(request);
        if (result?.Prompt?.Kind != "create_conflict")
        {
            return result;
        }

        var choice = await ShowConflictResolutionToastAsync(result.Prompt);
        if (string.IsNullOrWhiteSpace(choice) || string.Equals(choice, "cancel_create", StringComparison.OrdinalIgnoreCase))
        {
            _promptToast.Hide();
            return new CommandResponse(false, [], null, result.Prompt, CreateCanceledErrorCode);
        }

        if (string.Equals(choice, "manual_adjust", StringComparison.OrdinalIgnoreCase))
        {
            var guidance = CreateTileParityResolver.GetManualAdjustGuidance(request, _isJapanese);
            ApplyManualAdjustGuidance(guidance);
            return new CommandResponse(false, [], null, result.Prompt, guidance.Message);
        }

        var retried = request with { ConflictResolution = choice };
        return await _api.CreateTileAsync(retried);
    }

    private async Task<string?> ShowConflictResolutionToastAsync(CreateConflictPrompt prompt)
    {
        var toastPrompt = CreateTileParityResolver.BuildCreateConflictToastPrompt(prompt, _isJapanese);
        var completion = new TaskCompletionSource<string?>();

        _promptToast.ShowPrompt(
            toastPrompt,
            maxActions: Math.Clamp(toastPrompt.Actions.Count, 1, 5),
            async actionId =>
            {
                _promptToast.Hide();
                completion.TrySetResult(actionId);
                await Task.CompletedTask;
            });

        return await completion.Task;
    }

    private void ApplyManualAdjustGuidance(CreateTileManualAdjustGuidance guidance)
    {
        ClearManualAdjustHighlight();

        if (guidance.FocusStart)
        {
            _useStartAt = true;
            UseStartAtButton.IsChecked = true;
            StartDatePanel.Visibility = Visibility.Visible;
            StartDatePicker.BorderBrush = ManualAdjustHighlightBrush;
            StartTimePicker.BorderBrush = ManualAdjustHighlightBrush;
        }

        if (guidance.FocusEnd)
        {
            _useEndAt = true;
            UseEndAtButton.IsChecked = true;
            EndDatePanel.Visibility = Visibility.Visible;
            EndDatePicker.BorderBrush = ManualAdjustHighlightBrush;
            EndTimePicker.BorderBrush = ManualAdjustHighlightBrush;
        }

        UpdateVisibility();

        if (guidance.FocusStart)
        {
            StartDatePicker.Focus(FocusState.Programmatic);
            return;
        }

        if (guidance.FocusEnd)
        {
            EndDatePicker.Focus(FocusState.Programmatic);
        }
    }

    private void ClearManualAdjustHighlight()
    {
        StartDatePicker.ClearValue(Control.BorderBrushProperty);
        StartTimePicker.ClearValue(Control.BorderBrushProperty);
        EndDatePicker.ClearValue(Control.BorderBrushProperty);
        EndTimePicker.ClearValue(Control.BorderBrushProperty);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
