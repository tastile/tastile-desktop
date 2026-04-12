using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class GoogleCalendarIntegrationPresentationResolverTests
{
    [Fact]
    public void Resolve_DisconnectedState_UsesGuidedConnectionCopy()
    {
        var presentation = GoogleCalendarIntegrationPresentationResolver.Resolve(
            new GoogleCalendarIntegrationResponse
            {
                Connected = false,
                SyncMode = "push_only",
                ReadPolicy = "import_and_block_scheduling",
                WritePolicy = "tastile_owned_only",
            },
            syncStatus: null,
            plan: new CalendarSyncPlanPreviewResponse
            {
                SyncMode = "push_only",
                ReadPolicy = "import_and_block_scheduling",
                WritePolicy = "tastile_owned_only",
            });

        Assert.Equal("Not connected", presentation.StatusBadge);
        Assert.Equal("Connect Google Calendar to keep your plan in one place", presentation.Headline);
        Assert.Contains("Google account is linked", presentation.Detail);
        Assert.Equal("Connect Google Calendar", presentation.PrimaryActionText);
        Assert.Equal("Tastile -> Google Calendar", presentation.SyncModeLabel);
        Assert.Contains("Tastile schedules are written", presentation.SyncModeDescription);
        Assert.Equal("Last sync: not available until you connect", presentation.LastSyncSummary);
    }

    [Fact]
    public void Resolve_ConnectedState_UsesHumanReadableSyncAndHealthSummary()
    {
        var presentation = GoogleCalendarIntegrationPresentationResolver.Resolve(
            new GoogleCalendarIntegrationResponse
            {
                Connected = true,
                AccountEmail = "user@example.com",
                SelectedCalendarId = "primary",
                SyncMode = "bidirectional",
                ReadPolicy = "import_and_block_scheduling",
                WritePolicy = "tastile_owned_only",
                CanRead = true,
                CanWrite = true,
            },
            new SyncStatusResponse
            {
                InProgress = false,
                LastSuccessAt = "2026-04-10T03:00:00.000Z",
                LastError = null,
                LastResult = new SyncResultResponse
                {
                    Uploaded = 3,
                    Downloaded = 2,
                    Applied = 5,
                    Failed = 0,
                },
            },
            new CalendarSyncPlanPreviewResponse
            {
                SelectedCalendarId = "primary",
                SyncMode = "bidirectional",
                ReadPolicy = "import_and_block_scheduling",
                WritePolicy = "tastile_owned_only",
            });

        Assert.Equal("Connected", presentation.StatusBadge);
        Assert.Equal("Connected as user@example.com", presentation.Headline);
        Assert.Contains("Primary calendar", presentation.Detail);
        Assert.Equal("Two-way sync", presentation.SyncModeLabel);
        Assert.Contains("Google Calendar events are read back", presentation.SyncModeDescription);
        Assert.Contains("Last successful sync:", presentation.LastSyncSummary);
        Assert.Contains("uploaded 3", presentation.SyncHealthSummary);
        Assert.Contains("downloaded 2", presentation.SyncHealthSummary);
        Assert.Equal("Read Google events to protect focus blocks. Write only Tastile-owned schedule blocks back to Google Calendar.", presentation.PlanSummary);
    }
}
