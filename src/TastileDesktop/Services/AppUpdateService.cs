using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace TastileDesktop.Services;

public sealed record AppUpdateInfo(
    bool HasUpdate,
    string LatestVersion,
    string DownloadUrl,
    string? Notes);

public sealed class AppUpdateService
{
    private const string DefaultUpdateEndpoint = "https://tastile.app/api/version";
    private readonly HttpClient _httpClient;

    public AppUpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<AppUpdateInfo> CheckForUpdateAsync(string manifestUrl, string currentVersion)
    {
        var resolvedManifestUrl = ResolveManifestUrl(manifestUrl);

        try
        {
            var manifest = await _httpClient.GetFromJsonAsync<UpdateManifest>(resolvedManifestUrl);
            var latestVersion = manifest?.ResolvedLatestVersion;
            var downloadUrl = manifest?.ResolvedDownloadUrl;
            if (string.IsNullOrWhiteSpace(latestVersion) || string.IsNullOrWhiteSpace(downloadUrl))
            {
                return new AppUpdateInfo(false, currentVersion, string.Empty, null);
            }

            var hasUpdate = CompareVersions(latestVersion, currentVersion) > 0;
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri) ||
                !string.Equals(downloadUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return new AppUpdateInfo(false, currentVersion, string.Empty, null);
            }

            return new AppUpdateInfo(
                HasUpdate: hasUpdate,
                LatestVersion: latestVersion,
                DownloadUrl: downloadUri.ToString(),
                Notes: manifest?.ResolvedNotes);
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

    public async Task<string> DownloadInstallerAsync(string downloadUrl)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri) ||
            !string.Equals(downloadUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Installer download URL must use HTTPS.", nameof(downloadUrl));
        }

        var fileName = Path.GetFileName(downloadUri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "tastile-update.exe";
        }
        else if (!Path.HasExtension(fileName))
        {
            fileName = $"{fileName}.exe";
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"tastile-update-{Guid.NewGuid():N}-{fileName}");
        using var response = await _httpClient.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync();
        await using var destination = File.Create(tempPath);
        await source.CopyToAsync(destination);

        return tempPath;
    }

    private static int CompareVersions(string left, string right)
    {
        // Normalize versions to 4-part format (major.minor.build.revision)
        var normalizedLeft = NormalizeVersion(left);
        var normalizedRight = NormalizeVersion(right);

        if (Version.TryParse(normalizedLeft, out var lv) && Version.TryParse(normalizedRight, out var rv))
        {
            return lv.CompareTo(rv);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersion(string version)
    {
        // Split version string and normalize to 4 parts
        var parts = version.Split('.');
        if (parts.Length >= 4)
        {
            return version;
        }

        // Pad with zeros to get 4 parts
        var normalized = parts.ToList();
        while (normalized.Count < 4)
        {
            normalized.Add("0");
        }

        return string.Join('.', normalized);
    }

    private static string ResolveManifestUrl(string? manifestUrl)
    {
        if (!string.IsNullOrWhiteSpace(manifestUrl))
        {
            return manifestUrl.Trim();
        }

        var runtimeConfigured = RuntimeProfile.ResolveEnvironmentValue("TASTILE_UPDATE_URL");
        return string.IsNullOrWhiteSpace(runtimeConfigured) ? DefaultUpdateEndpoint : runtimeConfigured.Trim();
    }

    private sealed class UpdateManifest
    {
        [JsonPropertyName("latest_version")]
        public string? LatestVersion { get; set; }

        [JsonPropertyName("latest")]
        public string? Latest { get; set; }

        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonPropertyName("release_notes")]
        public string? ReleaseNotes { get; set; }

        public string? ResolvedLatestVersion =>
            string.IsNullOrWhiteSpace(LatestVersion) ? Latest?.Trim() : LatestVersion.Trim();

        public string? ResolvedDownloadUrl => DownloadUrl?.Trim();

        public string? ResolvedNotes =>
            string.IsNullOrWhiteSpace(Notes) ? ReleaseNotes?.Trim() : Notes.Trim();
    }
}
