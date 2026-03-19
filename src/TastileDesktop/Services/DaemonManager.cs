using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace TastileDesktop.Services;

// Helper to log to file
internal static class DaemonLog
{
    public static void Write(string msg)
    {
        var path = Path.Combine(Path.GetTempPath(), "tastile-daemon.log");
        File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}");
    }
}

public class DaemonManager : IDisposable
{
    private Process? _daemonProcess;
    private string _daemonPath;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };

    public DaemonManager()
    {
        // Look for daemon next to this exe, then in PATH
        var appDir = AppContext.BaseDirectory;
        var localPath = Path.Combine(appDir, "tastile-daemon.exe");
            _daemonPath = File.Exists(localPath) ? localPath : "tastile-daemon.exe";
            
            // Also check in publish folder (common deployment location)
            if (!File.Exists(_daemonPath))
            {
                var publishPath = Path.Combine(appDir, "..", "..", "tastile-daemon.exe");
                publishPath = Path.GetFullPath(publishPath);
                if (File.Exists(publishPath))
                {
                    _daemonPath = publishPath;
                }
            }
    }

    public async Task<bool> EnsureRunningAsync()
    {
        // If a compatible daemon is already running, reuse it.
        if (await DaemonCompatibility.IsCompatibleAsync(_http))
        {
            return true;
        }

        // A stale daemon can still answer /health but lack required routes.
        DaemonCompatibility.KillStaleDaemonProcesses(Process.GetCurrentProcess().Id);

        DaemonLog.Write($"Starting daemon from: {_daemonPath}");
        
        // Verify daemon exists
        if (!File.Exists(_daemonPath))
        {
            DaemonLog.Write($"Daemon not found at: {_daemonPath}");
            // Try current directory as fallback
            var fallbackPath = Path.Combine(Directory.GetCurrentDirectory(), "tastile-daemon.exe");
            DaemonLog.Write($"Trying fallback: {fallbackPath}");
            if (File.Exists(fallbackPath))
            {
                DaemonLog.Write("Fallback found!");
                _daemonPath = fallbackPath;
            }
            else
            {
                return false;
            }
        }

        // Start daemon as child process
        var psi = new ProcessStartInfo
        {
            FileName = _daemonPath,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            _daemonProcess = Process.Start(psi);
            DaemonLog.Write($"Daemon process started with PID: {_daemonProcess?.Id}");
        }
        catch (Exception ex)
        {
            DaemonLog.Write($"Failed to start daemon: {ex.Message}");
            return false;
        }

        // Wait for health
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(500);
            if (await IsHealthyAsync()) return true;
        }
        
        DaemonLog.Write("Daemon failed to become healthy within timeout");
        return false;
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            return await DaemonCompatibility.IsCompatibleAsync(_http);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_daemonProcess is { HasExited: false })
        {
            _daemonProcess.Kill();
            _daemonProcess.Dispose();
        }
        _http.Dispose();
    }
}
