using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using TastileDesktop.Models;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;
using System.IO;
using System.Threading;
using Windows.Storage.Pickers;

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
    private readonly DispatcherTimer _syncStatusTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly SemaphoreSlim _syncRefreshGate = new(1, 1);

    public SettingsWindow()
    {
        this.InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 520, 700);
        ViewModel.UpdateSystemAppearance(_appearanceService.GetCurrentSnapshot());
        _appearanceService.AppearanceChanged += OnAppearanceChanged;
        AuthService.Instance.AuthStateChanged += OnAuthStateChanged;
        RefreshAuthStatus();
        PopulateDesktopRuntimePaths();
        _syncStatusTimer.Tick += OnSyncStatusTimerTick;
        _syncStatusTimer.Start();
        _ = RefreshSyncStatusAsync();
        Closed += OnClosed;

        var currentVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        CurrentVersionTextBlock.Text = $"Current version: {currentVersion}";
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

    private void OnNextDisplayClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app && app.MainWindowInstance is MainWindow mainWindow)
        {
            var settings = new SettingsService();
            FloatingWindowHelper.RotateToNextDisplay(mainWindow, settings.Current);
        }
    }

    private void OnTopClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app && app.MainWindowInstance is MainWindow mainWindow)
        {
            var settings = new SettingsService();
            var newSettings = settings.Current with { QuickPanelVerticalPosition = QuickPanelVerticalPositions.Top };
            settings.Save(newSettings);
            FloatingWindowHelper.ForcePositionUpdate(mainWindow, newSettings);
        }
    }

    private void OnBottomClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app && app.MainWindowInstance is MainWindow mainWindow)
        {
            var settings = new SettingsService();
            var newSettings = settings.Current with { QuickPanelVerticalPosition = QuickPanelVerticalPositions.Bottom };
            settings.Save(newSettings);
            FloatingWindowHelper.ForcePositionUpdate(mainWindow, newSettings);
        }
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
            null,
            false
        );

        PromptToastDisplayService.Instance.ShowPrompt(
            testPrompt,
            Math.Clamp(ViewModel.PromptToastMaxVisible, 1, 5),
            async (actionId, _) =>
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

    private async void OnBrowsePromptToastSoundFileClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.MusicLibrary,
            ViewMode = PickerViewMode.List,
        };

        picker.FileTypeFilter.Add(".mp3");
        picker.FileTypeFilter.Add(".wav");
        picker.FileTypeFilter.Add(".m4a");
        picker.FileTypeFilter.Add(".aac");
        picker.FileTypeFilter.Add(".wma");
        picker.FileTypeFilter.Add(".flac");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            ViewModel.PromptToastSoundFilePath = file.Path;
        }
    }

    private async void OnTestPromptToastSoundClick(object sender, RoutedEventArgs e)
    {
        var previewSettings = new TastileSettings
        {
            PromptToastSoundEnabled = ViewModel.PromptToastSoundEnabled,
            PromptToastSoundSource = ViewModel.PromptToastSoundSource,
            PromptToastSoundFilePath = ViewModel.PromptToastSoundFilePath,
            PromptToastSoundPlaybackMode = PromptToastSoundPlaybackModes.FixedCount,
            PromptToastSoundDurationSeconds = ViewModel.PromptToastSoundDurationSeconds,
            PromptToastSoundRepeatCount = ViewModel.PromptToastSoundRepeatCount,
            PromptToastSoundRepeatIntervalSeconds = ViewModel.PromptToastSoundRepeatIntervalSeconds,
        };

        await PromptToastSoundService.Instance.PlayAsync(previewSettings);
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
        try
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
        catch (Exception ex)
        {
            AuthStatusTextBlock.Text = $"Sign-in failed: {ex.Message}";
            App.DebugLog($"[SettingsWindow] Sign-in failed: {ex}");
        }
    }

    private async void OnSignOutClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await AuthService.Instance.SignOutAsync(_api);
            RefreshAuthStatus();
        }
        catch (Exception ex)
        {
            AuthStatusTextBlock.Text = $"Sign-out failed: {ex.Message}";
            App.DebugLog($"[SettingsWindow] Sign-out failed: {ex}");
        }
    }

    private async void OnCheckUpdateClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var currentVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            var result = await _updateService.CheckForUpdateAsync(string.Empty, currentVersion);
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

    private async void OnResetLocalSyncDataClick(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmRecoveryActionAsync(
                "Clear local data",
                "This clears the local event log and local tiles on this device only. Cloud data is not deleted."))
        {
            return;
        }

        try
        {
            var result = await _api.ResetLocalSyncDataAsync();
            SyncLastResultTextBlock.Text = $"Result: {result?.Message ?? "Local sync data cleared."}";
            SyncLastErrorTextBlock.Text = "Error: -";
            await RefreshSyncStatusAsync();
        }
        catch (Exception ex)
        {
            SyncLastErrorTextBlock.Text = $"Error: {ex.Message}";
            App.DebugLog($"[SyncRecovery] Reset local failed: {ex}");
        }
    }

    private async void OnRedownloadRemoteSyncDataClick(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmRecoveryActionAsync(
                "Re-download cloud data",
                "This clears the local data on this device, then downloads the current cloud event log again."))
        {
            return;
        }

        try
        {
            var result = await _api.RedownloadRemoteSyncDataAsync();
            SyncLastResultTextBlock.Text = $"Result: {result?.Message ?? "Cloud data re-downloaded."}";
            SyncLastErrorTextBlock.Text = "Error: -";
            await RefreshSyncStatusAsync();
        }
        catch (Exception ex)
        {
            SyncLastErrorTextBlock.Text = $"Error: {ex.Message}";
            App.DebugLog($"[SyncRecovery] Redownload failed: {ex}");
        }
    }

    private async void OnSyncStatusTimerTick(object? sender, object e)
    {
        await RefreshSyncStatusAsync();
    }

    private async Task RefreshSyncStatusAsync()
    {
        if (!await _syncRefreshGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var statusTask = _api.GetSyncStatusAsync();
            var executionTask = _api.GetExecutionAsync();
            var runtimePathsTask = _api.GetRuntimePathsAsync();
            await Task.WhenAll(statusTask, executionTask, runtimePathsTask);

            TileQuotaResponse? quota = null;
            try
            {
                quota = await _api.GetTileQuotaAsync();
            }
            catch (Exception ex)
            {
                App.DebugLog($"[SyncStatus] Tile quota refresh skipped: {ex.Message}");
            }

            var status = statusTask.Result;
            var execution = executionTask.Result;
            var runtimePaths = runtimePathsTask.Result;
            if (status == null)
            {
                SyncStateTextBlock.Text = "Unknown";
                ApplyDaemonRuntimePaths(runtimePaths);
                return;
            }

            var hasFailedOps = status.LastResult?.Failed > 0;
            SyncStateTextBlock.Text = status.InProgress
                ? "Syncing"
                : (hasFailedOps || !string.IsNullOrWhiteSpace(status.LastError) ? "Error" : "Idle");
            SyncLastAttemptTextBlock.Text = FormatTimestamp(status.LastAttemptAt);
            SyncLastSuccessTextBlock.Text = FormatTimestamp(status.LastSuccessAt);
            SyncLocalTilesTextBlock.Text = execution?.TileCount.ToString() ?? "-";
            SyncLocalEventsTextBlock.Text = execution?.EventCount.ToString() ?? "-";
            SyncRemoteTilesTextBlock.Text = quota == null
                ? "-"
                : $"{quota.TileCount} / {quota.MaxTiles}";
            SyncRemoteSourceTextBlock.Text = quota?.Source switch
            {
                "remote" => "Supabase",
                "local_fallback" => "Local fallback",
                _ => "-"
            };

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

            ApplyDaemonRuntimePaths(runtimePaths);
        }
        catch (Exception ex)
        {
            SyncStateTextBlock.Text = "Unavailable";
            SyncLastErrorTextBlock.Text = $"Error: {ex.Message}";
            App.DebugLog($"[SyncStatus] Failed to refresh: {ex}");
        }
        finally
        {
            _syncRefreshGate.Release();
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

    private async Task<bool> ConfirmRecoveryActionAsync(string title, string message)
    {
        if (Content is not FrameworkElement root)
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = root.XamlRoot,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void PopulateDesktopRuntimePaths()
    {
        RuntimeProfileTextBlock.Text = RuntimeProfile.Name;
        RuntimeAppDataDirTextBlock.Text = RuntimeProfile.GetAppDataDirectory();
        RuntimeDbPathTextBlock.Text = Path.Combine(RuntimeProfile.GetAppDataDirectory(), "tastile.db");
        RuntimeSessionPathTextBlock.Text = Path.Combine(RuntimeProfile.GetAppDataDirectory(), "session.json");
        RuntimeDesktopApiLogPathTextBlock.Text = CoreApiClient.DebugLogPath;
        RuntimeDesktopDaemonLogPathTextBlock.Text = DaemonLog.LogPath;
        RuntimeCreateTileLogPathTextBlock.Text = CreateTileWindow.DebugLogPath;
        RuntimeDaemonStartupLogPathTextBlock.Text = "-";
        RuntimeDaemonExecutablePathTextBlock.Text = "-";
    }

    private void ApplyDaemonRuntimePaths(RuntimePathsResponse? runtimePaths)
    {
        if (runtimePaths == null)
        {
            return;
        }

        RuntimeProfileTextBlock.Text = runtimePaths.ProfileName;
        RuntimeAppDataDirTextBlock.Text = runtimePaths.AppDataDir;
        RuntimeDbPathTextBlock.Text = runtimePaths.DbPath;
        RuntimeSessionPathTextBlock.Text = runtimePaths.SessionPath;
        RuntimeDaemonStartupLogPathTextBlock.Text = runtimePaths.DaemonStartupLogPath;
        RuntimeDaemonExecutablePathTextBlock.Text = runtimePaths.DaemonExecutablePath;
    }

    private void ShowUpdateToast(AppUpdateInfo update)
    {
        var prompt = new PromptView(
            PromptId: $"update-{Guid.NewGuid():N}",
            Kind: "app_update",
            Severity: "info",
            TileId: null,
            Title: $"Update available: {update.LatestVersion}",
            Body: string.IsNullOrWhiteSpace(update.Notes) ? "Download the installer and install the latest version." : update.Notes,
            Why: "An application update is available.",
            SuggestedMinutes: null,
            Actions: new List<PromptActionView>
            {
                new("install_update", "Install Update"),
                new("ignore_update", "Ignore"),
            },
            CreatedAt: null,
            ExpiresAt: null,
            Stale: false);

        PromptToastDisplayService.Instance.ShowPrompt(
            prompt,
            maxActions: 2,
            async (actionId, _) =>
            {
                PromptToastDisplayService.Instance.Hide();
                if (string.Equals(actionId, "install_update", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var installerPath = await _updateService.DownloadInstallerAsync(update.DownloadUrl);
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = installerPath,
                            UseShellExecute = true,
                        });
                        ((App)Application.Current).Shutdown();
                    }
                    catch (Exception ex)
                    {
                        UpdateStatusTextBlock.Text = $"Update install failed: {ex.Message}";
                    }
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
