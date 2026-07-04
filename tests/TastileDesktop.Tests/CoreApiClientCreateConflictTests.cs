using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class CoreApiClientCreateConflictTests
{
    // v1 CreateTilePayload requires typed TileKind (i16) + PlanRole (i16).
    // The existing CreateTileRequest carries v0-shaped free-form fields
    // (title/next_action/done_definition/temporal/objective/...). The v1
    // body cannot be fabricated safely without a v1-shaped DTO at the UI
    // boundary, so the desktop surfaces the gap explicitly via
    // NotSupportedException instead of posting a body the server will 400.

    [Fact]
    public async Task CreateTileAsync_TypedRequest_ThrowsNotSupportedOnV1()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => client.CreateTileAsync(new CreateTileRequest(
            Title: "Fixed",
            NextAction: null,
            DoneDefinition: null,
            Temporal: null,
            Objective: null,
            Interruption: null,
            Automation: null,
            Annotation: null,
            ConflictResolution: null)));
        Assert.Contains("CreateTileAsync", ex.Message);
    }

    [Fact]
    public async Task CreateTileAsync_StringOverload_ThrowsNotSupportedOnV1()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => client.CreateTileAsync(title: "Fixed", nextAction: null, doneDefinition: null));
        Assert.Contains("CreateTileAsync", ex.Message);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
