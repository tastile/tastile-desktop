using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TastileDesktop.Models;
using TastileDesktop.Resources;
using TastileDesktop.Services;
using TastileDesktop.Views.CreateTile;
using TastileDesktop.Views.CreateTile.Bodies;

namespace TastileDesktop.Views;

public sealed partial class CreateTileWindow : Window
{
    private const string CreateCanceledErrorCode = "__create_canceled__";
    private static readonly SolidColorBrush ManualAdjustHighlightBrush = new(Windows.UI.Color.FromArgb(255, 255, 193, 7));
    public static string DebugLogPath => Path.Combine(Path.GetTempPath(), "tastile-desktop.log");

    private static void Log(string msg)
    {
        try
        {
            File.AppendAllText(DebugLogPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private readonly CoreApiClient _api = new(
        getAccessToken: Services.AuthService.Instance.GetAccessTokenAsync,
        refreshTokens: Services.CognitoAuthService.Instance.RefreshAsync);
    private readonly PromptToastDisplayService _promptToast = PromptToastDisplayService.Instance;
    private readonly string? _editTileId;
    private CreateTileCatalog _catalog = new([], [], []);
    private readonly CreateTileFormState _state = new();
    private CreateTileWorkflowKind _workflowKind = CreateTileWorkflowKind.Task;
    private ICreateTileBody? _activeBody;
    private bool _titleEdited;

    public CreateTileWindow() : this(null)
    {
    }

    public CreateTileWindow(string? editTileId, EditableTileView? editTile = null)
    {
        _editTileId = editTileId;

        try
        {
            Log("CreateTileWindow ctor start");
            InitializeComponent();
            Log("CreateTileWindow after InitializeComponent");
            FloatingWindowHelper.Configure(this, TitleBarArea, 720, 880);
            Header.CreateButton.Click += OnCreateClick;
            Header.CloseRequested += (_, _) => Close();
            Header.SubmitRequested += (_, _) => OnCreateClick(this, new RoutedEventArgs());
            Header.TitleChanged += (_, text) => OnTitleChanged(text);
            WorkflowTabs.SelectionChanged += (_, kind) => OnWorkflowChanged(kind);
            ApplyWindowTextContract(isEditMode: false);

            if (!string.IsNullOrEmpty(_editTileId))
            {
                ApplyWindowTextContract(isEditMode: true);
                Log($"Edit mode: editTileId={_editTileId}");
                if (editTile != null)
                {
                    var annotation = editTile.Annotation;
                    var objective = editTile.Objective;
                    var interruption = editTile.Interruption;
                    var temporal = editTile.Temporal;
                    var semanticRole = annotation?.SemanticRole ?? "work";
                    var objectiveMode = objective?.ObjectiveMode ?? "finish_once";
                    var targetWorkMin = objective?.TargetWorkMin;
                    var breakSplitsWork = interruption?.BreakSplitsWork ?? true;
                    var fixedStartValue = temporal?.FixedStart;
                    var fixedEndValue = temporal?.FixedEnd;
                    var activeStartValue = temporal?.ActiveStart;
                    var activeEndValue = temporal?.ActiveEnd;
                    var releaseAtValue = temporal?.ReleaseAt;
                    var dueAtValue = temporal?.DueAt;
                    var labels = annotation?.Labels ?? [];
                    var recurrence = objective?.Recurrence;
                    Log($"Edit tile: Title={editTile.Title}, SemanticRole={semanticRole}, ObjectiveMode={objectiveMode}, TargetWorkMin={targetWorkMin}, FixedStart={fixedStartValue}, FixedEnd={fixedEndValue}, BreakSplitsWork={breakSplitsWork}");

                    _state.Title = editTile.Title;
                    _titleEdited = !string.IsNullOrWhiteSpace(editTile.Title);

                    _workflowKind = CreateTileWindowContractResolver.ResolveWorkflowKind(
                        tileKind: semanticRole,
                        objectiveMode: objectiveMode,
                        fixedStart: !string.IsNullOrEmpty(fixedStartValue),
                        fixedEnd: !string.IsNullOrEmpty(fixedEndValue));
                    WorkflowTabs.ActiveKind = _workflowKind;

                    if (targetWorkMin.HasValue)
                    {
                        var total = targetWorkMin.Value;
                        _state.WorkHours = total / 60;
                        _state.WorkMinutes = total % 60;
                        _state.DurationManuallyEdited = true;
                    }

                    _state.BreakSplitsWork = breakSplitsWork;
                    _state.TileKind = semanticRole;
                    _state.ObjectiveMode = objectiveMode;

                    if (TryParseOffset(fixedStartValue ?? activeStartValue, out var startAt))
                    {
                        _state.StartAt = startAt;
                        _state.UseStartAt = true;
                    }
                    if (TryParseOffset(fixedEndValue ?? activeEndValue, out var endAt))
                    {
                        _state.EndAt = endAt;
                        _state.UseEndAt = true;
                    }
                    if (TryParseOffset(releaseAtValue, out var releaseAt))
                    {
                        _state.RecurrenceValidFromDate = releaseAt;
                        _state.RecurrenceValidFromEnabled = true;
                    }
                    if (TryParseOffset(dueAtValue, out var dueAt))
                    {
                        _state.RecurrenceValidToDate = dueAt;
                        _state.RecurrenceValidToEnabled = true;
                    }
                    if (labels.Count > 0)
                    {
                        var split = CreateTileParityResolver.SplitProjectAndTags(labels);
                        _state.Project = split.Project;
                        _state.Tags = split.Tags;
                    }

                    if (recurrence?.Generator.StepMin is int step)
                    {
                        _state.RecurrenceInterval = step / 60 > 0 ? step / 60 : 1;
                    }
                    if (recurrence?.Selector.Expression is { } expr)
                    {
                        if (expr.Contains("freq=daily")) _state.RecurrenceFrequency = "daily";
                        else if (expr.Contains("freq=weekly")) _state.RecurrenceFrequency = "weekly";
                        else if (expr.Contains("freq=monthly")) _state.RecurrenceFrequency = "monthly";
                    }
                    if (recurrence?.Window.StartOffsetMin is int startMin)
                    {
                        _state.RecurrenceStartTime = TimeSpan.FromMinutes(startMin);
                    }
                    if (recurrence?.Window.EndOffsetMin is int endMin)
                    {
                        _state.RecurrenceEndTime = TimeSpan.FromMinutes(endMin);
                    }

                    DeleteButton.Visibility = Visibility.Visible;
                }
            }

            MountBody(_workflowKind);
            ApplyDraftToBody();
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

    private void OnWorkflowChanged(CreateTileWorkflowKind kind)
    {
        if (_workflowKind == kind) return;
        _activeBody?.WriteState(_state);
        _workflowKind = kind;
        MountBody(kind);
        _activeBody?.ApplyState(_state);
        RefreshSuggestedTitle();
    }

    private void MountBody(CreateTileWorkflowKind kind)
    {
        BodyHost.Children.Clear();
        ICreateTileBody body = kind switch
        {
            CreateTileWorkflowKind.Event => new EventBody(),
            CreateTileWorkflowKind.Task => new TaskBody(),
            CreateTileWorkflowKind.Recurring => new RecurringBody(),
            _ => new DetailedBody(),
        };
        body.StateChanged += OnBodyStateChanged;
        body.DurationChanged += OnBodyDurationChanged;
        _activeBody = body;
        BodyHost.Children.Add((UserControl)body);
    }

    private void OnBodyStateChanged(object? sender, EventArgs e)
    {
        _activeBody?.WriteState(_state);
        RefreshSuggestedTitle();
    }

    private void OnBodyDurationChanged(object? sender, EventArgs e)
    {
        _activeBody?.WriteState(_state);
        RefreshSuggestedTitle();
    }

    private void ApplyDraftToBody()
    {
        _activeBody?.ApplyState(_state);
        Header.TitleText = _state.Title;
        Header.TitlePlaceholder = CreateTileParityResolver.GetSuggestedTitle(_state.ToDraft(), IsJapanese());
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
    }

    private void OnTitleChanged(string text)
    {
        _titleEdited = !string.IsNullOrWhiteSpace(text);
        _state.Title = text;
        RefreshSuggestedTitle();
    }

    private void RefreshSuggestedTitle()
    {
        var suggestion = CreateTileParityResolver.GetSuggestedTitle(_state.ToDraft(), IsJapanese());
        Header.TitlePlaceholder = suggestion;
        if (!_titleEdited) Header.TitleText = suggestion;
    }

    private static bool TryParseOffset(string? value, out DateTimeOffset result)
    {
        if (!string.IsNullOrWhiteSpace(value) && DateTimeOffset.TryParse(value, out result))
        {
            return true;
        }
        result = default;
        return false;
    }

    private void ApplyWindowTextContract(bool isEditMode)
    {
        var contract = CreateTileWindowContractResolver.ResolveWindowText(isEditMode, IsJapanese());
        Title = contract.WindowTitle;
        HeadingTextBlock.Text = contract.HeadingText;
        CreateButton.Content = contract.PrimaryButtonText;
    }

    // The submit button lives in the title row (CreateTileHeader) and is
    // exposed to the orchestrator as `CreateButton` so the historical test
    // contract `CreateButton.Content = contract.PrimaryButtonText;` keeps
    // working without coupling the window code to a private field.
    private Button CreateButton => Header.CreateButton;

    private bool TryBuildRequest(out CreateTileRequest request)
    {
        _activeBody?.WriteState(_state);
        var title = string.IsNullOrWhiteSpace(_state.Title)
            ? CreateTileParityResolver.GetSuggestedTitle(_state.ToDraft(), IsJapanese())
            : _state.Title.Trim();
        var workMinutes = ((_state.WorkHours ?? 0) * 60) + (_state.WorkMinutes ?? 0);
        var hasAnyTemporalConstraint = _state.UseStartAt || _state.UseEndAt;
        var isRecurring = _state.ObjectiveMode == "recurring";

        if (_state.UseStartAt && _state.UseEndAt && _state.StartAt.HasValue && _state.EndAt.HasValue && _state.EndAt <= _state.StartAt)
        {
            request = null!;
            ShowError(Strings.Get("CreateTile_ValidationStartEndOrder"));
            return false;
        }
        if (isRecurring
            && _state.RecurrenceUseStartAt && _state.RecurrenceUseEndAt
            && _state.RecurrenceStartTime.HasValue && _state.RecurrenceEndTime.HasValue
            && _state.RecurrenceEndTime <= _state.RecurrenceStartTime)
        {
            request = null!;
            ShowError(Strings.Get("CreateTile_ValidationRecurrenceEndAfterStart"));
            return false;
        }
        if (isRecurring && _state.RecurrenceInterval is not null && _state.RecurrenceInterval <= 0)
        {
            request = null!;
            ShowError(Strings.Get("CreateTile_ValidationRecurrenceIntervalMin"));
            return false;
        }
        if (_state.TileKind == "work" && !isRecurring && hasAnyTemporalConstraint && workMinutes <= 0)
        {
            request = null!;
            ShowError(Strings.Get("CreateTile_ValidationDurationRequired"));
            return false;
        }
        if (string.IsNullOrWhiteSpace(title))
        {
            request = null!;
            ShowError(Strings.Get("CreateTile_ValidationTitleRequired"));
            return false;
        }

        ErrorBanner.Visibility = Visibility.Collapsed;
        _state.Title = title;
        request = CreateTileParityResolver.BuildRequest(_state.ToDraft(), IsJapanese());
        return true;
    }

    private void ShowError(string message)
    {
        ErrorBanner.Title = Strings.Get("CreateTile_CreateError");
        ErrorBanner.Body = message;
        ErrorBanner.Visibility = Visibility.Visible;
    }

    // Gate-time helper: CreateTileParityResolver still needs a boolean to
    // format API-side strings (suggested title + conflict prompt body). UI
    // text in this window goes through Strings.Get() directly so it tracks
    // the live UICulture.
    private static bool IsJapanese() => CreateTileParityResolver.IsJapanese();

    private async Task<bool> EnsureTileQuotaAvailableAsync()
    {
        try
        {
            if (!Services.AuthService.Instance.IsAuthenticated)
            {
                return true;
            }
            var quota = await _api.GetTileQuotaAsync();
            if (quota == null)
            {
                ShowError(Strings.Get("CreateTile_QuotaCheckFailed"));
                return false;
            }
            if (quota.LimitReached)
            {
                ShowError(Strings.Get("CreateTile_QuotaLimitReached"));
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            ShowError(string.Format(Strings.Get("CreateTile_QuotaCheckException"), ex.Message));
            return false;
        }
    }

    private async void OnCreateClick(object sender, RoutedEventArgs e)
    {
        if (!TryBuildRequest(out var request)) return;
        var isEdit = !string.IsNullOrEmpty(_editTileId);
        if (!isEdit && !await EnsureTileQuotaAvailableAsync()) return;
        try
        {
            var result = isEdit
                ? await _api.UpdateTileAsync(_editTileId!, request)
                : await TryCreateWithConflictResolutionAsync(request);
            if (result == null)
            {
                ShowError(Strings.Get("CreateTile_NoResponse"));
                return;
            }
            if (!result.Ok && string.Equals(result.Error, CreateCanceledErrorCode, StringComparison.Ordinal))
            {
                return;
            }
            if (!result.Ok)
            {
                ShowError(result.Error ?? Strings.Get(isEdit ? "CreateTile_UpdateFailed" : "CreateTile_CreateFailed"));
                return;
            }
            // Server-side recalculation handles the post-update state; no local tick needed.
            Close();
        }
        catch (Exception ex)
        {
            var prefix = _editTileId is null
                ? string.Format(Strings.Get("CreateTile_CreateFailedWithMessage"), ex.Message)
                : string.Format(Strings.Get("CreateTile_UpdateFailedWithMessage"), ex.Message);
            ShowError(prefix);
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
            var guidance = CreateTileParityResolver.GetManualAdjustGuidance(request, IsJapanese());
            ApplyManualAdjustGuidance(guidance);
            return new CommandResponse(false, [], null, result.Prompt, guidance.Message);
        }
        var retried = request with { ConflictResolution = choice };
        return await _api.CreateTileAsync(retried);
    }

    private async Task<string?> ShowConflictResolutionToastAsync(CreateConflictPrompt prompt)
    {
        var toastPrompt = CreateTileParityResolver.BuildCreateConflictToastPrompt(prompt, IsJapanese());
        var completion = new TaskCompletionSource<string?>();

        _promptToast.ShowPrompt(
            toastPrompt,
            maxActions: Math.Clamp(toastPrompt.Actions.Count, 1, 5),
            async (actionId, _) =>
            {
                _promptToast.Hide();
                completion.TrySetResult(actionId);
                await Task.CompletedTask;
            });

        return await completion.Task;
    }

    private void ApplyManualAdjustGuidance(CreateTileManualAdjustGuidance guidance)
    {
        if (_workflowKind != CreateTileWorkflowKind.Event && _workflowKind != CreateTileWorkflowKind.Task)
        {
            _workflowKind = CreateTileWorkflowKind.Task;
            WorkflowTabs.ActiveKind = _workflowKind;
            MountBody(_workflowKind);
        }
        if (_activeBody is not EventBody and not TaskBody) return;

        _state.UseStartAt = guidance.FocusStart || _state.UseStartAt;
        _state.UseEndAt = guidance.FocusEnd || _state.UseEndAt;
        _activeBody.ApplyState(_state);
        ShowError(guidance.Message);
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_editTileId)) return;
        try
        {
            var result = await _api.DeleteTileAsync(_editTileId);
            if (result?.Ok == true)
            {
                Close();
            }
            else
            {
                ShowError(Strings.Get("CreateTile_DeleteFailed"));
            }
        }
        catch (Exception ex)
        {
            ShowError(string.Format(Strings.Get("CreateTile_ErrorPrefix"), ex.Message));
        }
    }
}
