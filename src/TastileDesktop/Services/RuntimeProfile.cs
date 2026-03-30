namespace TastileDesktop.Services;

public static class RuntimeProfile
{
    private const string ProfileEnvVar = "TASTILE_PROFILE";
    private const string DefaultProfile = "prod";

    public static string Name { get; } = ResolveProfileName();

    public static bool IsDevelopment =>
        string.Equals(Name, "dev", StringComparison.OrdinalIgnoreCase);

    public static int DaemonPort =>
        ResolveIntEnvironment("TASTILE_DAEMON_PORT", IsDevelopment ? 3141 : 3140);

    public static string DaemonBaseUrl => $"http://127.0.0.1:{DaemonPort}";

    public static string AppDataDirectoryName =>
        string.Equals(Name, DefaultProfile, StringComparison.OrdinalIgnoreCase)
            ? "Tastile"
            : $"Tastile-{Name}";

    public static string GetAppDataDirectory()
    {
        var appData = Environment.GetEnvironmentVariable("APPDATA");
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        return Path.Combine(appData, AppDataDirectoryName);
    }

    public static string GetLocalAppDataDirectory()
    {
        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        return Path.Combine(localAppData, AppDataDirectoryName);
    }

    public static string? ResolveEnvironmentValue(string key, string? fallback = null)
    {
        var scopedKey = ToScopedKey(key);
        var scopedValue = Environment.GetEnvironmentVariable(scopedKey);
        if (!string.IsNullOrWhiteSpace(scopedValue))
        {
            return scopedValue;
        }

        var directValue = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(directValue))
        {
            return directValue;
        }

        return fallback;
    }

    private static string ResolveProfileName()
    {
        var raw = Environment.GetEnvironmentVariable(ProfileEnvVar)?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DefaultProfile;
        }

        var normalized = raw.ToLowerInvariant();
        return normalized switch
        {
            "production" => DefaultProfile,
            "release" => DefaultProfile,
            _ => normalized,
        };
    }

    private static string ToScopedKey(string key)
    {
        var profileKey = Name.ToUpperInvariant();
        if (key.StartsWith("TASTILE_", StringComparison.OrdinalIgnoreCase))
        {
            return $"TASTILE_{profileKey}_{key["TASTILE_".Length..]}";
        }

        return $"TASTILE_{profileKey}_{key}";
    }

    private static int ResolveIntEnvironment(string key, int fallback)
    {
        var raw = ResolveEnvironmentValue(key);
        return int.TryParse(raw, out var value) ? value : fallback;
    }
}
