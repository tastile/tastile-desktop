using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class CoreApiClientIntegrationSettingsTests
{
    [Fact]
    public async Task GetIntegrationSettingsAsync_ParsesGoogleCalendarShape()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == "/auth/integrations/settings")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "google_calendar": {
                        "connected": true,
                        "provider_status": "connected",
                        "can_read": true,
                        "can_write": true,
                        "account_email": "user@example.com",
                        "selected_calendar_id": "primary",
                        "sync_mode": "bidirectional",
                        "read_policy": "import_only",
                        "write_policy": "all_editable",
                        "last_synced_at": "2026-03-30T04:00:00.000Z"
                      }
                    }
                    """),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var result = await client.GetIntegrationSettingsAsync();

        Assert.NotNull(result);
        Assert.True(result!.GoogleCalendar.Connected);
        Assert.Equal("user@example.com", result.GoogleCalendar.AccountEmail);
        Assert.Equal("primary", result.GoogleCalendar.SelectedCalendarId);
        Assert.Equal("bidirectional", result.GoogleCalendar.SyncMode);
        Assert.Equal("all_editable", result.GoogleCalendar.WritePolicy);
    }

    [Fact]
    public async Task UpdateGoogleCalendarIntegrationAsync_SendsPatchBody()
    {
        var client = new CoreApiClient(new HttpClient(new StubHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == "/auth/integrations/settings")
            {
                var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                var json = JsonDocument.Parse(body).RootElement;
                var gc = json.GetProperty("google_calendar");
                Assert.True(gc.GetProperty("connected").GetBoolean());
                Assert.Equal("user@example.com", gc.GetProperty("account_email").GetString());
                Assert.Equal("shared-work", gc.GetProperty("selected_calendar_id").GetString());
                Assert.Equal("pull_only", gc.GetProperty("sync_mode").GetString());
                Assert.Equal("import_only", gc.GetProperty("read_policy").GetString());
                Assert.Equal("all_editable", gc.GetProperty("write_policy").GetString());

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "google_calendar": {
                        "connected": true,
                        "provider_status": "connected",
                        "can_read": true,
                        "can_write": true,
                        "account_email": "user@example.com",
                        "selected_calendar_id": "shared-work",
                        "sync_mode": "pull_only",
                        "read_policy": "import_only",
                        "write_policy": "all_editable",
                        "last_synced_at": null
                      }
                    }
                    """),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("http://localhost:3140"),
        });

        var result = await client.UpdateGoogleCalendarIntegrationAsync(
            connected: true,
            accountEmail: "user@example.com",
            selectedCalendarId: "shared-work",
            syncMode: "pull_only",
            readPolicy: "import_only",
            writePolicy: "all_editable");

        Assert.NotNull(result);
        Assert.True(result!.GoogleCalendar.Connected);
        Assert.Equal("shared-work", result.GoogleCalendar.SelectedCalendarId);
        Assert.Equal("pull_only", result.GoogleCalendar.SyncMode);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
