using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace TastileDesktop.Services;

public class DaemonManager : IDisposable
{
    private Process? _daemonProcess;
    private readonly string _daemonPath;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };

    public DaemonManager()
    {
        // Look for daemon next to this exe, then in PATH
        var appDir = AppContext.BaseDirectory;
        var localPath = Path.Combine(appDir, "tastile-daemon.exe");
        _daemonPath = File.Exists(localPath) ? localPath : "tastile-daemon.exe";
    }

    public async Task EnsureRunningAsync()
    {
        // Check if already running
        if (await IsHealthyAsync()) return;

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
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start daemon: {ex.Message}");
            return;
        }

        // Wait for health
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(500);
            if (await IsHealthyAsync()) return;
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var resp = await _http.GetAsync("http://localhost:3140/health");
            return resp.IsSuccessStatusCode;
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
