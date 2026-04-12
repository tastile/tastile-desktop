using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using TastileDesktop.Services;

namespace TastileDesktop.Views;

public sealed partial class IntegrationsWindow : Window
{
    private readonly CoreApiClient _api = new();
    private const string GoogleCalendarOAuthScopes = "https://www.googleapis.com/auth/calendar.events";
    private static readonly List<string> GrantedGoogleCalendarScopes =
    [
        "https://www.googleapis.com/auth/calendar.events"
    ];

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
            var settingsTask = _api.GetIntegrationSettingsAsync();
            var syncStatusTask = _api.GetSyncStatusAsync();
            var planTask = _api.GetCalendarSyncPlanPreviewAsync();
            await Task.WhenAll(settingsTask, syncStatusTask, planTask);

            var gc = settingsTask.Result?.GoogleCalendar ?? new GoogleCalendarIntegrationResponse();
            var presentation = GoogleCalendarIntegrationPresentationResolver.Resolve(gc, syncStatusTask.Result, planTask.Result);

            StatusTextBlock.Text = presentation.StatusBadge;
            ConnectionHeadlineTextBlock.Text = presentation.Headline;
            ConnectionDetailTextBlock.Text = presentation.Detail;
            ReadWriteTextBlock.Text = presentation.PermissionsSummary;
            TargetCalendarTextBlock.Text = presentation.CalendarSummary;
            LastSyncTextBlock.Text = presentation.LastSyncSummary;
            SyncHealthTextBlock.Text = presentation.SyncHealthSummary;
            SyncPlanTextBlock.Text = presentation.PlanSummary;
            SyncModeDescriptionTextBlock.Text = presentation.SyncModeDescription;
            ConnectButton.Content = presentation.PrimaryActionText;

            TargetCalendarTextBox.Text = gc.SelectedCalendarId ?? string.Empty;
            SelectSyncMode(gc.SyncMode);

            var isConnected = gc.Connected;
            ConnectButton.IsEnabled = !isConnected;
            ConnectButton.Visibility = isConnected ? Visibility.Collapsed : Visibility.Visible;
            DisconnectButton.IsEnabled = isConnected;
            DisconnectButton.Visibility = isConnected ? Visibility.Visible : Visibility.Collapsed;
            SyncNowButton.IsEnabled = isConnected;
            SavePolicyButton.IsEnabled = isConnected;
            SyncModeComboBox.IsEnabled = isConnected;
            TargetCalendarTextBox.IsEnabled = isConnected;
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
            var authWindow = new AuthWindow(
                _api,
                GoogleCalendarOAuthScopes,
                new Dictionary<string, string>
                {
                    ["access_type"] = "offline",
                    ["prompt"] = "consent",
                });
            authWindow.Activate();
            var result = await authWindow.AuthResultTask;
            if (result.Success)
            {
                await AuthService.Instance.RefreshSessionFromDaemonAsync(_api);
                var session = AuthService.Instance.CurrentSession;
                if (string.IsNullOrWhiteSpace(session?.ProviderToken))
                {
                    ErrorTextBlock.Text = "Google Calendar access token was not returned. Please reconnect and approve calendar access.";
                    return;
                }

                await _api.UpdateGoogleCalendarIntegrationAsync(
                    connected: true,
                    canRead: true,
                    canWrite: true,
                    accountEmail: session.Email,
                    selectedCalendarId: "primary",
                    grantedScopes: GrantedGoogleCalendarScopes);

                await _api.TriggerSyncAsync();
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
            var selectedMode = (SyncModeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "push_only";
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
            if (SyncModeComboBox.Items[i] is ComboBoxItem comboBoxItem
                && comboBoxItem.Tag is string value
                && string.Equals(value, mode, StringComparison.OrdinalIgnoreCase))
            {
                SyncModeComboBox.SelectedIndex = i;
                SyncModeDescriptionTextBlock.Text = GoogleCalendarIntegrationPresentationResolver.ResolveSyncModeDescription(value);
                return;
            }
        }

        SyncModeComboBox.SelectedIndex = 0;
        SyncModeDescriptionTextBlock.Text = GoogleCalendarIntegrationPresentationResolver.ResolveSyncModeDescription("push_only");
    }
}
