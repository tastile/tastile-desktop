using System.Threading.Tasks;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class CoreApiClientIntegrationSettingsTests
{
    [Fact]
    public async Task GetIntegrationSettingsAsync_NotSupportedOnV1()
    {
        var client = new CoreApiClient(baseUrl: "http://localhost:3140");

        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetIntegrationSettingsAsync());
    }

    [Fact]
    public async Task UpdateGoogleCalendarIntegrationAsync_NotSupportedOnV1()
    {
        var client = new CoreApiClient(baseUrl: "http://localhost:3140");

        await Assert.ThrowsAsync<NotSupportedException>(() => client.UpdateGoogleCalendarIntegrationAsync(connected: true));
    }

    [Fact]
    public async Task GetCalendarSyncPlanPreviewAsync_NotSupportedOnV1()
    {
        var client = new CoreApiClient(baseUrl: "http://localhost:3140");

        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetCalendarSyncPlanPreviewAsync());
    }
}