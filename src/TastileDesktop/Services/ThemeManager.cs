using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace TastileDesktop.Services;

public static class ThemeManager
{
    public const string System = "system";
    public const string Light = "light";
    public const string DarkGray = "dark-gray";
    public const string DarkBlack = "dark-black";

    public static readonly string[] Modes = [System];

    public static SystemAppearanceSnapshot CurrentSnapshot { get; private set; } = new(
        DarkMode: false,
        HighContrast: false,
        TransparencyEnabled: true,
        AnimationsEnabled: true,
        AccentColor: "#FF0078D4");

    public static ElementTheme GetElementTheme(string mode) =>
        mode switch
        {
            Light => ElementTheme.Light,
            DarkGray or DarkBlack => ElementTheme.Dark,
            _ => CurrentSnapshot.AppTheme,
        };

    public static ElementTheme CurrentElementTheme => CurrentSnapshot.AppTheme;

    public static void ApplySystemAppearance(SystemAppearanceSnapshot snapshot, ResourceDictionary? resources = null, TastileSettings? settings = null)
    {
        CurrentSnapshot = snapshot with { };

        // RequestedTheme is set per-window by FloatingWindowHelper.ApplyWindowTheme.
        // Here we only update the accent color brushes that are defined in ThemeDictionaries
        // and therefore need to be updated in both Dark and Light dictionaries.
        var accentColor = AccentColorPreferenceResolver.Resolve(snapshot, settings);
        var accentHex = $"#{accentColor.R:X2}{accentColor.G:X2}{accentColor.B:X2}";
        var accentHoverHex = Lighten(accentColor, 0.10);

        // Update accent brushes in both theme dictionaries so they're correct regardless of theme
        var appResources = resources ?? Application.Current?.Resources;
        if (appResources != null)
        {
            UpdateAccentInThemeDictionary(appResources, "Dark", accentHex, accentHoverHex);
            UpdateAccentInThemeDictionary(appResources, "Light", accentHex, accentHoverHex);
        }
    }

    private static void UpdateAccentInThemeDictionary(ResourceDictionary resources, string themeKey, string accentHex, string accentHoverHex)
    {
        if (!resources.ThemeDictionaries.TryGetValue(themeKey, out var themeObj) || themeObj is not ResourceDictionary themeDict)
            return;

        SetBrush(themeDict, "AccentBrush", accentHex);
        SetBrush(themeDict, "AppPrimaryBrush", accentHex);
        SetBrush(themeDict, "AppPrimaryHoverBrush", accentHoverHex);
    }

    public static void ApplyTheme(string mode, ResourceDictionary? resources = null)
    {
        if (mode == System)
        {
            ApplySystemAppearance(CurrentSnapshot, resources);
            return;
        }

        var darkMode = GetElementTheme(mode) == ElementTheme.Dark;
        var forced = new SystemAppearanceSnapshot(
            DarkMode: darkMode,
            HighContrast: CurrentSnapshot.HighContrast,
            TransparencyEnabled: CurrentSnapshot.TransparencyEnabled,
            AnimationsEnabled: CurrentSnapshot.AnimationsEnabled,
            AccentColor: CurrentSnapshot.AccentColor);

        ApplySystemAppearance(forced, resources);
    }

