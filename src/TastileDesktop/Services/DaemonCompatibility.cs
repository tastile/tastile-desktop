using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TastileDesktop.Services;

internal static class DaemonCompatibility
{
    public static async Task<bool> IsCompatibleAsync(HttpClient httpClient, string? daemonBinaryPath = null, string? expectedBinarySha256 = null)
    {
        try
        {
            var healthResponse = await httpClient.GetAsync("/health");
            if (!healthResponse.IsSuccessStatusCode)
            {
                return false;
            }

            var versionResponse = await httpClient.GetAsync("/version");
            if (!versionResponse.IsSuccessStatusCode)
            {
                return false;
            }

            using var contentStream = await versionResponse.Content.ReadAsStreamAsync();
            var payload = await JsonSerializer.DeserializeAsync<VersionPayload>(
                contentStream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (payload is null
                || string.IsNullOrWhiteSpace(payload.Version)
                || !string.Equals(payload.App, "tastile-daemon", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(payload.BinarySha256))
            {
                return false;
            }

            if (!IsValidSha256Hex(payload.BinarySha256))
            {
                return false;
            }

            var expected = ResolveExpectedSha256(daemonBinaryPath, expectedBinarySha256);
            if (expected is null)
            {
                return false;
            }

            if (!string.Equals(payload.BinarySha256, expected, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void KillStaleDaemonProcesses(int currentProcessId, int? protectedDaemonProcessId = null)
    {
        foreach (var process in Process.GetProcessesByName("tastile-daemon"))
        {
            try
            {
                if (process.Id == currentProcessId)
                {
                    continue;
                }

                if (protectedDaemonProcessId.HasValue && process.Id == protectedDaemonProcessId.Value)
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

    private sealed class VersionPayload
    {
        public string? Version { get; set; }
        public string? App { get; set; }
        [JsonPropertyName("binary_sha256")]
        public string? BinarySha256 { get; set; }
    }

    private static string? ResolveExpectedSha256(string? daemonBinaryPath, string? expectedBinarySha256)
    {
        if (!string.IsNullOrWhiteSpace(expectedBinarySha256))
        {
            return expectedBinarySha256.Trim().ToLowerInvariant();
        }

        if (string.IsNullOrWhiteSpace(daemonBinaryPath) || !File.Exists(daemonBinaryPath))
        {
            return null;
        }

        return ComputeFileSha256(daemonBinaryPath);
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsValidSha256Hex(string sha)
    {
        if (sha.Length != 64)
        {
            return false;
        }

        foreach (var ch in sha)
        {
            if (!Uri.IsHexDigit(ch))
            {
                return false;
            }
        }

        return true;
    }
}
