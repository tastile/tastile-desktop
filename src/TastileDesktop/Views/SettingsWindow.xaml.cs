using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using TastileDesktop.Models;
using TastileDesktop.Resources;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;
using System.IO;
using Windows.Storage.Pickers;

namespace TastileDesktop.Views;

/// <summary>
/// Settings window for Tastile.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; } = new();
    private readonly SystemAppearanceService _appearanceService = SystemAppearanceService.Instance;
    private readonly AppUpdateService _updateService = new();

    public SettingsWindow()
    {
        this.InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 520, 700);
        ViewModel.UpdateSystemAppearance(_appearanceService.GetCurrentSnapshot());
        _appearanceService.AppearanceChanged += OnAppearanceChanged;
        AuthService.Instance.AuthStateChanged += OnAuthStateChanged;
        RefreshAuthStatus();
        PopulateDesktopRuntimePaths();
        Closed += OnClosed;

        var currentVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        CurrentVersionTextBlock.Text = string.Format(Strings.Get("Settings_CurrentVersion"), currentVersion);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // Close the window after saving
        this.Close();
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo) return;
        var tag = combo.SelectedValue as string;
        if (string.IsNullOrEmpty(tag)) return;
        try
        {
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = tag;
        }
        catch
        {
            // PrimaryLanguageOverride is not available on all SKUs; ignore.
        }
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
            ViewModel.QuickPanelVerticalPosition = newSettings.QuickPanelVerticalPosition;
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
            ViewModel.QuickPanelVerticalPosition = newSettings.QuickPanelVerticalPosition;
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
            Strings.Get("Settings_TestPromptToastTitle"),
            Strings.Get("Settings_TestPromptToastBody"),
            "",
            null,
            new List<Models.PromptActionView>
            {
                new("start", Strings.Get("Settings_TestPromptToastActionStart")),
                new("defer", Strings.Get("Settings_TestPromptToastActionDefer")),
                new("complete", Strings.Get("Settings_TestPromptToastActionComplete")),
            },
            null,
            null,
            false
        );

        PromptToastDisplayService.Instance.ShowPrompt(
            testPrompt,
            Math.Clamp(ViewModel.PromptToastMaxVisible, 1, 5),
            (actionId, _) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Test Toast] Action clicked: {actionId}");
                App.DebugLog($"[Test Toast] Action clicked: {actionId}");
                PromptToastDisplayService.Instance.Hide();
                return Task.CompletedTask;
            },
            (actionId, minutes) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Test Toast] Defer clicked: {actionId}, minutes: {minutes}");
                App.DebugLog($"[Test Toast] Defer clicked: {actionId}, minutes: {minutes}");
                PromptToastDisplayService.Instance.Hide();
                return Task.CompletedTask;
            });
    }

    private async void OnBrowsePromptToastSoundFileClick(object sender, RoutedEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            App.DebugLog($"[SettingsWindow] Prompt toast sound picker failed: {ex}");
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

        try
        {
            await PromptToastSoundService.Instance.PlayAsync(previewSettings);
        }
        catch (Exception ex)
        {
            App.DebugLog($"[SettingsWindow] Prompt toast sound test failed: {ex}");
        }
    }

    private void OnAppearanceChanged(object? sender, SystemAppearanceSnapshot snapshot)
    {
        DispatcherQueue.TryEnqueue(() => ViewModel.UpdateSystemAppearance(snapshot));
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _appearanceService.AppearanceChanged -= OnAppearanceChanged;
        AuthService.Instance.AuthStateChanged -= OnAuthStateChanged;
        Closed -= OnClosed;
    }

    private void OnAuthStateChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshAuthStatus);
    }

    private void RefreshAuthStatus()
    {
        var email = AuthService.Instance.UserEmail;
        AuthStatusTextBlock.Text = string.IsNullOrWhiteSpace(email)
            ? Strings.Get("Settings_NotSignedIn")
            : string.Format(Strings.Get("Settings_SignedInAs"), email);
    }

    private async void OnSignInClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var authWindow = new AuthWindow();
            authWindow.Activate();
            var tcs = new TaskCompletionSource();
            void OnClosed(object s, WindowEventArgs a) => tcs.TrySetResult();
            authWindow.Closed += OnClosed;
            await tcs.Task;
            authWindow.Closed -= OnClosed;
            RefreshAuthStatus();
        }
        catch (Exception ex)
        {
            AuthStatusTextBlock.Text = string.Format(Strings.Get("Settings_SignInFailed"), ex.Message);
            App.DebugLog($"[SettingsWindow] Sign-in failed: {ex}");
        }
    }

    private async void OnSignOutClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await AuthService.Instance.SignOutAsync();
            RefreshAuthStatus();
        }
        catch (Exception ex)
        {
            AuthStatusTextBlock.Text = string.Format(Strings.Get("Settings_SignOutFailed"), ex.Message);
            App.DebugLog($"[SettingsWindow] Sign-out failed: {ex}");
        }
    }

    private void OnOpenWebAccountClick(object sender, RoutedEventArgs e)
    {
        OpenExternalUrl(AppSettings.WebAccountUrl);
    }

    private async void OnCheckUpdateClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var currentVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            var result = await _updateService.CheckForUpdateAsync(string.Empty, currentVersion);
            if (!result.HasUpdate)
            {
                UpdateStatusTextBlock.Text = Strings.Get("Settings_UpToDate");
                return;
            }

            UpdateStatusTextBlock.Text = string.Format(Strings.Get("Settings_UpdateAvailable"), result.LatestVersion);
            ShowUpdateToast(result);
        }
        catch (Exception ex)
        {
            UpdateStatusTextBlock.Text = string.Format(Strings.Get("Settings_UpdateCheckFailed"), ex.Message);
        }
    }

    private void PopulateDesktopRuntimePaths()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Tastile");
        var localAppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tastile");

        RuntimeProfileTextBlock.Text = Strings.Get("Settings_ProfileAwsRemote");
        RuntimeAppDataDirTextBlock.Text = appDataDir;
        RuntimeDbPathTextBlock.Text = Strings.Get("Settings_LocalDatabaseNone");
        RuntimeSessionPathTextBlock.Text = Path.Combine(localAppDataDir, "Auth", "credentials.bin");
        RuntimeDesktopApiLogPathTextBlock.Text = CoreApiClient.DebugLogPath;
        RuntimeCreateTileLogPathTextBlock.Text = CreateTileWindow.DebugLogPath;
    }

    private static void OpenExternalUrl(string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }

    private void ShowUpdateToast(AppUpdateInfo update)
    {
        var prompt = new PromptView(
            PromptId: $"update-{Guid.NewGuid():N}",
            Kind: "app_update",
            Severity: "info",
            TileId: null,
            Title: string.Format(Strings.Get("Settings_UpdateAvailableTitle"), update.LatestVersion),
            Body: string.IsNullOrWhiteSpace(update.Notes) ? Strings.Get("Settings_UpdateBodyFallback") : update.Notes,
            Why: Strings.Get("Settings_UpdateWhy"),
            SuggestedMinutes: null,
            Actions: new List<PromptActionView>
            {
                new("install_update", Strings.Get("Settings_UpdateActionInstall")),
                new("ignore_update", Strings.Get("Settings_UpdateActionIgnore")),
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
                        var installerPath = await _updateService.DownloadInstallerAsync(update.DownloadUrl, update.Sha256);
                        AppUpdateService.StartSilentInstaller(installerPath);
                        ((App)Application.Current).Shutdown();
                    }
                    catch (Exception ex)
                    {
                        UpdateStatusTextBlock.Text = string.Format(Strings.Get("Settings_UpdateInstallFailed"), ex.Message);
                    }
                }
                else if (string.Equals(actionId, "ignore_update", StringComparison.OrdinalIgnoreCase))
                {
                    var settingsService = new SettingsService();
                    settingsService.Update(settings => settings.IgnoredUpdateVersion = update.LatestVersion);
                    UpdateStatusTextBlock.Text = string.Format(Strings.Get("Settings_UpdateIgnored"), update.LatestVersion);
                }
                await Task.CompletedTask;
            });
    }
}
