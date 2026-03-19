using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using TastileDesktop.Services;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;

namespace TastileDesktop;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TrayIconService? _trayIconService;
    private DaemonManager? _daemonManager;
    private readonly SettingsService _settingsService = new();
    private bool _isShuttingDown = false;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        SettingsService.GlobalSettingsChanged += OnSettingsChanged;
        
        // Initialize toast notifications
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;
    }

    private void OnSettingsChanged(object? sender, TastileSettings settings)
    {
        var queue = _mainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (queue == null)
        {
            ThemeManager.ApplyTheme(settings.ThemeMode, Resources);
            FloatingWindowHelper.RefreshOpenWindows(settings.ThemeMode);
            return;
        }

        queue.TryEnqueue(() =>
        {
            ThemeManager.ApplyTheme(settings.ThemeMode, Resources);
            FloatingWindowHelper.RefreshOpenWindows(settings.ThemeMode);
        });
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
        try
        {
            Log("OnLaunched starting...");
            
            // Register custom URL protocol (tastile://) for OAuth callbacks
            if (!ProtocolHandler.IsProtocolRegistered())
            {
                Log("Registering tastile:// protocol...");
                ProtocolHandler.RegisterProtocol();
            }
            
            // Check if launched via custom URL (OAuth callback)
            var cmdArgs = Environment.GetCommandLineArgs();
            var oauthCallback = cmdArgs.FirstOrDefault(a => a.StartsWith("tastile://"));
            
            if (oauthCallback != null)
            {
                Log($"Received OAuth callback: {oauthCallback}");
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
            
            // Create main window
            _mainWindow = new MainWindow();
            Log("MainWindow created");
            
            // Setup tray icon
            Log("Creating TrayIconService...");
            _trayIconService = new TrayIconService(_mainWindow.ViewModel, new Services.CoreApiClient());
            Log("Initializing tray icon...");
            _trayIconService.Initialize(_mainWindow);
            Log("Tray icon initialized");
            
            // Handle window close to minimize to tray instead
            _mainWindow.Closed += OnMainWindowClosed;
            
            // Show window unless --minimized flag is present
            if (!cmdArgs.Contains("--minimized"))
            {
                Log("Activating window...");
                _mainWindow.Activate();
                Log("Window activated");
                _mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        ThemeManager.ApplyTheme(_settingsService.Current.ThemeMode, Resources);
                        FloatingWindowHelper.RefreshOpenWindows(_settingsService.Current.ThemeMode);
                        Log($"Deferred theme applied: '{_settingsService.Current.ThemeMode}'");
                    }
                    catch (Exception ex)
                    {
                        Log($"Deferred theme apply failed: {ex.GetType().Name}: {ex.Message}");
                    }
                });
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
        Log($"OAuth code received, state: {state}");
        
        // TODO: Send code and state to daemon to complete OAuth
        // For now, just log it
        await Task.Delay(100);
        Log("OAuth callback processed");
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
        _trayIconService?.Dispose();
        _daemonManager?.Dispose();
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

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    public static void Hide(this Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        ShowWindow(hwnd, SW_HIDE);
    }

    public static void Show(this Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        ShowWindow(hwnd, SW_SHOW);
    }
}
