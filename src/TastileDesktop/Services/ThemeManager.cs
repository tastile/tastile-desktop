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

        SetBrush(themeDict, "OverrideAccentFillBrush", accentHex);
        SetBrush(themeDict, "OverrideAccentFillSecondaryBrush", accentHoverHex);
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
}
