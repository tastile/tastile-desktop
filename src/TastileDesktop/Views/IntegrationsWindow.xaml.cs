using Microsoft.UI.Xaml;
using TastileDesktop.Services;

namespace TastileDesktop.Views;

public sealed partial class IntegrationsWindow : Window
{
    private readonly CoreApiClient _api = new();

    public IntegrationsWindow()
    {
        InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 560, 480);
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            ErrorTextBlock.Text = string.Empty;
            var settings = await _api.GetIntegrationSettingsAsync();
            var gc = settings?.GoogleCalendar;
            StatusTextBlock.Text = $"Status: {(gc?.Connected == true ? "connected" : "disconnected")}";
            ReadWriteTextBlock.Text = $"Read/Write: {(gc?.CanRead == true ? "on" : "off")} / {(gc?.CanWrite == true ? "on" : "off")}";
            AccountTextBlock.Text = $"Account: {gc?.AccountEmail ?? "not linked"}";
            SyncedTextBlock.Text = $"Last synced: {gc?.LastSyncedAt ?? "never"}";
            ConnectButton.IsEnabled = gc?.Connected != true;
            DisconnectButton.IsEnabled = gc?.Connected == true;
            SyncNowButton.IsEnabled = gc?.Connected == true;
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = $"Failed to load integrations: {ex.Message}";
        }
    }

    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _api.UpdateGoogleCalendarIntegrationAsync(connected: true);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = $"Connect failed: {ex.Message}";
        }
    }

    private async void OnDisconnectClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _api.UpdateGoogleCalendarIntegrationAsync(connected: false, accountEmail: null);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = $"Disconnect failed: {ex.Message}";
        }
    }

    private async void OnSyncNowClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _api.TriggerSyncAsync();
            await _api.UpdateGoogleCalendarIntegrationAsync(lastSyncedAt: DateTime.UtcNow.ToString("O"));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = $"Sync failed: {ex.Message}";
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }
}
