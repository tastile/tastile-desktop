using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Views;

/// <summary>
/// An unavoidable intervention dialog that forces user action.
/// </summary>
public sealed partial class InterventionWindow : Window
{
    private readonly InterventionType _type;
    private readonly string? _tileTitle;
    private readonly string? _phaseStartedAt;
    private readonly List<TileView> _readyTiles;
    private bool _actionTaken = false;
    private string? _selectedTileId;

    public event EventHandler<InterventionAction>? ActionTaken;
    public string? SelectedTileId => _selectedTileId;

    // Win32 APIs for forcing topmost
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_SHOWWINDOW = 0x0040;

    public InterventionWindow(InterventionType type, string? tileTitle, string? phaseStartedAt, TilesResponse? tilesResponse = null)
    {
        this.InitializeComponent();
        FloatingWindowHelper.ApplyWindowTheme(this);
        _type = type;
        _tileTitle = tileTitle;
        _phaseStartedAt = phaseStartedAt;
        _readyTiles = tilesResponse?.Tiles?.Where(t => t.Lifecycle == "Ready").ToList() ?? new List<TileView>();

        SetupWindow();
        ConfigureContent();
    }

    private void SetupWindow()
    {
        // Get the AppWindow
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // Make it full screen overlay
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
        if (displayArea != null)
        {
            appWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                displayArea.WorkArea.X,
                displayArea.WorkArea.Y,
                displayArea.WorkArea.Width,
                displayArea.WorkArea.Height));
        }

        // Configure presenter to be always on top, not resizable, not minimizable
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }

        // Force topmost via Win32
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        SetForegroundWindow(hwnd);

        // Disable close button (handled in WindowProc override conceptually)
        // In WinUI 3, we can't easily disable the close button, but we can handle Closed event
        this.Closed += OnWindowClosed;
    }

    private void ConfigureContent()
    {
        switch (_type)
        {
            case InterventionType.Work:
                ConfigureWorkIntervention();
                break;
            case InterventionType.BreakOver:
                ConfigureBreakIntervention();
                break;
            case InterventionType.Idle:
                ConfigureIdleIntervention();
                break;
        }
    }

    private void ConfigureWorkIntervention()
    {
        WorkButtons.Visibility = Visibility.Visible;
        BreakButtons.Visibility = Visibility.Collapsed;
        IdleButtons.Visibility = Visibility.Collapsed;

        if (!string.IsNullOrEmpty(_tileTitle))
        {
            TileTitleText.Text = _tileTitle;
        }

        if (!string.IsNullOrEmpty(_phaseStartedAt) && 
            DateTimeOffset.TryParse(_phaseStartedAt, out var startedAt))
        {
            var elapsed = DateTimeOffset.UtcNow - startedAt;
            ElapsedTimeText.Text = $"Working for {(int)elapsed.TotalMinutes} minutes";
        }
    }

    private void ConfigureBreakIntervention()
    {
        WorkButtons.Visibility = Visibility.Collapsed;
        BreakButtons.Visibility = Visibility.Visible;
        IdleButtons.Visibility = Visibility.Collapsed;
        TileInfoPanel.Visibility = Visibility.Collapsed;

        MessageText.Text = "Your break time is over!";
    }

    private void ConfigureIdleIntervention()
    {
        WorkButtons.Visibility = Visibility.Collapsed;
        BreakButtons.Visibility = Visibility.Collapsed;
        IdleButtons.Visibility = Visibility.Visible;
        TileInfoPanel.Visibility = Visibility.Collapsed;

        MessageText.Text = "What would you like to work on?";

        // Load ready tiles from provided tiles response
        if (_readyTiles.Count > 0)
        {
            ReadyTilesList.ItemsSource = _readyTiles;
            ReadyTilesList.IsItemClickEnabled = true;
            NoReadyTilesText.Visibility = Visibility.Collapsed;
            ReadyTilesHintText.Visibility = Visibility.Visible;
        }
        else
        {
            ReadyTilesList.ItemsSource = null;
            ReadyTilesList.IsItemClickEnabled = false;
            NoReadyTilesText.Visibility = Visibility.Visible;
            ReadyTilesHintText.Visibility = Visibility.Collapsed;
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        // Block close unless an action button was clicked
        if (!_actionTaken)
        {
            args.Handled = true;
        }
    }

    private void OnContinue(object sender, RoutedEventArgs e)
    {
        _actionTaken = true;
        ActionTaken?.Invoke(this, InterventionAction.Continue);
        this.Close();
    }

    private void OnTakeBreak(object sender, RoutedEventArgs e)
    {
        _actionTaken = true;
        ActionTaken?.Invoke(this, InterventionAction.TakeBreak);
        this.Close();
    }

    private void OnComplete(object sender, RoutedEventArgs e)
    {
        _actionTaken = true;
        ActionTaken?.Invoke(this, InterventionAction.Complete);
        this.Close();
    }

    private void OnEndBreak(object sender, RoutedEventArgs e)
    {
        _actionTaken = true;
        ActionTaken?.Invoke(this, InterventionAction.EndBreak);
        this.Close();
    }

    private void OnReadyTileClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TileView tile)
        {
            _selectedTileId = tile.Id;
            _actionTaken = true;
            ActionTaken?.Invoke(this, InterventionAction.StartTile);
            this.Close();
        }
    }

    private void OnCreateNew(object sender, RoutedEventArgs e)
    {
        _actionTaken = true;
        // Open main window for creating a new tile
        this.Close();
    }
}
