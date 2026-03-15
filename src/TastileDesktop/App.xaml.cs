using Microsoft.UI.Xaml;
using TastileDesktop.Services;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;

namespace TastileDesktop;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TrayIconService? _trayIconService;
    private bool _isShuttingDown = false;

    public App()
    {
        this.InitializeComponent();
        
        // Initialize toast notifications
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;
    }
    
    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        // Re-route to NotificationService
        // This is handled in NotificationService, but we need to ensure the app is running
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = new MainWindow();
        
        // Setup tray icon
        _trayIconService = new TrayIconService(_mainWindow.ViewModel, new Services.CoreApiClient());
        _trayIconService.Initialize(_mainWindow);
        
        // Handle window close to minimize to tray instead
        _mainWindow.Closed += OnMainWindowClosed;
        
        // Check for --minimized flag
        var cmdArgs = Environment.GetCommandLineArgs();
        if (cmdArgs.Contains("--minimized"))
        {
            // Start minimized to tray only - don't activate window
        }
        else
        {
            _mainWindow.Activate();
        }
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
