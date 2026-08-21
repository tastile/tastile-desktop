using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;
using TastileDesktop.Views;

namespace TastileDesktop;

public sealed partial class MainWindow : Window
{
    private static readonly HashSet<string> ClockOnlyPropertyNames =
    [
        nameof(MainViewModel.MainCountdownText),
        nameof(MainViewModel.QuickBarTimerText),
        nameof(MainViewModel.QuickBarProgressValue),
        nameof(MainViewModel.MainRunningProgressPercent),
        nameof(MainViewModel.ExecutionStatusDetail),
        nameof(MainViewModel.QuickPanelLeadingText),
    ];

    private readonly SettingsService _settings = new();
    private readonly NativeQuickPanelWindow _nativePanel;
    private readonly List<Window> _ownedWindows = [];
    private readonly Dictionary<Type, Window> _ownedWindowByType = [];
    private bool _isPinned;
    private bool _isPanelVisible;
    private int _activatingWindowCount;
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _topMostGuardTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private DateTimeOffset _lastFocusRefresh = DateTimeOffset.MinValue;
    private bool _runningDragActive;
    private bool _nextCandidatesDragActive;
    private double _runningDragStartX;
    private double _runningDragStartOffset;
    private double _nextDragStartX;
    private double _nextDragStartOffset;
    public SolidColorBrush CreateActionIconForegroundBrush { get; }
    public SolidColorBrush IntegrationsActionIconForegroundBrush { get; }


    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        CreateActionIconForegroundBrush = ResolveThemeBrush(QuickPanelIconStyleResolver.Resolve(QuickPanelActionRole.PrimaryCreation).ForegroundBrushKey);
        IntegrationsActionIconForegroundBrush = ResolveThemeBrush(QuickPanelIconStyleResolver.Resolve(QuickPanelActionRole.SecondaryUtility).ForegroundBrushKey);
        InitializeComponent();
        RunningTasksScrollViewer.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnRunningTasksDragStart), true);
        RunningTasksScrollViewer.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnRunningTasksDragMove), true);
        RunningTasksScrollViewer.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnRunningTasksDragEnd), true);
        RunningTasksScrollViewer.AddHandler(UIElement.PointerExitedEvent, new PointerEventHandler(OnRunningTasksDragEnd), true);
        NextCandidatesScrollViewer.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnNextCandidatesDragStart), true);
        NextCandidatesScrollViewer.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnNextCandidatesDragMove), true);
        NextCandidatesScrollViewer.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnNextCandidatesDragEnd), true);
        NextCandidatesScrollViewer.AddHandler(UIElement.PointerExitedEvent, new PointerEventHandler(OnNextCandidatesDragEnd), true);
        FloatingWindowHelper.ConfigurePanel(this, 892, 88);
        _nativePanel = new NativeQuickPanelWindow(HandleNativePanelActionAsync);
        ApplyPinnedState(_settings.Current.QuickBarAlwaysOnTop);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _settings.SettingsChanged += (_, _) =>
        {
            if (_isPanelVisible)
            {
                FloatingWindowHelper.ForcePositionUpdate(this, _settings.Current);
            }
        };
        AuthService.Instance.AuthStateChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateAccountUI);
        _clockTimer.Tick += (_, _) => OnClockTick();
        _clockTimer.Start();
        _topMostGuardTimer.Tick += (_, _) => OnTopMostGuardTick();
        Activated += OnWindowActivated;
        Closed += (_, _) =>
        {
            _topMostGuardTimer.Stop();
            _nativePanel.Dispose();
        };
        UpdateAccountUI();
        UpdateQuickPanelUI();
        OnClockTick();
    }

    private static SolidColorBrush ResolveThemeBrush(string key)
    {
        return Application.Current.Resources[key] as SolidColorBrush
            ?? (SolidColorBrush)Application.Current.Resources["PrimaryForegroundBrush"];
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateQuickPanelUI);
        if (string.IsNullOrEmpty(e.PropertyName) || !ClockOnlyPropertyNames.Contains(e.PropertyName))
        {
            RefreshNativePanel();
        }
    }

    private void UpdateQuickPanelUI()
    {
        // ViewModel handles clock/date countdown binding; no-op here.
    }

    public async Task InitializeAsync()
    {
        await CognitoAuthService.Instance.TryLoadFromStoreAsync();
        await ViewModel.InitializeAsync();
        RefreshNativePanel();
        UpdateAccountUI();
    }

    public void ShowPanel()
    {
        _isPanelVisible = true;
        _nativePanel.Hide();
        FloatingWindowHelper.PlaceQuickPanel(this, _settings.Current);
        WindowExtensions.Show(this);
        UpdateTopMostGuard();
        Activate();
    }

    public void DebugOpenCreateTileWindow()
    {
        OpenCreateTileWindow();
    }

    private void RefreshNativePanel()
    {
        if (!_isPanelVisible)
        {
            return;
        }

        _nativePanel.Hide();
        FloatingWindowHelper.PlaceQuickPanel(this, _settings.Current);
    }

    private void UpdateAccountUI()
    {
        if (AuthService.Instance.IsAuthenticated)
        {
            // AccountMenuItem.Text = "Sign Out";
            return;
        }

        // AccountMenuItem.Text = "Sign In";
    }

    private void ApplyPinnedState(bool pinned)
    {
        _isPinned = FloatingWindowHelper.SetAlwaysOnTop(this, pinned);
        UpdateTopMostGuard();
        // PinIcon.Opacity = _isPinned ? 1.0 : 0.72;
        RefreshNativePanel();
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        TogglePin();
    }

    public void TogglePin()
    {
        ApplyPinnedState(!_isPinned);
        _settings.Update(settings => settings.QuickBarAlwaysOnTop = _isPinned);
    }

    private async void OnAccountClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!AuthService.Instance.IsAuthenticated)
            {
                var authWindow = new AuthWindow();
                authWindow.Activate();
                var tcs = new TaskCompletionSource();
                void OnClosed(object s, WindowEventArgs a) => tcs.TrySetResult();
                authWindow.Closed += OnClosed;
                await tcs.Task;
                authWindow.Closed -= OnClosed;

                if (AuthService.Instance.IsAuthenticated)
                {
                    await ViewModel.RefreshAsync();
                    ViewModel.StatusMessage = "Signed in";
                }
                UpdateAccountUI();
                return;
            }

            await AuthService.Instance.SignOutAsync();
            try
            {
                await ViewModel.RefreshAsync();
                ViewModel.StatusMessage = "Signed out";
            }
            catch (Exception ex)
            {
                Log($"[OnAccountClick] post-signout refresh failed: {ex.Message}");
                ViewModel.StatusMessage = "Signed out (refresh failed)";
            }
            finally
            {
                UpdateAccountUI();
            }
        }
        catch (Exception ex)
        {
            Log($"[OnAccountClick] failed: {ex.Message}");
            ViewModel.StatusMessage = $"Authentication failed: {ex.Message}";
            UpdateAccountUI();
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await RefreshDataAsync();
    }

    public Task RefreshDataAsync()
    {
        return ViewModel.RefreshAsync();
    }

    private void OnRunningTasksDragStart(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _runningDragActive = true;
        _runningDragStartX = e.GetCurrentPoint(RunningTasksScrollViewer).Position.X;
        _runningDragStartOffset = RunningTasksScrollViewer.HorizontalOffset;
        RunningTasksScrollViewer.CapturePointer(e.Pointer);
    }

    private void OnRunningTasksDragMove(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_runningDragActive) return;
        var currentX = e.GetCurrentPoint(RunningTasksScrollViewer).Position.X;
        var delta = _runningDragStartX - currentX;
        RunningTasksScrollViewer.ChangeView(Math.Max(0, _runningDragStartOffset + delta), null, null, disableAnimation: true);
        e.Handled = true;
    }

    private void OnRunningTasksDragEnd(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_runningDragActive) return;
        _runningDragActive = false;
        RunningTasksScrollViewer.ReleasePointerCapture(e.Pointer);
    }

    private void OnNextCandidatesDragStart(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _nextCandidatesDragActive = true;
        _nextDragStartX = e.GetCurrentPoint(NextCandidatesScrollViewer).Position.X;
        _nextDragStartOffset = NextCandidatesScrollViewer.HorizontalOffset;
        NextCandidatesScrollViewer.CapturePointer(e.Pointer);
    }

    private void OnNextCandidatesDragMove(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_nextCandidatesDragActive) return;
        var currentX = e.GetCurrentPoint(NextCandidatesScrollViewer).Position.X;
        var delta = _nextDragStartX - currentX;
        NextCandidatesScrollViewer.ChangeView(Math.Max(0, _nextDragStartOffset + delta), null, null, disableAnimation: true);
        e.Handled = true;
    }

    private void OnNextCandidatesDragEnd(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_nextCandidatesDragActive) return;
        _nextCandidatesDragActive = false;
        NextCandidatesScrollViewer.ReleasePointerCapture(e.Pointer);
    }

    private async void OnRunningTileClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string tileId || string.IsNullOrWhiteSpace(tileId))
        {
            return;
        }
        ViewModel.FocusRunningTile(tileId);
        // Core に prompt を要求
        await RequestPromptForTileAsync(tileId);
    }

    private async void OnNextPrimaryTileClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.NextUpTileId is not string tileId || string.IsNullOrWhiteSpace(tileId))
        {
            return;
        }
        // Core に prompt を要求
        await RequestPromptForTileAsync(tileId);
    }

    private async void OnNextCandidateTileClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string tileId || string.IsNullOrWhiteSpace(tileId))
        {
            return;
        }
        // Core に prompt を要求
        await RequestPromptForTileAsync(tileId);
    }

    private async void OnTaskStatusIconClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string tileId || string.IsNullOrWhiteSpace(tileId))
        {
            return;
        }
        // Core に prompt を要求
        await RequestPromptForTileAsync(tileId);
    }

    private async Task RequestPromptForTileAsync(string tileId)
    {
        try
        {
            Log($"[RequestPromptForTileAsync] Requesting prompt for tile: {tileId}");
            App.DebugLog($"[RequestPromptForTileAsync] Requesting prompt for tile: {tileId}");
            
            var response = await ViewModel.ApiClient.RequestPromptAsync(tileId);
            
            Log($"[RequestPromptForTileAsync] Response: ok={response?.Ok}, hasPrompt={response?.Prompt != null}, error={response?.Error}");
            App.DebugLog($"[RequestPromptForTileAsync] Response: ok={response?.Ok}, hasPrompt={response?.Prompt != null}, error={response?.Error}");
            
            if (response?.Ok == true && response.Prompt != null)
            {
                Log($"[RequestPromptForTileAsync] Injecting prompt: {response.Prompt.Title}");
                App.DebugLog($"[RequestPromptForTileAsync] Injecting prompt: {response.Prompt.Title}");
                // Core から返された prompt を ViewModel に注入
                ViewModel.InjectPrompt(response.Prompt);
            }
            else if (response?.Error != null)
            {
                Log($"[RequestPromptForTileAsync] Error: {response.Error}");
                App.DebugLog($"[RequestPromptForTileAsync] Error: {response.Error}");
            }
        }
        catch (Exception ex)
        {
            Log($"[RequestPromptForTileAsync] Exception: {ex.Message}");
            App.DebugLog($"[RequestPromptForTileAsync] Exception: {ex.Message}");
        }
    }

    private async void OnPendingPromptActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string actionId || string.IsNullOrWhiteSpace(actionId))
        {
            return;
        }
        await ViewModel.RespondToPromptAsync(actionId, null);
    }

    private void OnOpenExecuteWindowClick(object sender, RoutedEventArgs e)
    {
        OpenExecuteWindow();
    }

    private void OnOpenTilesWindowClick(object sender, RoutedEventArgs e)
    {
        OpenTilesWindow();
    }

    private void OnOpenTimelineWindowClick(object sender, RoutedEventArgs e)
    {
        OpenTimelineWindow();
    }

    private void OnOpenCreateTileWindowClick(object sender, RoutedEventArgs e)
    {
        OpenCreateTileWindow();
    }

    private void OnOpenIntegrationsWindowClick(object sender, RoutedEventArgs e)
    {
        OpenIntegrationsWindow();
    }

    private void OnOpenSettingsWindowClick(object sender, RoutedEventArgs e)
    {
        OpenSettingsWindow();
    }

    public void OpenSettingsWindow()
    {
        OpenOwnedWindow(() => new SettingsWindow());
    }

    public void OpenExecuteWindow()
    {
        OpenOwnedWindow(() => new ExecuteWindow());
    }

    public void OpenTilesWindow()
    {
        OpenOwnedWindow(() => new TilesWindow(ViewModel.PollingService));
    }

    public void OpenTimelineWindow()
    {
        OpenOwnedWindow(() => new TimelineWindow());
    }

    public void OpenCreateTileWindow()
    {
        OpenOwnedWindow(() => new CreateTileWindow());
    }

    public void OpenIntegrationsWindow()
    {
        OpenOwnedWindow(() => new IntegrationsWindow());
    }

    private void OnHidePanelClick(object sender, RoutedEventArgs e)
    {
        HidePanel();
    }

    public void HidePanel()
    {
        _isPanelVisible = false;
        _nativePanel.Hide();
        UpdateTopMostGuard();
        WindowExtensions.Hide(this);
    }

    private void UpdateTopMostGuard()
    {
        if (_isPinned && _isPanelVisible)
        {
            FloatingWindowHelper.ReassertAlwaysOnTop(this, true);
            if (!_topMostGuardTimer.IsEnabled)
            {
                _topMostGuardTimer.Start();
            }
            return;
        }

        if (_topMostGuardTimer.IsEnabled)
        {
            _topMostGuardTimer.Stop();
        }
    }

    private void OnTopMostGuardTick()
    {
        if (!_isPinned || !_isPanelVisible)
        {
            if (_topMostGuardTimer.IsEnabled)
            {
                _topMostGuardTimer.Stop();
            }
            return;
        }

        if (!FloatingWindowHelper.IsTopMost(this))
        {
            FloatingWindowHelper.ReassertAlwaysOnTop(this, true);
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState is not (WindowActivationState.PointerActivated or WindowActivationState.CodeActivated))
        {
            return;
        }
        if (DateTimeOffset.UtcNow - _lastFocusRefresh < TimeSpan.FromSeconds(1))
        {
            return;
        }
        _lastFocusRefresh = DateTimeOffset.UtcNow;
        _ = ViewModel.RefreshAsync();
    }

    private void OnClockTick()
    {
        ViewModel.NotifyTimeAdvanced();
    }

    private void OnQuitClick(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).Shutdown();
    }

    private Task HandleNativePanelActionAsync(string actionId)
    {
        Log($"HandleNativePanelActionAsync -> {actionId}");
        switch (actionId)
        {
            case "toggle-pin":
                ApplyPinnedState(!_isPinned);
                _settings.Update(settings => settings.QuickBarAlwaysOnTop = _isPinned);
                return Task.CompletedTask;
            case "open-tiles":
                OpenTilesWindow();
                return Task.CompletedTask;
            case "open-timeline":
                OpenTimelineWindow();
                return Task.CompletedTask;
            case "open-execute":
                OpenExecuteWindow();
                return Task.CompletedTask;
            case "open-create":
                OpenCreateTileWindow();
                return Task.CompletedTask;
            case "open-integrations":
                OpenIntegrationsWindow();
                return Task.CompletedTask;
            case "open-settings":
                OpenSettingsWindow();
                return Task.CompletedTask;
        }
        return Task.CompletedTask;
    }

    private NativeQuickPanelSnapshot CreateSnapshot()
    {
        var actions = new List<NativeQuickPanelAction>();
        if (!string.IsNullOrWhiteSpace(ViewModel.QuickPanelPrimaryActionId))
        {
            actions.Add(new NativeQuickPanelAction(ViewModel.QuickPanelPrimaryActionId!, ViewModel.QuickPanelPrimaryGlyph));
        }

        if (!string.IsNullOrWhiteSpace(ViewModel.QuickPanelSecondaryActionId))
        {
            actions.Add(new NativeQuickPanelAction(ViewModel.QuickPanelSecondaryActionId!, ViewModel.QuickPanelSecondaryGlyph));
        }

        if (!string.Equals(ViewModel.QuickPanelPrimaryActionId, "add-tile", StringComparison.Ordinal))
        {
            actions.Add(new NativeQuickPanelAction("open-create", "\uE710"));
        }

        actions.Add(new NativeQuickPanelAction("toggle-pin", "\uE718"));

        return new NativeQuickPanelSnapshot(
            LeadingText: ViewModel.QuickPanelLeadingText,
            Title: ViewModel.QuickBarTitle,
            StatusKind: !ViewModel.IsConnected
                ? NativeQuickPanelStatusKind.Offline
                : ViewModel.IsOnBreak
                    ? NativeQuickPanelStatusKind.Break
                    : ViewModel.IsWorking
                        ? NativeQuickPanelStatusKind.Working
                        : NativeQuickPanelStatusKind.Ready,
            PromptWaiting: ViewModel.HasPendingPrompt,
            ShowProgress: ViewModel.QuickBarProgressVisibility == Visibility.Visible,
            ProgressPercent: ViewModel.QuickBarProgressValue,
            Actions: actions);
    }

    private QuickPanelBounds ComputePanelBounds()
    {
        var displays = PromptToastDisplayEnumerator.GetDisplays();
        var preferredDisplayId = string.Equals(_settings.Current.PromptToastDisplayMode, PromptToastDisplayModes.ActiveWindowDisplay, StringComparison.Ordinal)
            ? PromptToastForegroundDisplayResolver.GetCurrentDisplayId(displays)
            : null;
        var fallback = displays.FirstOrDefault(static display => display.IsPrimary)?.WorkArea ?? new Windows.Graphics.RectInt32(0, 0, 1920, 1080);
        var workArea = QuickPanelPlacementResolver.ResolveWorkArea(displays, _settings.Current.PromptToastDisplayMode, preferredDisplayId, fallback);
        return QuickPanelPlacementResolver.ComputeBounds(workArea, _settings.Current.QuickPanelAnchor, _settings.Current.QuickPanelOrientation);
    }

    private void OpenOwnedWindow<TWindow>(Func<TWindow> factory) where TWindow : Window
    {
        if (_ownedWindowByType.TryGetValue(typeof(TWindow), out var existing))
        {
            Log($"ReuseOwnedWindow<{typeof(TWindow).Name}>");
            FloatingWindowHelper.CenterOnQuickPanelDisplay(existing, _settings.Current);
            _activatingWindowCount++;
            existing.Activate();
            _activatingWindowCount--;
            Log($"ActivatedOwnedWindow<{typeof(TWindow).Name}>");
            return;
        }

        Log($"OpenOwnedWindow<{typeof(TWindow).Name}>");
        Log($"CreateOwnedWindow<{typeof(TWindow).Name}> before factory");
        var window = factory();
        Log($"CreateOwnedWindow<{typeof(TWindow).Name}> after factory");
        _ownedWindows.Add(window);
        _ownedWindowByType[typeof(TWindow)] = window;
        window.Closed += OnOwnedWindowClosed;
        FloatingWindowHelper.CenterOnQuickPanelDisplay(window, _settings.Current);
        Log($"ActivateOwnedWindow<{typeof(TWindow).Name}> before Activate");
        window.Activate();
        Log($"ActivateOwnedWindow<{typeof(TWindow).Name}> after Activate");
        Log($"ActivatedOwnedWindow<{typeof(TWindow).Name}>");
    }

    private void OnOwnedWindowClosed(object sender, WindowEventArgs args)
    {
        if (_activatingWindowCount > 0)
        {
            return;
        }

        if (sender is not Window window)
        {
            return;
        }

        window.Closed -= OnOwnedWindowClosed;
        _ownedWindows.Remove(window);
        _ownedWindowByType.Remove(window.GetType());
    }

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
}
