using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TastileDesktop.ViewModels;

namespace TastileDesktop;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        this.InitializeComponent();

        // Set window size via AppWindow API
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(520, 780));

        // Initialize ViewModel
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await ViewModel.InitializeAsync();
    }

    private void OnTileClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not TileListItem item) return;

        // Only start tiles that are Ready
        if (!string.Equals(item.Lifecycle, "Ready", StringComparison.OrdinalIgnoreCase)) return;

        ViewModel.StartTileCommand.Execute(item.Id);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new Views.SettingsWindow();
        settingsWindow.Activate();
    }

    private void OnTileRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        // Show context flyout
        if (sender is FrameworkElement element && element.ContextFlyout is MenuFlyout flyout)
        {
            flyout.ShowAt(element, e.GetPosition(element));
        }
    }

    private void OnStartTileMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string tileId)
        {
            ViewModel.StartTileCommand.Execute(tileId);
        }
    }

    private void OnCompleteTileMenuClick(object sender, RoutedEventArgs e)
    {
        // Complete the currently active tile
        ViewModel.CompleteTileCommand.Execute(null);
    }

    private void OnDeferTileMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string tileId)
        {
            _ = ViewModel.DeferTileCommand.ExecuteAsync(tileId);
        }
    }

    private void OnDeleteTileMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string tileId)
        {
            _ = ViewModel.DeleteTileCommand.ExecuteAsync(tileId);
        }
    }
}
