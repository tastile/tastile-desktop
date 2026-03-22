using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;
using TastileDesktop.Views;

namespace TastileDesktop;

public sealed partial class MainWindow : Window
{
    private readonly SettingsService _settings = new();
    private readonly NativeQuickPanelWindow _nativePanel;
    private readonly List<Window> _ownedWindows = [];
    private readonly Dictionary<Type, Window> _ownedWindowByType = [];
    private bool _isPinned;
    private bool _isPanelVisible;
    private int _activatingWindowCount;
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _runningDragActive;
    private bool _nextCandidatesDragActive;
    private double _runningDragStartX;
    private double _runningDragStartOffset;
    private double _nextDragStartX;
    private double _nextDragStartOffset;


    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
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
        _settings.SettingsChanged += (_, _) => RefreshNativePanel();
        AuthService.Instance.AuthStateChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateAccountUI);
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        Closed += (_, _) => _nativePanel.Dispose();
        UpdateAccountUI();
        UpdateQuickPanelUI();
        UpdateClock();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateQuickPanelUI);
        RefreshNativePanel();
    }

    private void UpdateQuickPanelUI()
    {
        // Countdown text is data-bound; only keep clock fresh here.
        UpdateClock();
    }

    public async Task InitializeAsync()
    {
        await AuthService.Instance.InitializeAsync(ViewModel.ApiClient);
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
        Activate();
    }

    public void DebugOpenCreateTileWindow()
    {
        OpenOwnedWindow(() => new CreateTileWindow());
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
        // PinIcon.Opacity = _isPinned ? 1.0 : 0.72;
        RefreshNativePanel();
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        ApplyPinnedState(!_isPinned);
        _settings.Update(settings => settings.QuickBarAlwaysOnTop = _isPinned);
    }

    private async void OnAccountClick(object sender, RoutedEventArgs e)
    {
        if (!AuthService.Instance.IsAuthenticated)
        {
            var authWindow = new AuthWindow(ViewModel.ApiClient);
            authWindow.Activate();
            var result = await authWindow.AuthResultTask;
            if (result.Success)
            {
                await AuthService.Instance.RefreshSessionFromDaemonAsync(ViewModel.ApiClient);
                await ViewModel.RefreshAsync();
                ViewModel.StatusMessage = "Signed in";
                UpdateAccountUI();
            }
            else if (!string.IsNullOrWhiteSpace(result.Error) &&
                     !string.Equals(result.Error, "Authentication window closed", StringComparison.Ordinal))
            {
                ViewModel.StatusMessage = result.Error;
            }

            return;
        }

        await AuthService.Instance.SignOutAsync(ViewModel.ApiClient);
        ViewModel.StatusMessage = "Signed out";
        UpdateAccountUI();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
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
        // 実行中タイルをクリック → Core に StartTile を送る（Core が適切な prompt を返す）
        await ViewModel.StartTileCommand.ExecuteAsync(tileId);
    }

    private async void OnNextPrimaryTileClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string tileId || string.IsNullOrWhiteSpace(tileId))
            return;
        // Next タイルをクリック → Core に StartTile を送る（Core が適切な prompt を返す）
        await ViewModel.StartTileCommand.ExecuteAsync(tileId);
    }

    private async void OnNextCandidateTileClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string tileId || string.IsNullOrWhiteSpace(tileId))
            return;
        // Candidate タイルをクリック → Core に StartTile を送る（Core が適切な prompt を返す）
        await ViewModel.StartTileCommand.ExecuteAsync(tileId);
    }

    private async void OnTaskStatusIconClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string tileId || string.IsNullOrWhiteSpace(tileId))
            return;
        // ステータスアイコンをクリック → Core に StartTile を送る（Core が適切な prompt を返す）
        await ViewModel.StartTileCommand.ExecuteAsync(tileId);
    }

    private async void OnPendingPromptActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string actionId || string.IsNullOrWhiteSpace(actionId))
        {
            return;
        }
        await ViewModel.RespondToPromptAsync(actionId);
    }

    private void OnOpenExecuteWindowClick(object sender, RoutedEventArgs e)
    {
        OpenOwnedWindow(() => new ExecuteWindow());
    }

    private void OnOpenTilesWindowClick(object sender, RoutedEventArgs e)
    {
        OpenOwnedWindow(() => new TilesWindow());
    }

    private void OnOpenTimelineWindowClick(object sender, RoutedEventArgs e)
    {
        OpenOwnedWindow(() => new TimelineWindow());
    }

    private void OnOpenCreateTileWindowClick(object sender, RoutedEventArgs e)
    {
        OpenOwnedWindow(() => new CreateTileWindow());
    }

    private void OnOpenSettingsWindowClick(object sender, RoutedEventArgs e)
    {
        OpenOwnedWindow(() => new SettingsWindow());
    }

    private void OnHidePanelClick(object sender, RoutedEventArgs e)
    {
        _isPanelVisible = false;
        _nativePanel.Hide();
        WindowExtensions.Hide(this);
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        DateText.Text = now.ToString("yyyy/MM/dd");
        ClockText.Text = now.ToString("HH:mm:ss");
    }

    private void OnQuitClick(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).Shutdown();
    }

    private async Task HandleNativePanelActionAsync(string actionId)
    {
        Log($"HandleNativePanelActionAsync -> {actionId}");
        switch (actionId)
        {
            case "toggle-pin":
                ApplyPinnedState(!_isPinned);
                _settings.Update(settings => settings.QuickBarAlwaysOnTop = _isPinned);
                return;
            case "open-tiles":
                OpenOwnedWindow(() => new TilesWindow());
                return;
            case "open-timeline":
                OpenOwnedWindow(() => new TimelineWindow());
                return;
            case "open-execute":
                OpenOwnedWindow(() => new ExecuteWindow());
                return;
            case "open-create":
                OpenOwnedWindow(() => new CreateTileWindow());
                return;
            case "open-settings":
                OpenOwnedWindow(() => new SettingsWindow());
                return;
        }
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
