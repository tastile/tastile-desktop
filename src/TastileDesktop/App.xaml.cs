using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using TastileDesktop.Services;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;
using TastileDesktop.Views;

namespace TastileDesktop;

public partial class App : Application
{
    private static readonly string DebugLogPath = Path.Combine(
        RuntimeProfile.GetLocalAppDataDirectory(),
        "debug.log");

    private static Mutex? _singleInstanceMutex;
    private static readonly string MutexName = $"Global\\TastileDesktopSingleInstance-{RuntimeProfile.Name}";

    public static void DebugLog(string msg)
    {
        try
        {
            var dir = Path.GetDirectoryName(DebugLogPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.AppendAllText(DebugLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        }
        catch { }
    }
    private MainWindow? _mainWindow;
    private TrayIconService? _trayIconService;
    private DaemonManager? _daemonManager;
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
        Log($"XAML UNHANDLED: {e.Exception.GetType().Name}: {e.Exception.Message}");
        Log($"XAML STACK: {e.Exception.StackTrace}");
    }

    private void OnCurrentDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log($"APPDOMAIN UNHANDLED: {ex.GetType().Name}: {ex.Message}");
            Log($"APPDOMAIN STACK: {ex.StackTrace}");
        }
        else
        {
            Log($"APPDOMAIN UNHANDLED NON-EXCEPTION: {e.ExceptionObject}");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log($"TASK UNOBSERVED: {e.Exception.GetType().Name}: {e.Exception.Message}");
        Log($"TASK STACK: {e.Exception.StackTrace}");
    }
    
    private void Log(string msg)
    {
        var path = Path.Combine(Path.GetTempPath(), "tastile-desktop.log");
        File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}");
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
                Log("Secondary instance received OAuth callback");
                OAuthCallbackHandoff.Store(oauthCallback);
            }
            Log("Another instance is already running. Exiting.");
            // 既存インスタンスにフォーカスを送る（将来実装）
            Exit();
            return;
        }

        try
        {
            Log("OnLaunched starting...");
            
            // Register custom URL protocol (tastile://) for OAuth callbacks
            if (!ProtocolHandler.IsProtocolRegistered())
            {
                Log("Registering tastile:// protocol...");
                ProtocolHandler.RegisterProtocol();
            }
            
            if (oauthCallback != null)
            {
                Log("Received OAuth callback");
                await HandleOAuthCallbackAsync(oauthCallback);
                // Don't exit - ensure daemon is running
            }
            
            // Start or connect to daemon
            Log("Starting daemon...");
            _daemonManager = new DaemonManager();
            var daemonStarted = await _daemonManager.EnsureRunningAsync();
            if (daemonStarted)
            {
                Log("Daemon ready");
            }
            else
            {
                Log("WARNING: Daemon failed to start - app will use mock mode");
            }
            
            var apiClient = new Services.CoreApiClient();
            await AuthService.Instance.InitializeAsync(apiClient);
            if (!AuthService.Instance.IsAuthenticated)
            {
                if (!await EnsureAuthenticatedAsync(apiClient, "Authentication required before launch."))
                {
                    Shutdown();
                    return;
                }
            }

            // Create main window
            _mainWindow = new MainWindow();
            Log("MainWindow created");
            await _mainWindow.InitializeAsync();
            if (!AuthService.Instance.IsAuthenticated)
            {
                if (!await EnsureAuthenticatedAsync(apiClient, "Main window initialized without session."))
                {
                    Shutdown();
                    return;
                }
            }

            Log("MainWindow initialized");
            ApplyAppearance(_appearanceService.GetCurrentSnapshot());
            
            // Setup tray icon
            Log("Creating TrayIconService...");
            _trayIconService = new TrayIconService(_mainWindow.ViewModel, apiClient);
            Log("Initializing tray icon...");
            _trayIconService.Initialize(_mainWindow);
            Log("Tray icon initialized");

            _ = CheckForUpdatesOnLaunchAsync();
            
            // Handle window close to minimize to tray instead
            _mainWindow.Closed += OnMainWindowClosed;
            
            // Show window unless --minimized flag is present
            if (!cmdArgs.Contains("--minimized"))
            {
                Log("Showing quick panel...");
                _mainWindow.ShowPanel();
                Log("Quick panel shown");

                if (cmdArgs.Contains("--debug-open-create"))
                {
                    Log("Debug opening Create Tile...");
                    _mainWindow.DebugOpenCreateTileWindow();
                }
            }
            else
            {
                Log("Starting minimized");
            }
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.GetType().Name}: {ex.Message}");
            Log($"StackTrace: {ex.StackTrace}");
            throw;
        }
    }
    
    private async Task HandleOAuthCallbackAsync(string callbackUrl)
    {
        var result = ProtocolHandler.ParseOAuthCallback(callbackUrl);
        if (result == null)
        {
            Log("Invalid OAuth callback URL");
            return;
        }
        
        var (code, state) = result.Value;
        Log("OAuth callback parsed");

        if (!OAuthCallbackHandoff.MatchesExpectedState(state))
        {
            Log("Ignoring OAuth callback because state did not match the expected value.");
            return;
        }

        try
        {
            Log("OAuth callback received by app; daemon-managed localhost callback remains the source of truth.");
        }
        catch (Exception ex)
        {
            Log($"OAuth callback handoff processing failed: {ex.Message}");
        }

        OAuthCallbackHandoff.Store(callbackUrl);
        Log("OAuth callback stored for handoff fallback");
    }

    public async Task<bool> EnsureAuthenticatedAsync(CoreApiClient apiClient, string reason)
    {
        Log($"{reason} Waiting for Google OAuth completion.");
        while (!AuthService.Instance.IsAuthenticated)
        {
            var authWindow = new AuthWindow(apiClient);
            authWindow.Activate();
            var authResult = await authWindow.AuthResultTask;
            if (!authResult.Success)
            {
                Log($"Authentication aborted: {authResult.Error}");
                return false;
            }

            await AuthService.Instance.RefreshSessionFromDaemonAsync(apiClient);
        }

        return true;
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
            Log($"Update check failed: {ex.Message}");
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
            Body: string.IsNullOrWhiteSpace(update.Notes) ? "Restart to install the latest version." : update.Notes,
            Why: "A newer version is available.",
            SuggestedMinutes: null,
            Actions:
            [
                new Models.PromptActionView("install_update", "Restart & Install"),
                new Models.PromptActionView("ignore_update", "Ignore"),
            ],
            ExpiresAt: null,
            Stale: false);

        DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
        {
            PromptToastDisplayService.Instance.ShowPrompt(
                prompt,
                maxActions: 2,
                async actionId =>
                {
                    PromptToastDisplayService.Instance.Hide();
                    if (string.Equals(actionId, "install_update", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Uri.TryCreate(update.DownloadUrl, UriKind.Absolute, out var downloadUri) &&
                            string.Equals(downloadUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = downloadUri.ToString(),
                                UseShellExecute = true,
                            });
                        }
                        else
                        {
                            Log("Rejected startup update install because the download URL was invalid.");
                            return;
                        }

                        Shutdown();
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
        _daemonManager?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _mainWindow?.Close();
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
