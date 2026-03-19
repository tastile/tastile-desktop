using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TastileDesktop.ViewModels;

namespace TastileDesktop.Services;

/// <summary>
/// Manages the system tray icon and context menu.
/// </summary>
public class TrayIconService : IDisposable
{
    private readonly MainViewModel _viewModel;
    private readonly CoreApiClient _api;
    private TaskbarIcon? _trayIcon;
    private Window? _mainWindow;

    public TrayIconService(MainViewModel viewModel, CoreApiClient api)
    {
        _viewModel = viewModel;
        _api = api;
    }

    /// <summary>
    /// Initialize and show the tray icon.
    /// </summary>
    public void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;

        // Create the tray icon with a default icon
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Tastile - Initializing...",
            ContextMenuMode = ContextMenuMode.PopupMenu,
            NoLeftClickDelay = true,
        };
        
        // Set icon from ICO file
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "tastile-icon.ico");
        if (System.IO.File.Exists(iconPath))
        {
            _trayIcon.Icon = new System.Drawing.Icon(iconPath);
        }
        else
        {
            // Fallback to generated icon
            _trayIcon.IconSource = new GeneratedIconSource
            {
                Text = "T",
                FontSize = 32,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.ForestGreen),
            };
        }

        // Create the context flyout
        _trayIcon.ContextFlyout = CreateContextMenu();

        // Handle left click to show window
        _trayIcon.LeftClickCommand = new RelayCommand(ShowMainWindow);

        // Subscribe to VM changes to update menu and icon
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        
        // Set initial connection status
        UpdateTrayIconStatus();
        
        // Force create the tray icon
        try
        {
            _trayIcon.ForceCreate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create tray icon: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Update tray icon and tooltip based on connection status
    /// </summary>
    private void UpdateTrayIconStatus()
    {
        if (_trayIcon == null) return;
        
        try
        {
            if (_viewModel.IsConnected)
            {
                _trayIcon.ToolTipText = $"Tastile - Connected ({_viewModel.Tiles.Count} tiles)";
            }
            else
            {
                _trayIcon.ToolTipText = "Tastile - Disconnected";
            }
            
            // Icon stays the same (tastile-icon.ico), only tooltip changes
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update tray icon: {ex.Message}");
        }
    }

    private MenuFlyout CreateContextMenu()
    {
        var menu = new MenuFlyout();

        // Show Tastile
        var showItem = new MenuFlyoutItem
        {
            Text = "Show Tastile",
        };
        showItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(showItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        // Quick Create
        var quickCreateItem = new MenuFlyoutItem
        {
            Text = "Quick Create...",
        };
        quickCreateItem.Click += (_, _) => ShowQuickCreateDialog();
        menu.Items.Add(quickCreateItem);

        // Current tile status (disabled)
        var currentTile = _viewModel.ActiveTile?.Tile?.Title ?? "No active tile";
        var statusItem = new MenuFlyoutItem
        {
            Text = $"Current: {currentTile}",
            IsEnabled = false,
        };
        menu.Items.Add(statusItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        // Complete
        var completeItem = new MenuFlyoutItem
        {
            Text = "Complete",
            IsEnabled = _viewModel.IsWorking,
        };
        completeItem.Click += async (_, _) => await _viewModel.CompleteTileCommand.ExecuteAsync(null);
        menu.Items.Add(completeItem);

        // Break (5 min)
        var breakItem = new MenuFlyoutItem
        {
            Text = "Break (5 min)",
            IsEnabled = _viewModel.IsWorking,
        };
        breakItem.Click += async (_, _) => await _viewModel.StartBreakCommand.ExecuteAsync(null);
        menu.Items.Add(breakItem);

        // End Break
        var endBreakItem = new MenuFlyoutItem
        {
            Text = "End Break",
            IsEnabled = _viewModel.IsOnBreak,
        };
        endBreakItem.Click += async (_, _) => await _viewModel.EndBreakCommand.ExecuteAsync(null);
        menu.Items.Add(endBreakItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        // Sign in with Google
        System.Diagnostics.Debug.WriteLine("Adding Google Sign In menu item...");
        var googleSignInItem = new MenuFlyoutItem
        {
            Text = "Sign in with Google",
        };
        googleSignInItem.Click += async (_, _) => await SignInWithGoogleAsync();
        menu.Items.Add(googleSignInItem);
        System.Diagnostics.Debug.WriteLine($"Menu items count: {menu.Items.Count}");

        // Settings
        var settingsItem = new MenuFlyoutItem
        {
            Text = "Settings",
        };
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        // Quit
        var quitItem = new MenuFlyoutItem
        {
            Text = "Quit",
        };
        quitItem.Click += (_, _) => QuitApplication();
        menu.Items.Add(quitItem);

        return menu;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Rebuild menu when active tile changes
        if (e.PropertyName == nameof(MainViewModel.ActiveTile) ||
            e.PropertyName == nameof(MainViewModel.IsWorking) ||
            e.PropertyName == nameof(MainViewModel.IsOnBreak))
        {
            if (_trayIcon != null)
            {
                _trayIcon.ContextFlyout = CreateContextMenu();
            }
        }
        
        // Update icon when connection status changes
        if (e.PropertyName == nameof(MainViewModel.IsConnected) ||
            e.PropertyName == nameof(MainViewModel.Tiles))
        {
            UpdateTrayIconStatus();
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;

        _mainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            _mainWindow.Show();
            _mainWindow.Activate();
        });
    }

    private void ShowSettings()
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            var settingsWindow = new Views.SettingsWindow();
            settingsWindow.Activate();
        });
    }

    private async Task SignInWithGoogleAsync()
    {
        var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tastile-tray-signin.log");
        void Log(string msg)
        {
            try
            {
                System.IO.File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}");
                System.Diagnostics.Debug.WriteLine(msg);
            }
            catch { }
        }

        try
        {
            Log("[SignInWithGoogleAsync] === START ===");

            // Start OAuth flow via daemon - get auth URL from daemon
            Log("[SignInWithGoogleAsync] Calling StartBrowserAuthAsync...");
            var authUrl = await _api.StartBrowserAuthAsync("google");
            Log($"[SignInWithGoogleAsync] Got auth URL: {authUrl}");

            if (string.IsNullOrEmpty(authUrl))
            {
                Log("[SignInWithGoogleAsync] ERROR: No auth URL returned");
                return;
            }

            // Open browser with auth URL (Desktop App opens browser, not daemon)
            try
            {
                Log($"[SignInWithGoogleAsync] Opening browser...");
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                Log("[SignInWithGoogleAsync] Browser opened successfully");
            }
            catch (Exception ex)
            {
                Log($"[SignInWithGoogleAsync] ERROR opening browser: {ex.Message}");
                return;
            }

            Log("[SignInWithGoogleAsync] Starting polling...");

            // Poll for session completion (daemon receives callback on localhost:3140)
            _ = Task.Run(async () =>
            {
                var maxAttempts = 60; // 2 minutes
                for (int i = 0; i < maxAttempts; i++)
                {
                    await Task.Delay(2000);

                    Log($"[Poll] Attempt {i + 1}/{maxAttempts}...");
                    var session = await _api.GetSessionAsync();
                    Log($"[Poll] GetSessionAsync returned: {(session != null ? $"UserId={session.UserId}, Email={session.Email}" : "null")}");

                    if (session != null)
                    {
                        Log("[Poll] Session found! Calling AuthService.InitializeAsync...");
                        try
                        {
                            await AuthService.Instance.InitializeAsync(_api);
                            Log("[Poll] AuthService.InitializeAsync completed successfully");
                        }
                        catch (Exception ex)
                        {
                            Log($"[Poll] ERROR in InitializeAsync: {ex.GetType().Name}: {ex.Message}");
                            Log($"[Poll] StackTrace: {ex.StackTrace}");
                        }
                        break;
                    }
                }
                Log("[Poll] Polling finished");
            });

            Log("[SignInWithGoogleAsync] === END ===");
        }
        catch (Exception ex)
        {
            Log($"[SignInWithGoogleAsync] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            Log($"[SignInWithGoogleAsync] StackTrace: {ex.StackTrace}");
        }
    }

    private async void ShowQuickCreateDialog()
    {
        // Show main window first (ContentDialog needs a XamlRoot)
        ShowMainWindow();

        if (_mainWindow?.Content == null) return;

        // Wait for window to be ready
        await Task.Delay(100);

        var textBox = new TextBox { PlaceholderText = "Tile title..." };
        var dialog = new ContentDialog
        {
            Title = "Quick Create",
            Content = textBox,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            XamlRoot = _mainWindow.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var title = textBox.Text?.Trim();
            if (!string.IsNullOrEmpty(title))
                await _api.CreateTileAsync(title);
        }
    }

    private void QuitApplication()
    {
        _trayIcon?.Dispose();
        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ((App)Application.Current).Shutdown();
        });
    }

    public void Dispose()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _trayIcon?.Dispose();
    }
}

/// <summary>
/// Simple relay command for tray click.
/// </summary>
public class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;

    public RelayCommand(Action execute)
    {
        _execute = execute;
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();

    public event EventHandler? CanExecuteChanged;
}
