using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class AppUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdateAsync_ReturnsUpdate_WhenManifestHasNewerVersion()
    {
        var service = new AppUpdateService(new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri?.AbsoluteUri == "https://updates.example.com/manifest.json")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "latest_version": "9.9.9",
                      "download_url": "https://example.com/tastile-setup.exe",
                      "notes": "Bug fixes"
                    }
                    """),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        })));

        var result = await service.CheckForUpdateAsync(
            manifestUrl: "https://updates.example.com/manifest.json",
            currentVersion: "1.0.0");

        Assert.True(result.HasUpdate);
        Assert.Equal("9.9.9", result.LatestVersion);
        Assert.Equal("https://example.com/tastile-setup.exe", result.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNoUpdate_WhenVersionIsSameOrOlder()
    {
        var service = new AppUpdateService(new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri?.AbsoluteUri == "https://updates.example.com/manifest.json")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "latest_version": "1.0.0",
                      "download_url": "https://example.com/tastile-setup.exe",
                      "notes": "No-op"
                    }
                    """),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        })));

        var result = await service.CheckForUpdateAsync(
            manifestUrl: "https://updates.example.com/manifest.json",
            currentVersion: "1.0.0");

        Assert.False(result.HasUpdate);
    }

    [Fact]
    public void ShouldPromptForUpdate_RespectsIgnoredVersion()
    {
        var service = new AppUpdateService();
        var update = new AppUpdateInfo(
            HasUpdate: true,
            LatestVersion: "2.0.0",
            DownloadUrl: "https://example.com/setup.exe",
            Notes: null);

        Assert.False(service.ShouldPromptForUpdate(update, "2.0.0"));
        Assert.True(service.ShouldPromptForUpdate(update, "1.9.0"));
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNoUpdate_WhenManifestUrlIsBlank()
    {
        var service = new AppUpdateService();

        var result = await service.CheckForUpdateAsync(
            manifestUrl: "",
            currentVersion: "1.0.0");

        Assert.False(result.HasUpdate);
        Assert.Equal("1.0.0", result.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNoUpdate_WhenManifestPayloadIsInvalid()
    {
        var service = new AppUpdateService(new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri?.AbsoluteUri == "https://updates.example.com/manifest.json")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{ "latest_version": """),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        })));

        var result = await service.CheckForUpdateAsync(
            manifestUrl: "https://updates.example.com/manifest.json",
            currentVersion: "1.0.0");

        Assert.False(result.HasUpdate);
        Assert.Equal("1.0.0", result.LatestVersion);
        Assert.Equal(string.Empty, result.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNoUpdate_WhenRequestFails()
    {
        var service = new AppUpdateService(new HttpClient(new ThrowingHandler(new HttpRequestException("boom"))));

        var result = await service.CheckForUpdateAsync(
            manifestUrl: "https://updates.example.com/manifest.json",
            currentVersion: "1.0.0");

        Assert.False(result.HasUpdate);
        Assert.Equal("1.0.0", result.LatestVersion);
        Assert.Equal(string.Empty, result.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNoUpdate_WhenManifestDownloadUrlIsNotHttps()
    {
        var service = new AppUpdateService(new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri?.AbsoluteUri == "https://updates.example.com/manifest.json")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "latest_version": "9.9.9",
                      "download_url": "ms-appinstaller:?source=https://example.com/tastile.appinstaller",
                      "notes": "Bug fixes"
                    }
                    """),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        })));

        var result = await service.CheckForUpdateAsync(
            manifestUrl: "https://updates.example.com/manifest.json",
            currentVersion: "1.0.0");

        Assert.False(result.HasUpdate);
        Assert.Equal(string.Empty, result.DownloadUrl);
    }

    [Fact]
    public async Task DownloadInstallerAsync_SavesHttpsPayloadToExecutableFile()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var service = new AppUpdateService(new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri?.AbsoluteUri == "https://updates.example.com/tastile-0.3.0-setup.exe")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(payload),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        })));

        var path = await service.DownloadInstallerAsync("https://updates.example.com/tastile-0.3.0-setup.exe");

        try
        {
            Assert.EndsWith(".exe", path, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(path));
            Assert.Equal(payload, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }
}
