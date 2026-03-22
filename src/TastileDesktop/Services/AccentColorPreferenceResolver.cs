using Windows.UI;

namespace TastileDesktop.Services;

public static class AccentColorPreferenceResolver
{
    public static Color Resolve(SystemAppearanceSnapshot snapshot, TastileSettings? settings)
    {
        if (settings == null)
        {
            return snapshot.AccentColorValue;
        }
        return settings.AccentColorMode switch
        {
            AccentColorModes.WindowsAccent => snapshot.AccentColorValue,
            AccentColorModes.Custom or AccentColorModes.Manual => Parse(settings.CustomAccentColorHex),
            _ => snapshot.AccentColorValue,
        };
    }

    private static Color Parse(string hex)
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
            _ => Windows.UI.Color.FromArgb(0xFF, 0x00, 0x78, 0xD4),
        };
    }
}
