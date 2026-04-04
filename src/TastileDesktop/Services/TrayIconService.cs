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
    private readonly Action _quitCallback;
    private readonly SettingsService _settingsService;
    private TaskbarIcon? _trayIcon;
    private Window? _mainWindow;
    private MenuFlyout? _contextMenu;
    private MenuFlyoutSubItem? _windowsMenu;
    private MenuFlyoutSubItem? _actionsMenu;
    private MenuFlyoutSubItem? _accountMenu;
    private MenuFlyoutItem? _statusItem;
    private MenuFlyoutItem? _showPanelItem;
    private MenuFlyoutItem? _hidePanelItem;
    private MenuFlyoutItem? _refreshItem;
    private MenuFlyoutItem? _pinItem;
    private MenuFlyoutItem? _createTileItem;
    private MenuFlyoutItem? _executeItem;
    private MenuFlyoutItem? _tilesItem;
    private MenuFlyoutItem? _timelineItem;
    private MenuFlyoutItem? _integrationsItem;
    private MenuFlyoutItem? _settingsItem;
    private MenuFlyoutItem? _completeItem;
    private MenuFlyoutItem? _breakItem;
    private MenuFlyoutItem? _endBreakItem;
    private MenuFlyoutItem? _signInItem;

    public TrayIconService(MainViewModel viewModel, CoreApiClient api, Action quitCallback, SettingsService settingsService)
    {
        _viewModel = viewModel;
        _api = api;
        _quitCallback = quitCallback;
        _settingsService = settingsService;
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
            ContextMenuMode = ContextMenuMode.SecondWindow,
            NoLeftClickDelay = true,
        };
        
        // Resolve icon path across unpackaged, packaged, and legacy install layouts
        var iconCandidates = new[]
        {
            System.IO.Path.Combine(AppContext.BaseDirectory, "tastile-tray.ico"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "tastile-tray.ico"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "tastile.ico"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "tastile.ico"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "tastile-icon.ico"),
        };
        var iconPath = iconCandidates.FirstOrDefault(System.IO.File.Exists);
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

        // Handle left click to show window
        _trayIcon.LeftClickCommand = new RelayCommand(ShowMainWindow);

        // Subscribe to VM changes to update menu and icon
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        
        // Set initial connection status
        UpdateTrayIconStatus();
        
        // Force create the tray icon first, WITHOUT context menu
        try
        {
            _trayIcon.ForceCreate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create tray icon: {ex.Message}");
            // Clean up on failure
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _trayIcon?.Dispose();
            _trayIcon = null;
            return;
        }

        // NOW assign the context flyout AFTER ForceCreate
        // This is a workaround for H.NotifyIcon WinUI3 issues
        try
        {
            _contextMenu = CreateContextMenu();
            _trayIcon.ContextFlyout = _contextMenu;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to assign context flyout: {ex.Message}");
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
                var focusTitle = _viewModel.IsWorking
                    ? _viewModel.ActiveTileTitle ?? "Working"
                    : _viewModel.IsOnBreak
                        ? "Break in progress"
                        : _viewModel.NextUpTitle;
                _trayIcon.ToolTipText = $"Tastile - {_viewModel.QuickBarStatus}: {focusTitle}";
            }
            else
            {
                _trayIcon.ToolTipText = "Tastile - Offline";
            }
            
            // Icon stays fixed after initialization; only tooltip text changes
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update tray icon: {ex.Message}");
        }
    }

    private MenuFlyout CreateContextMenu()
    {
        var menu = new MenuFlyout();

        _showPanelItem = new MenuFlyoutItem
        {
            Text = "Show panel",
        };
        _showPanelItem.Click += (_, _) => ShowMainWindow();

        _hidePanelItem = new MenuFlyoutItem
        {
            Text = "Hide panel",
        };
        _hidePanelItem.Click += (_, _) => HidePanel();

        _refreshItem = new MenuFlyoutItem
        {
            Text = "Refresh",
        };
        _refreshItem.Click += (_, _) => RefreshPanel();

        _pinItem = new MenuFlyoutItem
        {
            Text = "Pin panel",
        };
        _pinItem.Click += (_, _) => TogglePin();

        // Current tile status (disabled)
        var currentTile = _viewModel.ActiveTileTitle ?? "No active tile";
        _statusItem = new MenuFlyoutItem
        {
            Text = $"Current: {currentTile}",
            IsEnabled = false,
        };

        _actionsMenu = new MenuFlyoutSubItem
        {
            Text = "Actions",
        };
        _completeItem = new MenuFlyoutItem
        {
            Text = "Complete",
            IsEnabled = _viewModel.IsWorking,
            Command = _viewModel.CompleteTileCommand,
        };
        _breakItem = new MenuFlyoutItem
        {
            Text = "Break (5 min)",
            IsEnabled = _viewModel.IsWorking,
            Command = _viewModel.StartBreakCommand,
        };
        _endBreakItem = new MenuFlyoutItem
        {
            Text = "End Break",
            IsEnabled = _viewModel.IsOnBreak,
            Command = _viewModel.EndBreakCommand,
        };

        _createTileItem = new MenuFlyoutItem
        {
            Text = "Create Tile",
        };
        _createTileItem.Click += (_, _) => OpenCreateTileWindow();

        _actionsMenu.Items.Add(_createTileItem);
        _actionsMenu.Items.Add(_completeItem);
        _actionsMenu.Items.Add(_breakItem);
        _actionsMenu.Items.Add(_endBreakItem);

        _windowsMenu = new MenuFlyoutSubItem
        {
            Text = "Windows",
        };
        _executeItem = new MenuFlyoutItem { Text = "Execute" };
        _executeItem.Click += (_, _) => OpenExecuteWindow();
        _tilesItem = new MenuFlyoutItem { Text = "Tiles" };
        _tilesItem.Click += (_, _) => OpenTilesWindow();
        _timelineItem = new MenuFlyoutItem { Text = "Timeline" };
        _timelineItem.Click += (_, _) => OpenTimelineWindow();
        _integrationsItem = new MenuFlyoutItem { Text = "Integrations" };
        _integrationsItem.Click += (_, _) => OpenIntegrationsWindow();
        _settingsItem = new MenuFlyoutItem { Text = "Settings" };
        _settingsItem.Click += (_, _) => ShowSettings();
        _windowsMenu.Items.Add(_executeItem);
        _windowsMenu.Items.Add(_tilesItem);
        _windowsMenu.Items.Add(_timelineItem);
        _windowsMenu.Items.Add(_integrationsItem);
        _windowsMenu.Items.Add(_settingsItem);

        _accountMenu = new MenuFlyoutSubItem
        {
            Text = "Account",
        };
        _signInItem = new MenuFlyoutItem
        {
            Text = "Sign in with Google",
        };
        _signInItem.Click += async (_, _) => await SignInWithGoogleAsync();
        _accountMenu.Items.Add(_signInItem);

        menu.Items.Add(_showPanelItem);
        menu.Items.Add(_hidePanelItem);
        menu.Items.Add(_refreshItem);
        menu.Items.Add(_pinItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(_statusItem);
        menu.Items.Add(_actionsMenu);
        menu.Items.Add(_windowsMenu);
        menu.Items.Add(_accountMenu);
        menu.Items.Add(new MenuFlyoutSeparator());

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
        // Keep one flyout instance alive and only update item states
        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainViewModel.IsWorking) ||
            e.PropertyName == nameof(MainViewModel.IsOnBreak) ||
            e.PropertyName == nameof(MainViewModel.ActiveTileTitle) ||
            e.PropertyName == nameof(MainViewModel.NextUpTitle))
        {
            RefreshContextMenuState();
        }
        
        // Update icon when connection status changes
        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainViewModel.IsConnected) ||
            e.PropertyName == nameof(MainViewModel.Tiles) ||
            e.PropertyName == nameof(MainViewModel.ActiveTileTitle) ||
            e.PropertyName == nameof(MainViewModel.NextUpTitle) ||
            e.PropertyName == nameof(MainViewModel.IsWorking) ||
            e.PropertyName == nameof(MainViewModel.IsOnBreak))
        {
            UpdateTrayIconStatus();
        }
    }

    private void RefreshContextMenuState()
    {
        try
        {
            if (_statusItem != null)
            {
                _statusItem.Text = $"Current: {_viewModel.ActiveTileTitle ?? "No active tile"}";
            }
            if (_pinItem != null)
            {
                _pinItem.Text = IsPanelPinned() ? "Unpin panel" : "Pin panel";
            }
            if (_completeItem != null)
            {
                _completeItem.IsEnabled = _viewModel.IsWorking;
            }
            if (_breakItem != null)
            {
                _breakItem.IsEnabled = _viewModel.IsWorking;
            }
            if (_endBreakItem != null)
            {
                _endBreakItem.IsEnabled = _viewModel.IsOnBreak;
            }
            if (_signInItem != null)
            {
                _signInItem.Text = AuthService.Instance.IsAuthenticated ? "Re-authenticate with Google" : "Sign in with Google";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to refresh context menu state: {ex.Message}");
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;

        _mainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            if (_mainWindow is MainWindow panelWindow)
            {
                panelWindow.ShowPanel();
                return;
            }

            _mainWindow.Show();
            _mainWindow.Activate();
        });
    }

    private void ShowSettings()
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ShowMainWindow();
            if (_mainWindow is MainWindow panelWindow)
            {
                panelWindow.OpenSettingsWindow();
            }
        });
    }

    private void OpenCreateTileWindow()
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ShowMainWindow();
            if (_mainWindow is MainWindow panelWindow)
            {
                panelWindow.OpenCreateTileWindow();
            }
        });
    }

    private void OpenExecuteWindow()
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ShowMainWindow();
            if (_mainWindow is MainWindow panelWindow)
            {
                panelWindow.OpenExecuteWindow();
            }
        });
    }

    private void OpenTilesWindow()
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ShowMainWindow();
            if (_mainWindow is MainWindow panelWindow)
            {
                panelWindow.OpenTilesWindow();
            }
        });
    }

    private void OpenTimelineWindow()
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ShowMainWindow();
            if (_mainWindow is MainWindow panelWindow)
            {
                panelWindow.OpenTimelineWindow();
            }
        });
    }

    private void OpenIntegrationsWindow()
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ShowMainWindow();
            if (_mainWindow is MainWindow panelWindow)
            {
                panelWindow.OpenIntegrationsWindow();
            }
        });
    }

    private void HidePanel()
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            if (_mainWindow is MainWindow panelWindow)
            {
                panelWindow.HidePanel();
            }
        });
    }

    private void RefreshPanel()
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(async () =>
        {
            if (_mainWindow is not MainWindow panelWindow)
            {
                return;
            }
            try
            {
                await panelWindow.RefreshDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to refresh from tray menu: {ex.Message}");
            }
        });
    }

    private void TogglePin()
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            if (_mainWindow is MainWindow panelWindow)
            {
                panelWindow.TogglePin();
                RefreshContextMenuState();
            }
        });
    }

    private bool IsPanelPinned()
    {
        return _settingsService.Current.QuickBarAlwaysOnTop;
    }

    private void MoveToNextDisplay()
    {
        if (_mainWindow is MainWindow panelWindow)
        {
            panelWindow.DispatcherQueue.TryEnqueue(() =>
            {
                FloatingWindowHelper.RotateToNextDisplay(panelWindow, _settingsService.Current);
            });
        }
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
            {
                var quota = await _api.GetTileQuotaAsync();
                if (quota?.LimitReached == true)
                {
                    _viewModel.StatusMessage = "Error: free plan limit reached (100 tiles).";
                    return;
                }

                await _api.CreateTileAsync(title);
            }
        }
    }

    private void QuitApplication()
    {
        _trayIcon?.Dispose();
        _quitCallback();
    }

    public void Dispose()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _trayIcon?.Dispose();
        _contextMenu = null;
        _windowsMenu = null;
        _actionsMenu = null;
        _accountMenu = null;
        _showPanelItem = null;
        _hidePanelItem = null;
        _refreshItem = null;
        _pinItem = null;
        _createTileItem = null;
        _executeItem = null;
        _tilesItem = null;
        _timelineItem = null;
        _integrationsItem = null;
        _settingsItem = null;
        _signInItem = null;
        _statusItem = null;
        _completeItem = null;
        _breakItem = null;
        _endBreakItem = null;
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

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
}
