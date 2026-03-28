namespace TastileDesktop.Services;

public sealed class StartupRegistrationService
{
    public const string RegistryValueName = "TastileDesktop";
    private const string RegistryRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static string BuildCommand(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }

        return $"\"{executablePath}\" --minimized";
    }

    public void Apply(bool enable, string? executablePath = null)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryRunKeyPath, writable: true);
        if (key == null)
        {
            return;
        }

        if (enable)
        {
            var resolvedPath = executablePath ?? Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return;
            }

            key.SetValue(RegistryValueName, BuildCommand(resolvedPath));
            return;
        }

        key.DeleteValue(RegistryValueName, throwOnMissingValue: false);
    }
}
