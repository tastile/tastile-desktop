using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace TastileDesktop.Services;

public static class ThemeManager
{
    public const string Light = "light";
    public const string DarkGray = "dark-gray";
    public const string DarkBlack = "dark-black";

    public static readonly string[] Modes = [Light, DarkGray, DarkBlack];

    public static ElementTheme GetElementTheme(string mode) =>
        mode == Light ? ElementTheme.Light : ElementTheme.Dark;

    public static void ApplyTheme(string mode, ResourceDictionary? resources = null)
    {
        var targetResources = resources ?? Application.Current?.Resources;
        if (targetResources == null)
        {
            return;
        }

        var palette = mode switch
        {
            DarkGray => new ThemePalette(
                Background: "#171717",
                Surface0: "#1F1F1F",
                Surface1: "#141414",
                Surface2: "#262626",
                SurfaceElevated: "#262626",
                Foreground: "#FAFAFA",
                ForegroundMuted: "#FFD4D4D4",
                ForegroundSubtle: "#737373",
                Border: "#525252",
                BorderStrong: "#737373",
                Interactive: "#FAFAFA",
                InteractiveHover: "#E5E5E5",
                InteractiveActive: "#D4D4D4",
                Primary: "#FAFAFA",
                PrimaryForeground: "#0A0A0A",
                PrimaryHover: "#E5E5E5"),
            DarkBlack => new ThemePalette(
                Background: "#000000",
                Surface0: "#0A0A0A",
                Surface1: "#050505",
                Surface2: "#171717",
                SurfaceElevated: "#171717",
                Foreground: "#FFFFFF",
                ForegroundMuted: "#FFD4D4D4",
                ForegroundSubtle: "#737373",
                Border: "#404040",
                BorderStrong: "#525252",
                Interactive: "#FFFFFF",
                InteractiveHover: "#F5F5F5",
                InteractiveActive: "#E5E5E5",
                Primary: "#FFFFFF",
                PrimaryForeground: "#000000",
                PrimaryHover: "#F5F5F5"),
            _ => new ThemePalette(
                Background: "#F5F5F5",
                Surface0: "#FAFAFA",
                Surface1: "#EFEFEF",
                Surface2: "#FFFFFF",
                SurfaceElevated: "#FFFFFF",
                Foreground: "#0A0A0A",
                ForegroundMuted: "#525252",
                ForegroundSubtle: "#A3A3A3",
                Border: "#D4D4D4",
                BorderStrong: "#A3A3A3",
                Interactive: "#0A0A0A",
                InteractiveHover: "#262626",
                InteractiveActive: "#404040",
                Primary: "#0A0A0A",
                PrimaryForeground: "#FFFFFF",
                PrimaryHover: "#262626"),
        };

        SetBrush(targetResources, "AppBackgroundBrush", palette.Background);
        SetBrush(targetResources, "AppSurface0Brush", palette.Surface0);
        SetBrush(targetResources, "AppSurface1Brush", palette.Surface1);
        SetBrush(targetResources, "AppSurface2Brush", palette.Surface2);
        SetBrush(targetResources, "AppSurfaceElevatedBrush", palette.SurfaceElevated);
        SetBrush(targetResources, "AppForegroundBrush", palette.Foreground);
        SetBrush(targetResources, "AppForegroundMutedBrush", palette.ForegroundMuted);
        SetBrush(targetResources, "AppForegroundSubtleBrush", palette.ForegroundSubtle);
        SetBrush(targetResources, "AppBorderBrush", palette.Border);
        SetBrush(targetResources, "AppBorderStrongBrush", palette.BorderStrong);
        SetBrush(targetResources, "AppInteractiveBrush", palette.Interactive);
        SetBrush(targetResources, "AppInteractiveHoverBrush", palette.InteractiveHover);
        SetBrush(targetResources, "AppInteractiveActiveBrush", palette.InteractiveActive);
        SetBrush(targetResources, "AppPrimaryBrush", palette.Primary);
        SetBrush(targetResources, "AppPrimaryForegroundBrush", palette.PrimaryForeground);
        SetBrush(targetResources, "AppPrimaryHoverBrush", palette.PrimaryHover);
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
