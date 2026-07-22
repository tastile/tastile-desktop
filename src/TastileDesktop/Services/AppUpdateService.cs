using System.Net.Http.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace TastileDesktop.Services;

public sealed record AppUpdateInfo(
    bool HasUpdate,
    string LatestVersion,
    string DownloadUrl,
    string Sha256,
    string? Notes);

public sealed class AppUpdateService
{
    private const string UpdateBaseUrlEnvVar = "TASTILE_DESKTOP_UPDATE_BASE_URL";
    private static readonly string[] SilentInstallerArguments =
    [
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/CLOSEAPPLICATIONS",
        "/RESTARTAPPLICATIONS",
    ];

    // Resolve once at static init so missing env vars fail-fast on first access.
    private static readonly string DefaultUpdateEndpoint = ResolveDefaultUpdateEndpoint();

    private static string ResolveDefaultUpdateEndpoint()
    {
        var raw = Environment.GetEnvironmentVariable(UpdateBaseUrlEnvVar)?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return BuildManifestUrl("https://download.tastile.app");
        }

        return BuildManifestUrl(raw);
    }

    private static string BuildManifestUrl(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        return $"{trimmed}/updates/desktop/manifest.json";
    }

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
            var sha256 = manifest?.ResolvedSha256 ?? string.Empty;
            if (string.IsNullOrWhiteSpace(latestVersion) ||
                string.IsNullOrWhiteSpace(downloadUrl) ||
                !IsSha256Hex(sha256))
            {
                return new AppUpdateInfo(false, currentVersion, string.Empty, string.Empty, null);
            }

            var hasUpdate = CompareVersions(latestVersion, currentVersion) > 0;
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri) ||
                !string.Equals(downloadUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return new AppUpdateInfo(false, currentVersion, string.Empty, string.Empty, null);
            }

            return new AppUpdateInfo(
                HasUpdate: hasUpdate,
                LatestVersion: latestVersion,
                DownloadUrl: downloadUri.ToString(),
                Sha256: sha256,
                Notes: manifest?.ResolvedNotes);
        }
        catch (HttpRequestException)
        {
            return new AppUpdateInfo(false, currentVersion, string.Empty, string.Empty, null);
        }
        catch (NotSupportedException)
        {
            return new AppUpdateInfo(false, currentVersion, string.Empty, string.Empty, null);
        }
        catch (System.Text.Json.JsonException)
        {
            return new AppUpdateInfo(false, currentVersion, string.Empty, string.Empty, null);
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

    public async Task<string> DownloadInstallerAsync(string downloadUrl, string expectedSha256)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri) ||
            !string.Equals(downloadUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Installer download URL must use HTTPS.", nameof(downloadUrl));
        }
        if (!IsSha256Hex(expectedSha256))
        {
            throw new ArgumentException("Installer SHA-256 is required.", nameof(expectedSha256));
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

        await using (var source = await response.Content.ReadAsStreamAsync())
        await using (var destination = File.Create(tempPath))
        {
            await source.CopyToAsync(destination);
        }

        string actualSha256;
        await using (var downloaded = File.OpenRead(tempPath))
        using (var sha = SHA256.Create())
        {
            actualSha256 = Convert.ToHexString(await sha.ComputeHashAsync(downloaded));
        }
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(tempPath);
            throw new InvalidOperationException("Installer SHA-256 verification failed.");
        }

        return tempPath;
    }

    public static ProcessStartInfo CreateSilentInstallerStartInfo(string installerPath)
    {
        if (string.IsNullOrWhiteSpace(installerPath))
        {
            throw new ArgumentException("Installer path is required.", nameof(installerPath));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true,
        };

        foreach (var argument in SilentInstallerArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    public static Process StartSilentInstaller(string installerPath)
    {
        return Process.Start(CreateSilentInstallerStartInfo(installerPath)) ??
            throw new InvalidOperationException("Failed to start the update installer.");
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
            var configuredUrl = manifestUrl.Trim();
            if (IsLegacyVersionEndpoint(configuredUrl))
            {
                return DefaultUpdateEndpoint;
            }

            return configuredUrl;
        }

        var runtimeConfigured = Environment.GetEnvironmentVariable("TASTILE_UPDATE_URL");
        if (string.IsNullOrWhiteSpace(runtimeConfigured))
        {
            return DefaultUpdateEndpoint;
        }

        var runtimeUrl = runtimeConfigured.Trim();
        return IsLegacyVersionEndpoint(runtimeUrl) ? DefaultUpdateEndpoint : runtimeUrl;
    }

    private static bool IsLegacyVersionEndpoint(string url)
    {
        return url.Contains("/api/version", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSha256Hex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
        {
            return false;
        }

        return value.All(c => Uri.IsHexDigit(c));
    }

    private sealed class UpdateManifest
    {
        [JsonPropertyName("latest_version")]
        public string? LatestVersion { get; set; }

        [JsonPropertyName("latest")]
        public string? Latest { get; set; }

        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonPropertyName("release_notes")]
        public string? ReleaseNotes { get; set; }

        public string? ResolvedLatestVersion =>
            string.IsNullOrWhiteSpace(LatestVersion) ? Latest?.Trim() : LatestVersion.Trim();

        public string? ResolvedDownloadUrl => DownloadUrl?.Trim();

        public string? ResolvedSha256 => Sha256?.Trim();

        public string? ResolvedNotes =>
            string.IsNullOrWhiteSpace(Notes) ? ReleaseNotes?.Trim() : Notes.Trim();
    }
}
