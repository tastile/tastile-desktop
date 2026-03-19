using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;

namespace TastileDesktop.Views;

public sealed partial class TilesWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public TilesWindow()
    {
        InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 720, 760);
        _ = ViewModel.InitializeAsync();
    }

    private void OnTileClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TileListItem item && item.IsStartEnabled)
        {
            ViewModel.StartTileCommand.Execute(item.Id);
        }
    }

    private void OnTileRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
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
        if (sender is MenuFlyoutItem item && item.Tag is string tileId)
        {
            ViewModel.CompleteTileCommand.Execute(tileId);
        }
    }

    private void OnDeferTileMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string tileId)
        {
            ViewModel.DeferTileCommand.Execute(tileId);
        }
    }

    private void OnDeleteTileMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string tileId)
        {
            ViewModel.DeleteTileCommand.Execute(tileId);
        }
    }
}
