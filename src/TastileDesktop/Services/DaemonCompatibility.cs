using System.Diagnostics;
using System.Net.Http;

namespace TastileDesktop.Services;

internal static class DaemonCompatibility
{
    public static async Task<bool> IsCompatibleAsync(HttpClient httpClient)
    {
        try
        {
            var healthResponse = await httpClient.GetAsync("/health");
            if (!healthResponse.IsSuccessStatusCode)
            {
                return false;
            }

            var versionResponse = await httpClient.GetAsync("/version");
            return versionResponse.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public static void KillStaleDaemonProcesses(int currentProcessId)
    {
        foreach (var process in Process.GetProcessesByName("tastile-daemon"))
        {
            try
            {
                if (process.Id == currentProcessId)
                {
                    continue;
                }

                process.Kill(entireProcessTree: false);
            }
            catch
            {
                // Ignore unrelated or already-exited processes.
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
