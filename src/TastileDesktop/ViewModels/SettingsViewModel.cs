using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using TastileDesktop.Services;

namespace TastileDesktop.ViewModels;

/// <summary>
/// ViewModel for the settings window.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly StartupRegistrationService _startupRegistrationService;
    private int _toastNotifyMinutes;
    private int _interventionMinutes;
    private bool _promptToastEnabled;
    private int _promptToastMaxVisible;
    private string _promptToastDisplayMode = PromptToastDisplayModes.PrimaryDisplay;
    private string _promptToastAnchor = PromptToastAnchors.TopCenter;
    private bool _promptToastMirrorSecondaryDisplays;
    private bool _promptToastAnimate = true;
    private bool _promptToastSoundEnabled = true;
    private string _promptToastSoundSource = PromptToastSoundSources.SystemBeep;
    private string _promptToastSoundPlaybackMode = PromptToastSoundPlaybackModes.FixedCount;
    private string _promptToastSoundFilePath = string.Empty;
    private int _promptToastSoundDurationSeconds = 2;
    private int _promptToastSoundRepeatCount = 1;
    private int _promptToastSoundRepeatIntervalSeconds = 2;
    private bool _promptOverlayEnabled;
    private int _promptOverlayDurationSeconds;
    private bool _promptOverlaySuppressFullscreen;
    private int _defaultBreakMinutes;
    private int _idlePromptMinutes;
    private int _interventionRepeatMinutes;
    private bool _launchAtStartup;
    private string _accentColorMode = AccentColorModes.WindowsAccent;
    private string _customAccentColorHex = "#0078D4";
    private string _windowsAccentColorHex = "#000000";
    private string _uiAccentColorHex = "#000000";
    private string _quickPanelVerticalPosition = QuickPanelVerticalPositions.Top;
    private string _appThemeSummary = "Unknown";
    private string _systemThemeSummary = "Unknown";
    private string _transparencySummary = "Unknown";
    private string _animationsSummary = "Unknown";
    private string _highContrastSummary = "Unknown";
    private string _taskbarAlignmentSummary = "Unknown";
    private string _accentColorHex = "#000000";
    private SolidColorBrush _accentBrush = new(Colors.Transparent);
    private SolidColorBrush _windowsAccentBrush = new(Colors.Transparent);
    private SolidColorBrush _uiAccentBrush = new(Colors.Transparent);

    public string AccentColorMode
    {
        get => _accentColorMode;
        set
        {
            if (SetProperty(ref _accentColorMode, value))
            {
                OnPropertyChanged(nameof(IsManualAccentColor));
                OnPropertyChanged(nameof(ManualAccentVisibility));
                RefreshAccentPreview();
            }
        }
    }

    public string CustomAccentColorHex
    {
        get => _customAccentColorHex;
        set
        {
            if (SetProperty(ref _customAccentColorHex, value))
            {
                RefreshAccentPreview();
            }
        }
    }

    public string WindowsAccentColorHex
    {
        get => _windowsAccentColorHex;
        set => SetProperty(ref _windowsAccentColorHex, value);
    }

    public string UiAccentColorHex
    {
        get => _uiAccentColorHex;
        set => SetProperty(ref _uiAccentColorHex, value);
    }

    public string AppThemeSummary
    {
        get => _appThemeSummary;
        set => SetProperty(ref _appThemeSummary, value);
    }

    public string SystemThemeSummary
    {
        get => _systemThemeSummary;
        set => SetProperty(ref _systemThemeSummary, value);
    }

    public string TransparencySummary
    {
        get => _transparencySummary;
        set => SetProperty(ref _transparencySummary, value);
    }

    public string AnimationsSummary
    {
        get => _animationsSummary;
        set => SetProperty(ref _animationsSummary, value);
    }

    public string HighContrastSummary
    {
        get => _highContrastSummary;
        set => SetProperty(ref _highContrastSummary, value);
    }

    public string TaskbarAlignmentSummary
    {
        get => _taskbarAlignmentSummary;
        set => SetProperty(ref _taskbarAlignmentSummary, value);
    }

    public string AccentColorHex
    {
        get => _accentColorHex;
        set => SetProperty(ref _accentColorHex, value);
    }

    public SolidColorBrush AccentBrush
    {
        get => _accentBrush;
        set => SetProperty(ref _accentBrush, value);
    }

    public SolidColorBrush WindowsAccentBrush
    {
        get => _windowsAccentBrush;
        set => SetProperty(ref _windowsAccentBrush, value);
    }

    public SolidColorBrush UiAccentBrush
    {
        get => _uiAccentBrush;
        set => SetProperty(ref _uiAccentBrush, value);
    }

    public string QuickPanelVerticalPosition
    {
        get => _quickPanelVerticalPosition;
        set => SetProperty(ref _quickPanelVerticalPosition, value);
    }

    public bool IsManualAccentColor => string.Equals(AccentColorMode, AccentColorModes.Manual, StringComparison.Ordinal);

    public Visibility ManualAccentVisibility => IsManualAccentColor ? Visibility.Visible : Visibility.Collapsed;

    public int ToastNotifyMinutes
    {
        get => _toastNotifyMinutes;
        set => SetProperty(ref _toastNotifyMinutes, value);
    }

    public bool PromptToastEnabled
    {
        get => _promptToastEnabled;
        set => SetProperty(ref _promptToastEnabled, value);
    }

    public int PromptToastMaxVisible
    {
        get => _promptToastMaxVisible;
        set => SetProperty(ref _promptToastMaxVisible, value);
    }

    public string PromptToastDisplayMode
    {
        get => _promptToastDisplayMode;
        set => SetProperty(ref _promptToastDisplayMode, value);
    }

    public string PromptToastAnchor
    {
        get => _promptToastAnchor;
        set => SetProperty(ref _promptToastAnchor, value);
    }

    public bool PromptToastMirrorSecondaryDisplays
    {
        get => _promptToastMirrorSecondaryDisplays;
        set => SetProperty(ref _promptToastMirrorSecondaryDisplays, value);
    }

    public bool PromptToastAnimate
    {
        get => _promptToastAnimate;
        set => SetProperty(ref _promptToastAnimate, value);
    }

    public bool PromptToastSoundEnabled
    {
        get => _promptToastSoundEnabled;
        set => SetProperty(ref _promptToastSoundEnabled, value);
    }

    public string PromptToastSoundSource
    {
        get => _promptToastSoundSource;
        set
        {
            if (SetProperty(ref _promptToastSoundSource, value))
            {
                OnPropertyChanged(nameof(IsCustomPromptToastSoundSource));
                OnPropertyChanged(nameof(PromptToastCustomSoundFileVisibility));
            }
        }
    }

    public string PromptToastSoundPlaybackMode
    {
        get => _promptToastSoundPlaybackMode;
        set
        {
            if (SetProperty(ref _promptToastSoundPlaybackMode, value))
            {
                OnPropertyChanged(nameof(IsFixedPromptToastSoundPlaybackMode));
                OnPropertyChanged(nameof(PromptToastFixedCountSettingsVisibility));
            }
        }
    }

    public string PromptToastSoundFilePath
    {
        get => _promptToastSoundFilePath;
        set => SetProperty(ref _promptToastSoundFilePath, value);
    }

    public int PromptToastSoundDurationSeconds
    {
        get => _promptToastSoundDurationSeconds;
        set => SetProperty(ref _promptToastSoundDurationSeconds, value);
    }

    public int PromptToastSoundRepeatCount
    {
        get => _promptToastSoundRepeatCount;
        set => SetProperty(ref _promptToastSoundRepeatCount, value);
    }

    public int PromptToastSoundRepeatIntervalSeconds
    {
        get => _promptToastSoundRepeatIntervalSeconds;
        set => SetProperty(ref _promptToastSoundRepeatIntervalSeconds, value);
    }

    public bool IsCustomPromptToastSoundSource => string.Equals(PromptToastSoundSource, PromptToastSoundSources.CustomFile, StringComparison.Ordinal);

    public Visibility PromptToastCustomSoundFileVisibility => IsCustomPromptToastSoundSource ? Visibility.Visible : Visibility.Collapsed;

    public bool IsFixedPromptToastSoundPlaybackMode => string.Equals(PromptToastSoundPlaybackMode, PromptToastSoundPlaybackModes.FixedCount, StringComparison.Ordinal);

    public Visibility PromptToastFixedCountSettingsVisibility => IsFixedPromptToastSoundPlaybackMode ? Visibility.Visible : Visibility.Collapsed;

    public int InterventionMinutes
    {
        get => _interventionMinutes;
        set => SetProperty(ref _interventionMinutes, value);
    }

    public bool PromptOverlayEnabled
    {
        get => _promptOverlayEnabled;
        set => SetProperty(ref _promptOverlayEnabled, value);
    }

    public int PromptOverlayDurationSeconds
    {
        get => _promptOverlayDurationSeconds;
        set => SetProperty(ref _promptOverlayDurationSeconds, value);
    }

    public bool PromptOverlaySuppressFullscreen
    {
        get => _promptOverlaySuppressFullscreen;
        set => SetProperty(ref _promptOverlaySuppressFullscreen, value);
    }

    public int DefaultBreakMinutes
    {
        get => _defaultBreakMinutes;
        set => SetProperty(ref _defaultBreakMinutes, value);
    }

    public int IdlePromptMinutes
    {
        get => _idlePromptMinutes;
        set => SetProperty(ref _idlePromptMinutes, value);
    }

    public int InterventionRepeatMinutes
    {
        get => _interventionRepeatMinutes;
        set => SetProperty(ref _interventionRepeatMinutes, value);
    }

    public bool LaunchAtStartup
    {
        get => _launchAtStartup;
        set => SetProperty(ref _launchAtStartup, value);
    }

    public SettingsViewModel()
    {
        _settingsService = new SettingsService();
        _startupRegistrationService = new StartupRegistrationService();
        LoadSettings();
    }

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _startupRegistrationService = new StartupRegistrationService();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var current = _settingsService.Current;
        AccentColorMode = current.AccentColorMode;
        CustomAccentColorHex = current.CustomAccentColorHex;
        ToastNotifyMinutes = current.ToastNotifyMinutes;
        InterventionMinutes = current.InterventionMinutes;
        PromptToastEnabled = current.PromptToastEnabled;
        PromptToastMaxVisible = current.PromptToastMaxVisible;
        PromptToastDisplayMode = current.PromptToastDisplayMode;
        PromptToastAnchor = current.PromptToastAnchor;
        PromptToastMirrorSecondaryDisplays = current.PromptToastMirrorSecondaryDisplays;
        PromptToastAnimate = current.PromptToastAnimate;
        PromptToastSoundEnabled = current.PromptToastSoundEnabled;
        PromptToastSoundSource = current.PromptToastSoundSource;
        PromptToastSoundPlaybackMode = current.PromptToastSoundPlaybackMode;
        PromptToastSoundFilePath = current.PromptToastSoundFilePath;
        PromptToastSoundDurationSeconds = current.PromptToastSoundDurationSeconds;
        PromptToastSoundRepeatCount = current.PromptToastSoundRepeatCount;
        PromptToastSoundRepeatIntervalSeconds = current.PromptToastSoundRepeatIntervalSeconds;
        PromptOverlayEnabled = current.PromptOverlayEnabled;
        PromptOverlayDurationSeconds = current.PromptOverlayDurationSeconds;
        PromptOverlaySuppressFullscreen = current.PromptOverlaySuppressFullscreen;
        DefaultBreakMinutes = current.DefaultBreakMinutes;
        IdlePromptMinutes = current.IdlePromptMinutes;
        InterventionRepeatMinutes = current.InterventionRepeatMinutes;
        LaunchAtStartup = current.LaunchAtStartup;
        QuickPanelVerticalPosition = current.QuickPanelVerticalPosition;
        UpdateSystemAppearance(SystemAppearanceService.Instance.GetCurrentSnapshot());
    }

    public void UpdateSystemAppearance(SystemAppearanceSnapshot snapshot)
    {
        AppThemeSummary = snapshot.AppThemeLabel;
        SystemThemeSummary = snapshot.SystemThemeLabel;
        TransparencySummary = snapshot.TransparencyLabel;
        AnimationsSummary = snapshot.AnimationsLabel;
        HighContrastSummary = snapshot.HighContrastLabel;
        TaskbarAlignmentSummary = snapshot.TaskbarAlignmentLabel;
        var resolvedAccent = AccentColorPreferenceResolver.Resolve(snapshot, _settingsService.Current with
        {
            AccentColorMode = AccentColorMode,
            CustomAccentColorHex = CustomAccentColorHex,
        });
        AccentColorHex = $"#{resolvedAccent.R:X2}{resolvedAccent.G:X2}{resolvedAccent.B:X2}";
        AccentBrush = new SolidColorBrush(resolvedAccent);
        WindowsAccentColorHex = snapshot.WindowsAccentColorHex;
        UiAccentColorHex = snapshot.UiAccentColorHex;
        WindowsAccentBrush = new SolidColorBrush(snapshot.WindowsAccentColor);
        UiAccentBrush = new SolidColorBrush(snapshot.UiAccentColor);
    }

    private void RefreshAccentPreview()
        => UpdateSystemAppearance(SystemAppearanceService.Instance.GetCurrentSnapshot());

    [RelayCommand]
    private void Save()
    {
        var current = _settingsService.Current;
        var settings = new TastileSettings
        {
            ThemeMode = ThemeManager.System,
            AccentColorMode = AccentColorMode,
            CustomAccentColorHex = CustomAccentColorHex,
            ToastNotifyMinutes = ToastNotifyMinutes,
            InterventionMinutes = InterventionMinutes,
            PromptToastEnabled = PromptToastEnabled,
            PromptToastMaxVisible = PromptToastMaxVisible,
            PromptToastDisplayMode = PromptToastDisplayMode,
            PromptToastAnchor = PromptToastAnchor,
            PromptToastMirrorSecondaryDisplays = PromptToastMirrorSecondaryDisplays,
            PromptToastAnimate = PromptToastAnimate,
            PromptToastSoundEnabled = PromptToastSoundEnabled,
            PromptToastSoundSource = PromptToastSoundSource,
            PromptToastSoundPlaybackMode = PromptToastSoundPlaybackMode,
            PromptToastSoundFilePath = PromptToastSoundFilePath,
            PromptToastSoundDurationSeconds = PromptToastSoundDurationSeconds,
            PromptToastSoundRepeatCount = PromptToastSoundRepeatCount,
            PromptToastSoundRepeatIntervalSeconds = PromptToastSoundRepeatIntervalSeconds,
            PromptOverlayEnabled = PromptOverlayEnabled,
            PromptOverlayDurationSeconds = PromptOverlayDurationSeconds,
            PromptOverlaySuppressFullscreen = PromptOverlaySuppressFullscreen,
            DefaultBreakMinutes = DefaultBreakMinutes,
            IdlePromptMinutes = IdlePromptMinutes,
            InterventionRepeatMinutes = InterventionRepeatMinutes,
            LaunchAtStartup = LaunchAtStartup,
            QuickPanelVerticalPosition = QuickPanelVerticalPosition,
            UpdateManifestUrl = current.UpdateManifestUrl,
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
            _startupRegistrationService.Apply(enable);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Startup task registration failed: {ex.Message}");
        }
    }
}
