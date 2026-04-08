using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using TastileDesktop.Services;

namespace TastileDesktop.Views;

public sealed partial class IntegrationsWindow : Window
{
    private readonly CoreApiClient _api = new();

    public IntegrationsWindow()
    {
        this.InitializeComponent();
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
            SyncModeTextBlock.Text = $"Sync mode: {gc?.SyncMode ?? "push_only"}";
            TargetCalendarTextBlock.Text = $"Target calendar: {gc?.SelectedCalendarId ?? "primary"}";
            SyncedTextBlock.Text = $"Last synced: {gc?.LastSyncedAt ?? "never"}";
            TargetCalendarTextBox.Text = gc?.SelectedCalendarId ?? string.Empty;
            SelectSyncMode(gc?.SyncMode ?? "push_only");

            var plan = await _api.GetCalendarSyncPlanPreviewAsync();
            SyncPlanTextBlock.Text = $"Sync plan: {plan?.SyncMode ?? "push_only"} / {plan?.ReadPolicy ?? "import_and_block_scheduling"} / {plan?.WritePolicy ?? "tastile_owned_only"}";
            ConnectButton.IsEnabled = gc?.Connected != true;
            DisconnectButton.IsEnabled = gc?.Connected == true;
            SyncNowButton.IsEnabled = gc?.Connected == true;
            SavePolicyButton.IsEnabled = gc?.Connected == true;
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
            var authWindow = new AuthWindow(_api);
            authWindow.Activate();
            var result = await authWindow.AuthResultTask;
            if (result.Success)
            {
                await AuthService.Instance.RefreshSessionFromDaemonAsync(_api);
                await RefreshAsync();
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                ErrorTextBlock.Text = result.Error;
            }
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = $"Connect failed: {ex.Message}";
            App.DebugLog($"[IntegrationsWindow] Connect failed: {ex}");
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

    private async void OnSavePolicyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ErrorTextBlock.Text = string.Empty;
            var selectedMode = (SyncModeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "push_only";
            var targetCalendar = string.IsNullOrWhiteSpace(TargetCalendarTextBox.Text)
                ? "primary"
                : TargetCalendarTextBox.Text.Trim();
            await _api.UpdateGoogleCalendarIntegrationAsync(
                syncMode: selectedMode,
                selectedCalendarId: targetCalendar);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = $"Save policy failed: {ex.Message}";
        }
    }

    private void SelectSyncMode(string mode)
    {
        for (var i = 0; i < SyncModeComboBox.Items.Count; i++)
        {
            if (SyncModeComboBox.Items[i] is ComboBoxItem { Content: string value }
                && string.Equals(value, mode, StringComparison.OrdinalIgnoreCase))
            {
                SyncModeComboBox.SelectedIndex = i;
                return;
            }
        }

        SyncModeComboBox.SelectedIndex = 0;
    }
}
