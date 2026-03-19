using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TastileDesktop.Services;
using System.Collections.Generic;

namespace TastileDesktop.ViewModels;

/// <summary>
/// ViewModel for the settings window.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    public IReadOnlyList<string> ThemeModes { get; } = ThemeManager.Modes;

    [ObservableProperty]
    private string _themeMode = ThemeManager.Light;

    [ObservableProperty]
    private int _toastNotifyMinutes;

    [ObservableProperty]
    private int _interventionMinutes;

    [ObservableProperty]
    private int _defaultBreakMinutes;

    [ObservableProperty]
    private int _idlePromptMinutes;

    [ObservableProperty]
    private int _interventionRepeatMinutes;

    [ObservableProperty]
    private bool _launchAtStartup;

    public SettingsViewModel()
    {
        _settingsService = new SettingsService();
        LoadSettings();
    }

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var current = _settingsService.Current;
        ThemeMode = current.ThemeMode;
        ToastNotifyMinutes = current.ToastNotifyMinutes;
        InterventionMinutes = current.InterventionMinutes;
        DefaultBreakMinutes = current.DefaultBreakMinutes;
        IdlePromptMinutes = current.IdlePromptMinutes;
        InterventionRepeatMinutes = current.InterventionRepeatMinutes;
        LaunchAtStartup = current.LaunchAtStartup;
    }

    [RelayCommand]
    private void Save()
    {
        var settings = new TastileSettings
        {
            ThemeMode = ThemeMode,
            ToastNotifyMinutes = ToastNotifyMinutes,
            InterventionMinutes = InterventionMinutes,
            DefaultBreakMinutes = DefaultBreakMinutes,
            IdlePromptMinutes = IdlePromptMinutes,
            InterventionRepeatMinutes = InterventionRepeatMinutes,
            LaunchAtStartup = LaunchAtStartup,
        };
        _settingsService.Save(settings);
        
        // Handle startup task
        UpdateStartupTask(LaunchAtStartup);
    }

    [RelayCommand]
    private void Cancel()
    {
        LoadSettings();
    }

    private void UpdateStartupTask(bool enable)
    {
        try
        {
            const string valueName = "Tastile";
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);

            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (exePath != null)
                    key.SetValue(valueName, $"\"{exePath}\" --minimized");
            }
            else
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Startup task registration failed: {ex.Message}");
        }
    }
}
