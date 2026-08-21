using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using TastileDesktop.Resources;
using TastileDesktop.Services;

namespace TastileDesktop.Views;

public sealed partial class IntegrationsWindow : Window
{
    private readonly CoreApiClient _api = new(
        getAccessToken: Services.AuthService.Instance.GetAccessTokenAsync,
        refreshTokens: Services.CognitoAuthService.Instance.RefreshAsync);
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
            var planTask = _api.GetCalendarSyncPlanPreviewAsync();
            await Task.WhenAll(settingsTask, planTask);

            var gc = settingsTask.Result?.GoogleCalendar ?? new GoogleCalendarIntegrationResponse();
            var presentation = GoogleCalendarIntegrationPresentationResolver.Resolve(gc, planTask.Result);

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
            ErrorTextBlock.Text = string.Format(Strings.Get("Integrations_LoadError"), ex.Message);
        }
    }

    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        // Google Calendar integration is not yet implemented for the
        // Cognito-only auth model. The previous daemon-mediated Google
        // OAuth flow relied on a ProviderToken in the AuthSession, which
        // Cognito's Hosted UI does not issue.
        ErrorTextBlock.Text = Strings.Get("Integrations_NotAvailable");
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
            ErrorTextBlock.Text = string.Format(Strings.Get("Integrations_DisconnectError"), ex.Message);
        }
    }

    private async void OnSyncNowClick(object sender, RoutedEventArgs e)
    {
        // Manual sync was a local-daemon action and is not part of the remote
        // AWS architecture; the integration server handles scheduling itself.
        ErrorTextBlock.Text = Strings.Get("Integrations_SyncNotAvailable");
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
            ErrorTextBlock.Text = string.Format(Strings.Get("Integrations_SavePolicyError"), ex.Message);
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
