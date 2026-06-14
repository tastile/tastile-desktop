using System.Text.Json;

namespace TastileDesktop.Services;

/// <summary>
/// Manages application settings persistence.
/// </summary>
public class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Tastile");
    private static readonly string SettingsFile =
        Path.Combine(SettingsDir, "settings.json");

    public TastileSettings Current { get; private set; } = new();

    public event EventHandler? SettingsChanged;
    public static event EventHandler<TastileSettings>? GlobalSettingsChanged;

    public SettingsService()
    {
        Load();
    }

    public void Load()
    {
        if (!File.Exists(SettingsFile))
        {
            Current = new TastileSettings();
            return;
        }
        try
        {
            var json = File.ReadAllText(SettingsFile);
            Current = JsonSerializer.Deserialize<TastileSettings>(json) ?? new();
            Current = NormalizePromptSettings(Current);
        }
        catch
        {
            Current = new TastileSettings();
        }
    }

    public void Save(TastileSettings settings)
    {
        Current = NormalizePromptSettings(settings);
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        GlobalSettingsChanged?.Invoke(this, Current);
    }

    public void Update(Action<TastileSettings> update)
    {
        var updated = Current with { };
        update(updated);
        Save(updated);
    }

    private static TastileSettings NormalizePromptSettings(TastileSettings settings)
    {
        settings.PromptToastAnchor = NormalizeAnchor(settings.PromptToastAnchor);
        settings.PromptToastDisplayMode = NormalizeDisplayMode(settings.PromptToastDisplayMode);
        settings.PromptToastSoundSource = PromptToastSoundPlanBuilder.NormalizeSource(settings.PromptToastSoundSource);
        settings.PromptToastSoundPlaybackMode = PromptToastSoundPlanBuilder.NormalizePlaybackMode(settings.PromptToastSoundPlaybackMode);
        settings.PromptToastSoundDurationSeconds = Math.Clamp(settings.PromptToastSoundDurationSeconds, 1, 30);
        settings.PromptToastSoundRepeatCount = Math.Clamp(settings.PromptToastSoundRepeatCount, 1, 10);
        settings.PromptToastSoundRepeatIntervalSeconds = Math.Clamp(settings.PromptToastSoundRepeatIntervalSeconds, 1, 30);
        settings.PromptToastSoundFilePath = (settings.PromptToastSoundFilePath ?? string.Empty).Trim();
        settings.SecurityLockTimeoutMinutes = Math.Clamp(settings.SecurityLockTimeoutMinutes, 1, 240);
        settings.SecurityLockLastClosedAtUtc = (settings.SecurityLockLastClosedAtUtc ?? string.Empty).Trim();
        return settings;
    }

    private static string NormalizeAnchor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PromptToastAnchors.TopCenter;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "top-center" or "topcenter" => PromptToastAnchors.TopCenter,
            "top-right" or "topright" => PromptToastAnchors.TopRight,
            "top-left" or "topleft" => PromptToastAnchors.TopLeft,
            "bottom-center" or "bottomcenter" => PromptToastAnchors.BottomCenter,
            "bottom-right" or "bottomright" => PromptToastAnchors.BottomRight,
            "bottom-left" or "bottomleft" => PromptToastAnchors.BottomLeft,
            _ => PromptToastAnchors.TopCenter,
        };
    }

    private static string NormalizeDisplayMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PromptToastDisplayModes.PrimaryDisplay;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "primary-display" or "primarydisplay" => PromptToastDisplayModes.PrimaryDisplay,
            "active-window-display" or "activewindowdisplay" => PromptToastDisplayModes.ActiveWindowDisplay,
            "all-displays" or "alldisplays" => PromptToastDisplayModes.AllDisplays,
            _ => PromptToastDisplayModes.PrimaryDisplay,
        };
    }
}

/// <summary>
/// Application settings.
/// </summary>
public record TastileSettings
{
    public string ThemeMode { get; set; } = ThemeManager.System;
    public string AccentColorMode { get; set; } = AccentColorModes.WindowsAccent;
    public string CustomAccentColorHex { get; set; } = "#0078D4";
    public int ToastNotifyMinutes { get; set; } = 15;
    public int InterventionMinutes { get; set; } = 25;
    public bool PromptToastEnabled { get; set; } = true;
    public int PromptToastMaxVisible { get; set; } = 3;
    public string PromptToastDisplayMode { get; set; } = PromptToastDisplayModes.PrimaryDisplay;
    public string PromptToastAnchor { get; set; } = PromptToastAnchors.TopCenter;
    public bool PromptToastMirrorSecondaryDisplays { get; set; } = false;
    public bool PromptToastAnimate { get; set; } = true;
    public bool PromptToastSoundEnabled { get; set; } = true;
    public string PromptToastSoundSource { get; set; } = PromptToastSoundSources.SystemBeep;
    public string PromptToastSoundPlaybackMode { get; set; } = PromptToastSoundPlaybackModes.FixedCount;
    public string PromptToastSoundFilePath { get; set; } = string.Empty;
    public int PromptToastSoundDurationSeconds { get; set; } = 2;
    public int PromptToastSoundRepeatCount { get; set; } = 1;
    public int PromptToastSoundRepeatIntervalSeconds { get; set; } = 2;
    public bool QuickBarAlwaysOnTop { get; set; } = true;
    public string QuickPanelAnchor { get; set; } = QuickPanelAnchors.TopCenter;
    public string QuickPanelOrientation { get; set; } = QuickPanelOrientations.Horizontal;
    public string QuickPanelVerticalPosition { get; set; } = QuickPanelVerticalPositions.Top;
    public bool PromptOverlayEnabled { get; set; } = true;
    public int PromptOverlayDurationSeconds { get; set; } = 4;
    public bool PromptOverlaySuppressFullscreen { get; set; } = true;
    public int DefaultBreakMinutes { get; set; } = 5;
    public int DefaultDeferMinutes { get; set; } = 30;
    public int IdlePromptMinutes { get; set; } = 5;
    public int InterventionRepeatMinutes { get; set; } = 5;
    public bool LaunchAtStartup { get; set; } = false;
    public bool SecurityLockEnabled { get; set; } = true;
    public int SecurityLockTimeoutMinutes { get; set; } = 10;
    public string SecurityLockLastClosedAtUtc { get; set; } = string.Empty;
    public string UpdateManifestUrl { get; set; } = Environment.GetEnvironmentVariable("TASTILE_UPDATE_URL")?.Trim() ?? string.Empty;
    public string IgnoredUpdateVersion { get; set; } = string.Empty;
}
