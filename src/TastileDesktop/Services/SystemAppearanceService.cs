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
    public Windows.UI.Color WindowsAccentColor => Windows.UI.Color.FromArgb(255, 0, 120, 212);
    public Windows.UI.Color UiAccentColor => Windows.UI.Color.FromArgb(255, 0, 120, 212);
    public Windows.UI.Color AccentColorValue => WindowsAccentColor;
};

public sealed class SystemAppearanceService
{
    public static SystemAppearanceService Instance { get; } = new();
    public static SystemAppearanceService Current { get; } = Instance;
    
    public event EventHandler<SystemAppearanceSnapshot>? AppearanceChanged;
    
    public void NotifyAppearanceChanged() => OnAppearanceChanged(new SystemAppearanceSnapshot(true, false, true, true, "#FF0078D4"));
    
    public void OnAppearanceChanged(SystemAppearanceSnapshot snapshot) => AppearanceChanged?.Invoke(this, snapshot);
    
    public SystemAppearanceSnapshot GetCurrentSnapshot()
    {
        return new SystemAppearanceSnapshot(
            DarkMode: true,
            HighContrast: false,
            TransparencyEnabled: true,
            AnimationsEnabled: true,
            AccentColor: "#FF0078D4");
    }
}
