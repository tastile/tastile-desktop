using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using TastileDesktop.Models;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;

namespace TastileDesktop.Views;

/// <summary>
/// Settings window for Tastile.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; } = new();
    private readonly SystemAppearanceService _appearanceService = SystemAppearanceService.Instance;
    private readonly CoreApiClient _api = new();
    private readonly AppUpdateService _updateService = new();
    private readonly DispatcherTimer _syncStatusTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    public SettingsWindow()
    {
        this.InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 520, 700);
        ViewModel.UpdateSystemAppearance(_appearanceService.GetCurrentSnapshot());
        _appearanceService.AppearanceChanged += OnAppearanceChanged;
        AuthService.Instance.AuthStateChanged += OnAuthStateChanged;
        RefreshAuthStatus();
        _syncStatusTimer.Tick += OnSyncStatusTimerTick;
        _syncStatusTimer.Start();
        _ = RefreshSyncStatusAsync();
        Closed += OnClosed;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // Close the window after saving
        this.Close();
    }

    private async void OnTestPromptOverlayClick(object sender, RoutedEventArgs e)
    {
        var seconds = Math.Clamp(ViewModel.PromptOverlayDurationSeconds, 1, 15);
        await PromptAttentionOverlayService.Current.ShowTestOverlayAsync(TimeSpan.FromSeconds(seconds));
    }

    private void OnTestPromptToastClick(object sender, RoutedEventArgs e)
    {
        var testPrompt = new Models.PromptView(
            Guid.NewGuid().ToString(),
            "test",
            null,
            null,
            "Test Prompt",
            "This is a test toast notification",
            "",
            null,
            new List<Models.PromptActionView>
            {
                new("start", "開始"),
                new("defer", "先送り"),
                new("complete", "完了"),
            },
            null,
            false
        );

        PromptToastDisplayService.Instance.ShowPrompt(
            testPrompt,
            Math.Clamp(ViewModel.PromptToastMaxVisible, 1, 5),
            async actionId =>
            {
                System.Diagnostics.Debug.WriteLine($"[Test Toast] Action clicked: {actionId}");
                App.DebugLog($"[Test Toast] Action clicked: {actionId}");
                PromptToastDisplayService.Instance.Hide();
            },
            async (actionId, minutes) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Test Toast] Defer clicked: {actionId}, minutes: {minutes}");
                App.DebugLog($"[Test Toast] Defer clicked: {actionId}, minutes: {minutes}");
                PromptToastDisplayService.Instance.Hide();
            });
    }

    private void OnAppearanceChanged(object? sender, SystemAppearanceSnapshot snapshot)
    {
        DispatcherQueue.TryEnqueue(() => ViewModel.UpdateSystemAppearance(snapshot));
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _appearanceService.AppearanceChanged -= OnAppearanceChanged;
        AuthService.Instance.AuthStateChanged -= OnAuthStateChanged;
        _syncStatusTimer.Tick -= OnSyncStatusTimerTick;
        _syncStatusTimer.Stop();
        Closed -= OnClosed;
    }

    private void OnAuthStateChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshAuthStatus);
    }

    private void RefreshAuthStatus()
    {
        var email = AuthService.Instance.UserEmail;
        AuthStatusTextBlock.Text = string.IsNullOrWhiteSpace(email) ? "Not signed in" : $"Signed in as {email}";
    }

    private async void OnSignInGoogleClick(object sender, RoutedEventArgs e)
    {
        var authWindow = new AuthWindow(_api);
        authWindow.Activate();
        var result = await authWindow.AuthResultTask;
        if (result.Success)
        {
            await AuthService.Instance.RefreshSessionFromDaemonAsync(_api);
            RefreshAuthStatus();
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            AuthStatusTextBlock.Text = result.Error;
        }
    }

    private async void OnSignOutClick(object sender, RoutedEventArgs e)
    {
        await AuthService.Instance.SignOutAsync(_api);
        RefreshAuthStatus();
    }

    private async void OnCheckUpdateClick(object sender, RoutedEventArgs e)
    {
        var manifestUrl = ViewModel.UpdateManifestUrl?.Trim();
        if (string.IsNullOrWhiteSpace(manifestUrl))
        {
            UpdateStatusTextBlock.Text = "Manifest URL is required.";
            return;
        }

        try
        {
            var currentVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            var result = await _updateService.CheckForUpdateAsync(manifestUrl, currentVersion);
            if (!result.HasUpdate)
            {
                UpdateStatusTextBlock.Text = "You are up to date.";
                return;
            }

            UpdateStatusTextBlock.Text = $"Update {result.LatestVersion} is available.";
            ShowUpdateToast(result);
        }
        catch (Exception ex)
        {
            UpdateStatusTextBlock.Text = $"Update check failed: {ex.Message}";
        }
    }

    private async void OnSyncNowClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _api.TriggerSyncAsync();
            await RefreshSyncStatusAsync();
        }
        catch (Exception ex)
        {
            SyncStateTextBlock.Text = "Sync failed";
            SyncLastErrorTextBlock.Text = $"Error: {ex.Message}";
            App.DebugLog($"[SyncStatus] Manual sync failed: {ex}");
        }
    }

    private async void OnRefreshSyncStatusClick(object sender, RoutedEventArgs e)
    {
        await RefreshSyncStatusAsync();
    }

    private async void OnSyncStatusTimerTick(object? sender, object e)
    {
        await RefreshSyncStatusAsync();
    }

    private async Task RefreshSyncStatusAsync()
    {
        try
        {
            var status = await _api.GetSyncStatusAsync();
            if (status == null)
            {
                SyncStateTextBlock.Text = "Unknown";
                return;
            }

            var hasFailedOps = status.LastResult?.Failed > 0;
            SyncStateTextBlock.Text = status.InProgress
                ? "Syncing"
                : (hasFailedOps || !string.IsNullOrWhiteSpace(status.LastError) ? "Error" : "Idle");
            SyncLastAttemptTextBlock.Text = FormatTimestamp(status.LastAttemptAt);
            SyncLastSuccessTextBlock.Text = FormatTimestamp(status.LastSuccessAt);

            if (status.LastResult != null)
            {
                SyncLastResultTextBlock.Text = $"Result: uploaded={status.LastResult.Uploaded}, downloaded={status.LastResult.Downloaded}, applied={status.LastResult.Applied}, failed={status.LastResult.Failed}";
            }
            else
            {
                SyncLastResultTextBlock.Text = "Result: -";
            }

            if (!string.IsNullOrWhiteSpace(status.LastError))
            {
                SyncLastErrorTextBlock.Text = $"Error: {status.LastError}";
            }
            else if (hasFailedOps)
            {
                SyncLastErrorTextBlock.Text = $"Error: sync reported failed={status.LastResult!.Failed}. Check daemon logs for details.";
            }
            else
            {
                SyncLastErrorTextBlock.Text = "Error: -";
            }
        }
        catch (Exception ex)
        {
            SyncStateTextBlock.Text = "Unavailable";
            SyncLastErrorTextBlock.Text = $"Error: {ex.Message}";
            App.DebugLog($"[SyncStatus] Failed to refresh: {ex}");
        }
    }

    private static string FormatTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        return value;
    }

    private void ShowUpdateToast(AppUpdateInfo update)
    {
        var prompt = new PromptView(
            PromptId: $"update-{Guid.NewGuid():N}",
            Kind: "app_update",
            Severity: "info",
            TileId: null,
            Title: $"Update available: {update.LatestVersion}",
            Body: string.IsNullOrWhiteSpace(update.Notes) ? "Restart to install the latest version." : update.Notes,
            Why: "An application update is available.",
            SuggestedMinutes: null,
            Actions: new List<PromptActionView>
            {
                new("install_update", "Restart & Install"),
                new("ignore_update", "Ignore"),
            },
            ExpiresAt: null,
            Stale: false);

        PromptToastDisplayService.Instance.ShowPrompt(
            prompt,
            maxActions: 2,
            async actionId =>
            {
                PromptToastDisplayService.Instance.Hide();
                if (string.Equals(actionId, "install_update", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(update.DownloadUrl))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = update.DownloadUrl,
                            UseShellExecute = true,
                        });
                    }

                    ((App)Application.Current).Shutdown();
                }
                else if (string.Equals(actionId, "ignore_update", StringComparison.OrdinalIgnoreCase))
                {
                    var settingsService = new SettingsService();
                    settingsService.Update(settings => settings.IgnoredUpdateVersion = update.LatestVersion);
                    UpdateStatusTextBlock.Text = $"Ignored update {update.LatestVersion}.";
                }
                await Task.CompletedTask;
            });
    }
}
