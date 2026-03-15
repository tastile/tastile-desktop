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
            ToolTipText = "Tastile",
            ContextMenuMode = ContextMenuMode.PopupMenu,
            NoLeftClickDelay = true,
        };

        // Create the context flyout
        _trayIcon.ContextFlyout = CreateContextMenu();

        // Handle left click to show window
        _trayIcon.LeftClickCommand = new RelayCommand(ShowMainWindow);

        // Set icon source (try ICO file first, fallback to generated icon)
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "tastile-tray.ico");
            if (File.Exists(iconPath))
            {
                _trayIcon.IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath));
            }
            else
            {
                _trayIcon.IconSource = new GeneratedIconSource
                {
                    Text = "T",
                    FontSize = 32,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
                };
            }
        }
        catch
        {
            // If icon loading fails, the tray icon will use a default icon
        }

        // Subscribe to VM changes to update menu
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
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
