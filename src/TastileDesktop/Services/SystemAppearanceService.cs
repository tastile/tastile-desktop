using Windows.UI.ViewManagement;

namespace TastileDesktop.Services;

public sealed record SystemAppearanceSnapshot(
    bool DarkMode,
    bool HighContrast,
    bool TransparencyEnabled,
    bool AnimationsEnabled,
    string AccentColor)
{
    public bool HighContrastEnabled => HighContrast;
    public Microsoft.UI.Xaml.ElementTheme AppTheme => DarkMode ? Microsoft.UI.Xaml.ElementTheme.Dark : Microsoft.UI.Xaml.ElementTheme.Light;
    public Microsoft.UI.Xaml.ElementTheme SystemTheme => DarkMode ? Microsoft.UI.Xaml.ElementTheme.Dark : Microsoft.UI.Xaml.ElementTheme.Light;
    public string AppThemeLabel => DarkMode ? "Dark" : "Light";
    public string SystemThemeLabel => DarkMode ? "Dark" : "Light";
    public string TransparencyLabel => TransparencyEnabled ? "On" : "Off";
    public string AnimationsLabel => AnimationsEnabled ? "On" : "Off";
    public string HighContrastLabel => HighContrast ? "On" : "Off";
    public string TaskbarAlignmentLabel => "Left";
    public string WindowsAccentColorHex => AccentColor;
    public string UiAccentColorHex => AccentColor;
    public string AccentColorValueHex => AccentColor;
    public string AccentColorHex => AccentColor;
    public Windows.UI.Color WindowsAccentColor => ParseColor(AccentColor);
    public Windows.UI.Color UiAccentColor => ParseColor(AccentColor);
    public Windows.UI.Color AccentColorValue => ParseColor(AccentColor);

    private static Windows.UI.Color ParseColor(string hex)
    {
        var normalized = hex.TrimStart('#');
        return normalized.Length switch
        {
            6 => Windows.UI.Color.FromArgb(
                0xFF,
                Convert.ToByte(normalized.Substring(0, 2), 16),
                Convert.ToByte(normalized.Substring(2, 2), 16),
                Convert.ToByte(normalized.Substring(4, 2), 16)),
            8 => Windows.UI.Color.FromArgb(
                Convert.ToByte(normalized.Substring(0, 2), 16),
                Convert.ToByte(normalized.Substring(2, 2), 16),
                Convert.ToByte(normalized.Substring(4, 2), 16),
                Convert.ToByte(normalized.Substring(6, 2), 16)),
            _ => Windows.UI.Color.FromArgb(255, 0, 120, 212),
        };
    }
};

public sealed class SystemAppearanceService
{
    public static SystemAppearanceService Instance { get; } = new();
    public static SystemAppearanceService Current { get; } = Instance;

    private readonly UISettings _uiSettings = new();

    public event EventHandler<SystemAppearanceSnapshot>? AppearanceChanged;

    public SystemAppearanceService()
    {
        _uiSettings.ColorValuesChanged += (_, _) =>
        {
            OnAppearanceChanged(GetCurrentSnapshot());
        };
    }

    public void NotifyAppearanceChanged() => OnAppearanceChanged(GetCurrentSnapshot());

    public void OnAppearanceChanged(SystemAppearanceSnapshot snapshot) => AppearanceChanged?.Invoke(this, snapshot);

    public SystemAppearanceSnapshot GetCurrentSnapshot()
    {
        var uiSettings = _uiSettings;

        // Dark mode: foreground is light when dark mode is active
        var foreground = uiSettings.GetColorValue(UIColorType.Foreground);
        var darkMode = foreground.R > 128;

        // Accent color from system
        var accent = uiSettings.GetColorValue(UIColorType.Accent);
        var accentHex = $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}";

        // High contrast
        var background = uiSettings.GetColorValue(UIColorType.Background);
        var highContrast = background.R == 0 && background.G == 0 && background.B == 0
                           && foreground.R == 255 && foreground.G == 255 && foreground.B == 255;

        return new SystemAppearanceSnapshot(
            DarkMode: darkMode,
            HighContrast: highContrast,
            TransparencyEnabled: true,
            AnimationsEnabled: true,
            AccentColor: accentHex);
    }
}