    public static Color GetColor(string resourceKey)
    {
        if (Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return Colors.White;
    }

    private static ThemePalette CreateDarkPalette(SystemAppearanceSnapshot snapshot)
    {
        var primary = snapshot.AccentColorValueHex;
        return new ThemePalette(
            Background: "#202020",
            Surface0: "#1C1C1C",
            Surface1: "#262626",
            Surface2: "#2F2F2F",
            SurfaceElevated: "#2B2B2B",
            Foreground: "#FFFFFF",
            ForegroundMuted: "#D6D6D6",
            ForegroundSubtle: "#A6A6A6",
            Border: "#3D3D3D",
            BorderStrong: "#575757",
            Interactive: "#FFFFFF",
            InteractiveHover: "#F4F4F4",
            InteractiveActive: "#E5E5E5",
            Primary: primary,
            PrimaryForeground: "#FFFFFF",
            PrimaryHover: Lighten(snapshot.AccentColorValue, 0.10));
    }

    private static ThemePalette CreateLightPalette(SystemAppearanceSnapshot snapshot)
    {
        var primary = snapshot.AccentColorValueHex;
        return new ThemePalette(
            Background: "#F3F3F3",
            Surface0: "#F9F9F9",
            Surface1: "#F3F3F3",
            Surface2: "#FFFFFF",
            SurfaceElevated: "#FFFFFF",
            Foreground: "#111111",
            ForegroundMuted: "#444444",
            ForegroundSubtle: "#666666",
            Border: "#D9D9D9",
            BorderStrong: "#C7C7C7",
            Interactive: "#111111",
            InteractiveHover: "#2A2A2A",
            InteractiveActive: "#3B3B3B",
            Primary: primary,
            PrimaryForeground: "#FFFFFF",
            PrimaryHover: Lighten(snapshot.AccentColorValue, 0.08));
    }

    private static ThemePalette CreateHighContrastPalette(SystemAppearanceSnapshot snapshot)
    {
        var background = snapshot.AppTheme == ElementTheme.Dark ? "#000000" : "#FFFFFF";
        var foreground = snapshot.AppTheme == ElementTheme.Dark ? "#FFFFFF" : "#000000";
        return new ThemePalette(
            Background: background,
            Surface0: background,
            Surface1: background,
            Surface2: background,
            SurfaceElevated: background,
            Foreground: foreground,
            ForegroundMuted: foreground,
            ForegroundSubtle: foreground,
            Border: foreground,
            BorderStrong: foreground,
            Interactive: foreground,
            InteractiveHover: foreground,
            InteractiveActive: foreground,
            Primary: snapshot.AccentColorValueHex,
            PrimaryForeground: "#FFFFFF",
            PrimaryHover: snapshot.AccentColorValueHex);
    }

    private static void SetBrush(ResourceDictionary resources, string key, string hex)
    {
        var color = Parse(hex);
        SetBrush(resources, key, color);
    }

    private static void SetBrush(ResourceDictionary resources, string key, Color color)
    {
        if (resources.TryGetValue(key, out var value) && value is SolidColorBrush brush)
        {
            brush.Color = color;
            return;
        }

        resources[key] = new SolidColorBrush(color);
    }

    private static Color Parse(string hex)
    {
        var normalized = hex.TrimStart('#');
        return normalized.Length switch
        {
            6 => Color.FromArgb(
                0xFF,
                Convert.ToByte(normalized.Substring(0, 2), 16),
                Convert.ToByte(normalized.Substring(2, 2), 16),
                Convert.ToByte(normalized.Substring(4, 2), 16)),
            8 => Color.FromArgb(
                Convert.ToByte(normalized.Substring(0, 2), 16),
                Convert.ToByte(normalized.Substring(2, 2), 16),
                Convert.ToByte(normalized.Substring(4, 2), 16),
                Convert.ToByte(normalized.Substring(6, 2), 16)),
            _ => Colors.Transparent,
        };
    }

    private static string Lighten(Color color, double amount)
    {
        byte Shift(byte channel)
        {
            var value = channel + (255 - channel) * amount;
            return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
        }

        return $"#{Shift(color.R):X2}{Shift(color.G):X2}{Shift(color.B):X2}";
    }

    private sealed record ThemePalette(
        string Background,
        string Surface0,
        string Surface1,
        string Surface2,
        string SurfaceElevated,
        string Foreground,
        string ForegroundMuted,
        string ForegroundSubtle,
        string Border,
        string BorderStrong,
        string Interactive,
        string InteractiveHover,
        string InteractiveActive,
        string Primary,
        string PrimaryForeground,
        string PrimaryHover);
}
