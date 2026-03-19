using Microsoft.UI.Xaml;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;
using TastileDesktop.Views;

namespace TastileDesktop;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 560, 460);
        _ = ViewModel.InitializeAsync();
        AuthService.Instance.AuthStateChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateAccountUI);
        UpdateAccountUI();
    }

    private void UpdateAccountUI()
    {
        AccountTextBlock.Text = AuthService.Instance.IsAuthenticated
            ? AuthService.Instance.UserEmail ?? "Account"
            : "Sign In";
    }

    private async void OnAccountClick(object sender, RoutedEventArgs e)
    {
        if (!AuthService.Instance.IsAuthenticated)
        {
            var authWindow = new AuthWindow(ViewModel.ApiClient);
            authWindow.Activate();
            var result = await authWindow.AuthResultTask;
            if (result.Success)
            {
                await AuthService.Instance.RefreshSessionFromDaemonAsync(ViewModel.ApiClient);
                await ViewModel.RefreshAsync();
                ViewModel.StatusMessage = "Signed in";
                UpdateAccountUI();
            }
            else if (!string.IsNullOrWhiteSpace(result.Error) &&
                     !string.Equals(result.Error, "Authentication window closed", StringComparison.Ordinal))
            {
                ViewModel.StatusMessage = result.Error;
            }
            return;
        }

        await AuthService.Instance.SignOutAsync(ViewModel.ApiClient);
        ViewModel.StatusMessage = "Signed out";
        UpdateAccountUI();
    }

    private void OnOpenExecuteWindowClick(object sender, RoutedEventArgs e)
    {
        var window = new ExecuteWindow();
        window.Activate();
    }

    private void OnOpenTilesWindowClick(object sender, RoutedEventArgs e)
    {
        var window = new TilesWindow();
        window.Activate();
    }

    private void OnOpenTimelineWindowClick(object sender, RoutedEventArgs e)
    {
        var window = new TimelineWindow();
        window.Activate();
    }

    private void OnOpenCreateTileWindowClick(object sender, RoutedEventArgs e)
    {
        var window = new CreateTileWindow();
        window.Activate();
    }

    private void OnOpenSettingsWindowClick(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow();
        window.Activate();
    }
}
