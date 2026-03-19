using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TastileDesktop.Services;
using Xunit;

namespace TastileDesktop.Tests;

public sealed class DaemonCompatibilityTests
{
    [Fact]
    public async Task IsCompatibleAsync_ReturnsFalse_WhenHealthIsOkButVersionRouteIsMissing()
    {
        using var client = new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/health")
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            if (request.RequestUri?.AbsolutePath == "/version")
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        };

        var compatible = await DaemonCompatibility.IsCompatibleAsync(client);

        Assert.False(compatible);
    }

    [Fact]
    public async Task IsCompatibleAsync_ReturnsTrue_WhenHealthAndVersionRoutesExist()
    {
        using var client = new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/health")
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            if (request.RequestUri?.AbsolutePath == "/version")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"version\":\"test\"}"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        };

        var compatible = await DaemonCompatibility.IsCompatibleAsync(client);

        Assert.True(compatible);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
