using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class CoreApiClientV1RouteTests
{
    [Fact]
    public async Task GetTilesAsync_UsesV1ListTilesRoute()
    {
        string? capturedPath = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "tiles": [] }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.GetTilesAsync();

        Assert.Equal("/v1/tiles", capturedPath);
    }

    [Fact]
    public async Task GetTileByIdAsync_UsesV1ReadTileRoute()
    {
        string? capturedPath = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "id": "t-1" }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.GetTileByIdAsync("t-1");

        Assert.Equal("/v1/tiles/t-1", capturedPath);
    }

    [Fact]
    public async Task GetEditableTileByIdAsync_UsesV1EditableRoute()
    {
        string? capturedPath = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "id": "t-1" }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.GetEditableTileByIdAsync("t-1");

        Assert.Equal("/v1/tiles/t-1/editable", capturedPath);
    }

    [Fact]
    public async Task GetActiveTileAsync_UsesV1ActiveTileRoute()
    {
        string? capturedPath = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "tile": null }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.GetActiveTileAsync();

        Assert.Equal("/v1/active-tile", capturedPath);
    }

    [Fact]
    public async Task GetPendingPromptAsync_UsesV1PromptsPendingRoute()
    {
        string? capturedPath = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "prompt": null }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.GetPendingPromptAsync();

        Assert.Equal("/v1/prompts/pending", capturedPath);
    }

    [Fact]
    public async Task GetTodayTimelineAsync_UsesV1TimelineTodayRoute()
    {
        string? capturedPath = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "items": [], "range_start": "2026-01-01T00:00:00Z", "range_end": "2026-01-02T00:00:00Z" }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.GetTodayTimelineAsync();

        Assert.Equal("/v1/timeline/today", capturedPath);
    }

    [Fact]
    public async Task GetTileQuotaAsync_UsesV1QuotaTilesRoute()
    {
        string? capturedPath = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "limit": 100, "used": 0 }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.GetTileQuotaAsync();

        Assert.Equal("/v1/quota/tiles", capturedPath);
    }

    [Fact]
    public async Task DeleteTileAsync_UsesV1TileDeleteRoute()
    {
        string? capturedMethod = null;
        string? capturedPath = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedMethod = request.Method.Method;
            capturedPath = request.RequestUri?.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "ok": true, "events": [], "tile_id": null }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.DeleteTileAsync("tile-3");

        Assert.Equal("DELETE", capturedMethod);
        Assert.Equal("/v1/tiles/tile-3", capturedPath);
    }

    [Fact]
    public async Task RequestPromptAsync_ThrowsNotSupportedOnV1_BecauseV1PromptCreateBodyShapeDiffers()
    {
        // v1 create_prompt expects { kind, payload }, not a tile_id-only body.
        // Surfacing as NotSupportedException is preferable to posting a body
        // the server will reject.
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => client.RequestPromptAsync("tile-2"));
        Assert.Contains("RequestPromptAsync", ex.Message);
    }

    [Fact]
    public async Task StartTileAsync_ThrowsNotSupportedOnV1_BecauseStartPayloadNeedsPlanFields()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => client.StartTileAsync("tile-7"));
        Assert.Contains("StartTileAsync", ex.Message);
    }

    [Fact]
    public async Task DeferTileAsync_ThrowsNotSupportedOnV1_BecauseLifecycleNeedsNumericState()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => client.DeferTileAsync("tile-9", minutes: 30));
        Assert.Contains("DeferTileAsync", ex.Message);
    }

    [Fact]
    public async Task AttachMemoAsync_UsesV1TileMemosRoute()
    {
        string? capturedPath = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "ok": true, "events": [], "tile_id": null }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.AttachMemoAsync("tile-4", text: "memo text");

        Assert.Equal("/v1/tiles/tile-4/memos", capturedPath);
    }

    [Fact]
    public async Task SignOutAsync_UsesV1AuthSignoutRoute()
    {
        string? capturedMethod = null;
        string? capturedPath = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedMethod = request.Method.Method;
            capturedPath = request.RequestUri?.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        await client.SignOutAsync();

        Assert.Equal("POST", capturedMethod);
        Assert.Equal("/v1/auth/signout", capturedPath);
    }

    [Fact]
    public async Task CheckHealthAsync_UsesV1HealthRoute()
    {
        string? capturedPath = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var ok = await client.CheckHealthAsync();

        Assert.True(ok);
        Assert.Equal("/v1/health", capturedPath);
    }

    [Fact]
    public async Task GetTilesInProgressAsync_ThrowsNotSupported()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetTilesInProgressAsync());
    }

    [Fact]
    public async Task GetExecutionViewAsync_WithoutExecutionId_ThrowsNotSupported()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetExecutionViewAsync());
    }

    [Fact]
    public async Task GetExecutionAsync_WithoutExecutionId_ThrowsNotSupported()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetExecutionAsync());
    }

    [Fact]
    public async Task StartBreakAsync_ThrowsNotSupported()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        await Assert.ThrowsAsync<NotSupportedException>(() => client.StartBreakAsync(5));
    }

    [Fact]
    public async Task EndBreakAsync_ThrowsNotSupported()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        await Assert.ThrowsAsync<NotSupportedException>(() => client.EndBreakAsync());
    }

    [Fact]
    public async Task ExtendTileAsync_ThrowsNotSupported()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        await Assert.ThrowsAsync<NotSupportedException>(() => client.ExtendTileAsync(15));
    }

    [Fact]
    public async Task DebugTokenAsync_ThrowsNotSupported()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        await Assert.ThrowsAsync<NotSupportedException>(() => client.DebugTokenAsync());
    }

    // ----- v1 CommandEnvelope body-shape assertions (v1/14 §1) -----

    [Fact]
    public async Task DeleteTileAsync_SendsCommandEnvelopeWithArchivePayload()
    {
        // archive_tile requires { idempotency_key, expected_revision,
        // occurred_at, payload: { tile_id } }; the previous
        // implementation sent no body at all (the v0 endpoint didn't
        // need one).
        string? capturedMethod = null;
        string? capturedPath = null;
        string? capturedBody = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedMethod = request.Method.Method;
            capturedPath = request.RequestUri?.AbsolutePath;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "ok": true, "events": [], "tile_id": null }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.DeleteTileAsync("tile-3");

        Assert.Equal("DELETE", capturedMethod);
        Assert.Equal("/v1/tiles/tile-3", capturedPath);
        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        // Envelope surface
        Assert.True(root.TryGetProperty("expected_revision", out var er));
        Assert.Equal(JsonValueKind.Null, er.ValueKind);
        Assert.True(root.TryGetProperty("idempotency_key", out var idem));
        Assert.NotEqual(JsonValueKind.Null, idem.ValueKind);
        Assert.True(Guid.TryParse(idem.GetString(), out _), "idempotency_key must be a parseable UUID");
        Assert.True(root.TryGetProperty("occurred_at", out var occurred));
        Assert.Equal(JsonValueKind.Null, occurred.ValueKind);
        // Inner payload must contain the tile_id we passed.
        var payload = root.GetProperty("payload");
        Assert.Equal("tile-3", payload.GetProperty("tile_id").GetString());
    }

    [Fact]
    public async Task AttachMemoAsync_SendsCommandEnvelopeWithBodyField()
    {
        // attach_memo expects { idempotency_key, expected_revision,
        // occurred_at, payload: { tile_id, body } }. The desktop's
        // legacy 'text' field is mapped to the v1 'body' field; the
        // legacy 'memo_kind' is dropped and triggers NotSupported
        // when supplied (see AttachMemoAsync_WithMemoKind_ThrowsNotSupported).
        string? capturedPath = null;
        string? capturedBody = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "ok": true, "events": [], "tile_id": null }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.AttachMemoAsync("tile-4", text: "memo text");

        Assert.Equal("/v1/tiles/tile-4/memos", capturedPath);
        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("expected_revision", out var er));
        Assert.Equal(JsonValueKind.Null, er.ValueKind);
        Assert.True(root.TryGetProperty("idempotency_key", out var idem));
        Assert.True(Guid.TryParse(idem.GetString(), out _));
        var payload = root.GetProperty("payload");
        Assert.Equal("tile-4", payload.GetProperty("tile_id").GetString());
        Assert.Equal("memo text", payload.GetProperty("body").GetString());
        Assert.False(payload.TryGetProperty("memo_kind", out _),
            "v1 AttachMemoPayload has no memo_kind; the desktop must not send it.");
    }

    [Fact]
    public async Task AttachMemoAsync_WithMemoKind_ThrowsNotSupported()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => client.AttachMemoAsync("tile-5", text: "x", memoKind: "scratch"));
        Assert.Contains("memo_kind", ex.Message);
    }

    [Fact]
    public async Task AttachMemoAsync_WithoutTileId_ThrowsNotSupported()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        await Assert.ThrowsAsync<NotSupportedException>(() => client.AttachMemoAsync(null, text: "x"));
    }

    [Fact]
    public async Task RespondStartupRecoveryPromptAsync_DoesNotWrapInCommandEnvelope()
    {
        // /v1/prompts/startup-recovery accepts raw JSON (no CommandEnvelope).
        // Verify the body shape stays as-is so the server keeps parsing it.
        string? capturedPath = null;
        string? capturedBody = null;
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "ok": true, "events": [], "tile_id": null }"""),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        _ = await client.RespondStartupRecoveryPromptAsync(
            promptId: "p-1",
            tileId: "t-1",
            actionId: "CONFIRM_CONTINUE");

        Assert.Equal("/v1/prompts/startup-recovery", capturedPath);
        Assert.NotNull(capturedBody);
        // The body must NOT carry the v1 envelope keys, otherwise the
        // server-side freeform `serde_json::Value` parser would still
        // accept it but it would be misleading for future readers.
        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        Assert.False(root.TryGetProperty("payload", out _),
            "/v1/prompts/startup-recovery does not use the CommandEnvelope; no top-level 'payload'.");
        Assert.False(root.TryGetProperty("idempotency_key", out _),
            "/v1/prompts/startup-recovery does not use the CommandEnvelope; no top-level 'idempotency_key'.");
        Assert.Equal("p-1", root.GetProperty("prompt_id").GetString());
        Assert.Equal("t-1", root.GetProperty("tile_id").GetString());
        Assert.Equal("CONFIRM_CONTINUE", root.GetProperty("action_id").GetString());
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}