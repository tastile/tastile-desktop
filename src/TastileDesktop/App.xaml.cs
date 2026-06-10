using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using TastileDesktop.Services;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;
using TastileDesktop.Views;

namespace TastileDesktop;

public partial class App : Application
{
    private static readonly string CallbackHandoffPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tastile", "Auth", "callback-pending.txt");

    private static Mutex? _singleInstanceMutex;
    private static readonly string MutexName = "Global\\TastileDesktopSingleInstance";

    public static void DebugLog(string msg)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "tastile-desktop.log");
            File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}");
        }
        catch { }
    }
    private MainWindow? _mainWindow;
    private TrayIconService? _trayIconService;
    private readonly SettingsService _settingsService = new();
    private readonly AppUpdateService _appUpdateService = new();
    private readonly SystemAppearanceService _appearanceService = SystemAppearanceService.Instance;
    private bool _isShuttingDown = false;
    public MainWindow? MainWindowInstance => _mainWindow;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        SettingsService.GlobalSettingsChanged += OnSettingsChanged;
        _appearanceService.AppearanceChanged += OnAppearanceChanged;

        // Initialize toast notifications
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;
    }

    private void OnSettingsChanged(object? sender, TastileSettings settings)
    {
        var queue = _mainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (queue == null)
        {
            ApplyAppearance(_appearanceService.GetCurrentSnapshot());
            return;
        }

        queue.TryEnqueue(() => ApplyAppearance(_appearanceService.GetCurrentSnapshot()));
    }

    private void OnAppearanceChanged(object? sender, SystemAppearanceSnapshot snapshot)
    {
        var queue = _mainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (queue == null)
        {
            ApplyAppearance(snapshot);
            return;
        }

        queue.TryEnqueue(() => ApplyAppearance(snapshot));
    }

    private void ApplyAppearance(SystemAppearanceSnapshot snapshot)
    {
        ThemeManager.ApplySystemAppearance(snapshot, Resources, _settingsService.Current);
        FloatingWindowHelper.RefreshOpenWindows();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        DebugLog($"XAML UNHANDLED: {e.Exception.GetType().Name}: {e.Exception.Message}");
        DebugLog($"XAML STACK: {e.Exception.StackTrace}");
    }

    private void OnCurrentDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            DebugLog($"APPDOMAIN UNHANDLED: {ex.GetType().Name}: {ex.Message}");
            DebugLog($"APPDOMAIN STACK: {ex.StackTrace}");
        }
        else
        {
            DebugLog($"APPDOMAIN UNHANDLED NON-EXCEPTION: {e.ExceptionObject}");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        DebugLog($"TASK UNOBSERVED: {e.Exception.GetType().Name}: {e.Exception.Message}");
        DebugLog($"TASK STACK: {e.Exception.StackTrace}");
    }

    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        // Re-route to NotificationService
        // This is handled in NotificationService, but we need to ensure the app is running
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var cmdArgs = Environment.GetCommandLineArgs();
        var oauthCallback = cmdArgs.FirstOrDefault(a => a.StartsWith("tastile://", StringComparison.OrdinalIgnoreCase));

        // シングルインスタンスチェック
        bool createdNew;
        _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);

        if (!createdNew)
        {
            if (oauthCallback != null)
            {
                DebugLog("Secondary instance received OAuth callback");
                TryStoreCallbackHandoff(oauthCallback);
            }
            DebugLog("Another instance is already running. Exiting.");
            Exit();
            return;
        }

        try
        {
            DebugLog("OnLaunched starting...");

            // Register custom URL protocol (tastile://) for OAuth callbacks
            if (!ProtocolHandler.IsProtocolRegistered())
            {
                DebugLog("Registering tastile:// protocol...");
                ProtocolHandler.RegisterProtocol();
            }

            // Drain any callback that was handoffed from a previous secondary instance
            var handoffCallback = TryConsumeCallbackHandoff();
            if (handoffCallback != null)
            {
                DebugLog("Consumed OAuth callback handoff from secondary instance");
                await HandleOAuthCallbackAsync(handoffCallback);
            }
            if (oauthCallback != null && handoffCallback == null)
            {
                DebugLog("Received OAuth callback");
                await HandleOAuthCallbackAsync(oauthCallback);
            }

            // Hydrate session from DPAPI store
            await CognitoAuthService.Instance.TryLoadFromStoreAsync();
            if (!CognitoAuthService.Instance.IsAuthenticated)
            {
                var authWindow = new AuthWindow();
                authWindow.Activate();

                var tcs = new TaskCompletionSource<AuthResult>();
                EventHandler onAuthStateChanged = (_, _) =>
                {
                    if (CognitoAuthService.Instance.IsAuthenticated)
                    {
                        tcs.TrySetResult(new AuthResult(true));
                    }
                };
                CognitoAuthService.Instance.AuthStateChanged += onAuthStateChanged;
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                await using var registration = cts.Token.Register(() => tcs.TrySetResult(new AuthResult(false, "timeout")));
                var result = await tcs.Task;
                CognitoAuthService.Instance.AuthStateChanged -= onAuthStateChanged;
                authWindow.Close();
                if (!result.Success)
                {
                    DebugLog($"Authentication aborted: {result.ErrorCode}");
                    Shutdown();
                    return;
                }
            }

            var apiClient = new Services.CoreApiClient(
                AppSettings.ApiBaseUrl,
                AuthService.Instance.GetAccessTokenAsync,
                CognitoAuthService.Instance.RefreshAsync);

            // Create main window
            _mainWindow = new MainWindow();
            DebugLog("MainWindow created");
            await _mainWindow.InitializeAsync();
            DebugLog("MainWindow initialized");

            ApplyAppearance(_appearanceService.GetCurrentSnapshot());

            // Setup tray icon
            DebugLog("Creating TrayIconService...");
            _trayIconService = new TrayIconService(_mainWindow.ViewModel, apiClient, () => Shutdown(), _settingsService);
            DebugLog("Initializing tray icon...");
            _trayIconService.Initialize(_mainWindow);
            DebugLog("Tray icon initialized");

            _ = CheckForUpdatesOnLaunchAsync();

            // Handle window close to minimize to tray instead
            _mainWindow.Closed += OnMainWindowClosed;

            // Show window unless --minimized flag is present
            if (!cmdArgs.Contains("--minimized"))
            {
                DebugLog("Showing quick panel...");
                _mainWindow.ShowPanel();
                DebugLog("Quick panel shown");

                if (cmdArgs.Contains("--debug-open-create"))
                {
                    DebugLog("Debug opening Create Tile...");
                    _mainWindow.DebugOpenCreateTileWindow();
                }

                if (cmdArgs.Contains("--debug-open-timeline"))
                {
                    DebugLog("Debug opening Timeline...");
                    _mainWindow.OpenTimelineWindow();
                }
            }
            else
            {
                DebugLog("Starting minimized");
            }
        }
        catch (Exception ex)
        {
            DebugLog($"ERROR: {ex.GetType().Name}: {ex.Message}");
            DebugLog($"StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    private async Task HandleOAuthCallbackAsync(string callbackUrl)
    {
        var result = ProtocolHandler.ParseOAuthCallback(callbackUrl);
        if (result == null)
        {
            DebugLog("Invalid OAuth callback URL");
            return;
        }

        var (code, state) = result.Value;
        DebugLog("OAuth callback parsed");
        await CognitoAuthService.Instance.HandleAuthorizationCodeAsync(code, state);
    }

    private static void TryStoreCallbackHandoff(string url)
    {
        try
        {
            var dir = Path.GetDirectoryName(CallbackHandoffPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(CallbackHandoffPath, url);
        }
        catch (Exception ex)
        {
            DebugLog($"Failed to store callback handoff: {ex.Message}");
        }
    }

    private static string? TryConsumeCallbackHandoff()
    {
        try
        {
            if (!File.Exists(CallbackHandoffPath)) return null;
            var url = File.ReadAllText(CallbackHandoffPath).Trim();
            File.Delete(CallbackHandoffPath);
            return string.IsNullOrEmpty(url) ? null : url;
        }
        catch (Exception ex)
        {
            DebugLog($"Failed to consume callback handoff: {ex.Message}");
            return null;
        }
    }

    private async Task CheckForUpdatesOnLaunchAsync()
    {
        try
        {
            var settings = _settingsService.Current;
            var manifestUrl = settings.UpdateManifestUrl;
            if (string.IsNullOrWhiteSpace(manifestUrl))
            {
                return;
            }

            var currentVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            var update = await _appUpdateService.CheckForUpdateAsync(manifestUrl, currentVersion);
            if (!_appUpdateService.ShouldPromptForUpdate(update, settings.IgnoredUpdateVersion))
            {
                return;
            }

            ShowStartupUpdateToast(update);
        }
        catch (Exception ex)
        {
            DebugLog($"Update check failed: {ex.Message}");
        }
    }

    private void ShowStartupUpdateToast(AppUpdateInfo update)
    {
        var prompt = new Models.PromptView(
            PromptId: $"startup-update-{Guid.NewGuid():N}",
            Kind: "app_update",
            Severity: "info",
            TileId: null,
            Title: $"Update available: {update.LatestVersion}",
            Body: string.IsNullOrWhiteSpace(update.Notes) ? "Download the installer and install the latest version." : update.Notes,
            Why: "A newer version is available.",
            SuggestedMinutes: null,
            Actions:
            [
                new Models.PromptActionView("install_update", "Install Update"),
                new Models.PromptActionView("ignore_update", "Ignore"),
            ],
            CreatedAt: null,
            ExpiresAt: null,
            Stale: false);

        DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
        {
            PromptToastDisplayService.Instance.ShowPrompt(
                prompt,
                maxActions: 2,
                async (actionId, _) =>
                {
                    PromptToastDisplayService.Instance.Hide();
                    if (string.Equals(actionId, "install_update", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var installerPath = await _appUpdateService.DownloadInstallerAsync(update.DownloadUrl);
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = installerPath,
                                UseShellExecute = true,
                            });
                            Shutdown();
                        }
                        catch (Exception ex)
                        {
                            DebugLog($"Update install failed: {ex.Message}");
                        }
                    }
                    else if (string.Equals(actionId, "ignore_update", StringComparison.OrdinalIgnoreCase))
                    {
                        _settingsService.Update(s => s.IgnoredUpdateVersion = update.LatestVersion);
                    }
                    await Task.CompletedTask;
                });
        });
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        // If we're not shutting down, hide the window instead of closing
        if (!_isShuttingDown && _mainWindow != null)
        {
            args.Handled = true;
            _mainWindow.Hide();
        }
    }

    /// <summary>
    /// Call this when the user explicitly wants to quit from the tray menu.
    /// </summary>
    public void Shutdown()
    {
        _isShuttingDown = true;
        _appearanceService.AppearanceChanged -= OnAppearanceChanged;
        _trayIconService?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _mainWindow?.Close();
        Exit();
    }
}

/// <summary>
/// Extension methods for Window.
/// </summary>
public static class WindowExtensions
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;

    public static void Hide(this Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        ShowWindow(hwnd, SW_HIDE);
    }

    public static void Show(this Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
    }

    public static void BringToFront(this Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }
}
