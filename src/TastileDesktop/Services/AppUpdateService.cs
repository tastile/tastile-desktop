using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TastileDesktop.Services;

public sealed record AppUpdateInfo(
    bool HasUpdate,
    string LatestVersion,
    string DownloadUrl,
    string? Notes);

public sealed class AppUpdateService
{
    private readonly HttpClient _httpClient;

    public AppUpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<AppUpdateInfo> CheckForUpdateAsync(string manifestUrl, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(manifestUrl))
        {
            return new AppUpdateInfo(false, currentVersion, string.Empty, null);
        }

        try
        {
            var manifest = await _httpClient.GetFromJsonAsync<UpdateManifest>(manifestUrl);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.LatestVersion) || string.IsNullOrWhiteSpace(manifest.DownloadUrl))
            {
                return new AppUpdateInfo(false, currentVersion, string.Empty, null);
            }

            var hasUpdate = CompareVersions(manifest.LatestVersion, currentVersion) > 0;
            return new AppUpdateInfo(
                HasUpdate: hasUpdate,
                LatestVersion: manifest.LatestVersion,
                DownloadUrl: manifest.DownloadUrl,
                Notes: manifest.Notes);
        }
        catch (HttpRequestException)
        {
            return new AppUpdateInfo(false, currentVersion, string.Empty, null);
        }
        catch (NotSupportedException)
        {
            return new AppUpdateInfo(false, currentVersion, string.Empty, null);
        }
        catch (System.Text.Json.JsonException)
        {
            return new AppUpdateInfo(false, currentVersion, string.Empty, null);
        }
    }

    public bool ShouldPromptForUpdate(AppUpdateInfo update, string? ignoredVersion)
    {
        if (!update.HasUpdate)
        {
            return false;
        }

        return !string.Equals(update.LatestVersion, ignoredVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareVersions(string left, string right)
    {
        if (Version.TryParse(left, out var lv) && Version.TryParse(right, out var rv))
        {
            return lv.CompareTo(rv);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class UpdateManifest
    {
        [JsonPropertyName("latest_version")]
        public string LatestVersion { get; set; } = string.Empty;

        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }
}
