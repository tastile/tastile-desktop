using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using TastileDesktop.Services;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;

namespace TastileDesktop;

public partial class App : Application
{
    private static readonly string DebugLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tastile", "debug.log");

    private static Mutex? _singleInstanceMutex;
    private const string MutexName = "Global\\TastileDesktopSingleInstance";

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
        // シングルインスタンスチェック
        bool createdNew;
        _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);
        
        if (!createdNew)
        {
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
            await _mainWindow.InitializeAsync();
            Log("MainWindow initialized");
            ApplyAppearance(_appearanceService.GetCurrentSnapshot());
            
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
