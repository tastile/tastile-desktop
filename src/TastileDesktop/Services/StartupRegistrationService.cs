using Windows.ApplicationModel;

namespace TastileDesktop.Services;

public sealed class StartupRegistrationService
{
    public const string RegistryValueName = "TastileDesktop";

    /// <summary>
    /// TaskId of the <c>windows.startupTask</c> extension declared in
    /// <c>Package.appxmanifest</c>. Must stay in sync with the manifest.
    /// </summary>
    public const string StartupTaskId = "TastileStartup";

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
        // Packaged (MSIX) builds cannot rely on the HKCU Run key being honored
        // for launch-at-startup; they must go through StartupTask instead.
        // Unpackaged builds keep the registry path. When the packaged lookup
        // itself is unavailable (unpackaged process), fall back to registry.
        if (TryApplyStartupTask(enable))
        {
            return;
        }

        ApplyRegistry(enable, executablePath);
    }

    private void ApplyRegistry(bool enable, string? executablePath)
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

    /// <summary>
    /// Returns <c>true</c> when the app is running with package identity and
    /// the StartupTask API handled the request; <c>false</c> when the process
    /// is unpackaged and the caller should use the registry fallback.
    /// </summary>
    private static bool TryApplyStartupTask(bool enable)
    {
        try
        {
            var task = StartupTask.GetAsync(StartupTaskId)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            if (enable)
            {
                // DisabledByUser / DisabledByPolicy require the user to
                // re-enable the task in Task Manager Startup apps; requesting
                // enablement is still the correct packaged-side action.
                if (task.State != StartupTaskState.Enabled &&
                    task.State != StartupTaskState.EnabledByPolicy)
                {
                    task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            else
            {
                task.Disable();
            }

            return true;
        }
        catch
        {
            // Unpackaged process: package APIs are unavailable.
            return false;
        }
    }
}
