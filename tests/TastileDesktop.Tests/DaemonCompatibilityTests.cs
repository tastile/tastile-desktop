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
                    Content = new StringContent("{\"version\":\"test\",\"app\":\"tastile-daemon\",\"binary_sha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        };

        var compatible = await DaemonCompatibility.IsCompatibleAsync(
            client,
            expectedBinarySha256: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        Assert.True(compatible);
    }

    [Fact]
    public async Task IsCompatibleAsync_ReturnsFalse_WhenVersionPayloadMissesDaemonApp()
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
                    Content = new StringContent("{\"version\":\"test\",\"binary_sha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}"),
                };
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
    public async Task IsCompatibleAsync_ReturnsFalse_WhenVersionPayloadMissesBinarySha()
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
                    Content = new StringContent("{\"version\":\"test\",\"app\":\"tastile-daemon\"}"),
                };
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
    public async Task IsCompatibleAsync_ReturnsFalse_WhenBinaryShaDoesNotMatchExpected()
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
                    Content = new StringContent("{\"version\":\"test\",\"app\":\"tastile-daemon\",\"binary_sha256\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"}"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        };

        var compatible = await DaemonCompatibility.IsCompatibleAsync(
            client,
            expectedBinarySha256: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        Assert.False(compatible);
    }

    [Fact]
    public async Task IsCompatibleAsync_ReturnsFalse_WhenExpectedShaCannotBeResolved()
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
                    Content = new StringContent("{\"version\":\"test\",\"app\":\"tastile-daemon\",\"binary_sha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        };

        var compatible = await DaemonCompatibility.IsCompatibleAsync(
            client,
            daemonBinaryPath: Path.Combine(Path.GetTempPath(), $"missing-daemon-{Guid.NewGuid():N}.exe"));

        Assert.False(compatible);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
