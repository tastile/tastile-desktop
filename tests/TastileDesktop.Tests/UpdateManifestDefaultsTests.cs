using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class UpdateManifestDefaultsTests
{
    [Fact]
    public async Task CheckForUpdateAsync_UsesDefaultFeed_WhenManifestUrlIsBlank()
    {
        var service = new AppUpdateService(new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri?.AbsoluteUri == "https://tastile.app/api/version")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "latest": "9.9.9",
                      "download_url": "https://cdn.example.com/tastile-desktop-9.9.9.exe",
                      "release_notes": "Latest desktop build"
                    }
                    """),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        })));

        var result = await service.CheckForUpdateAsync(
            manifestUrl: "",
            currentVersion: "1.0.0");

        Assert.True(result.HasUpdate);
        Assert.Equal("9.9.9", result.LatestVersion);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
