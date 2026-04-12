namespace TastileDesktop.Services;

public sealed record GoogleCalendarIntegrationPresentation(
    string StatusBadge,
    string Headline,
    string Detail,
    string PrimaryActionText,
    string SyncModeLabel,
    string SyncModeDescription,
    string PermissionsSummary,
    string CalendarSummary,
    string LastSyncSummary,
    string SyncHealthSummary,
    string PlanSummary);

public static class GoogleCalendarIntegrationPresentationResolver
{
    public static GoogleCalendarIntegrationPresentation Resolve(
        GoogleCalendarIntegrationResponse integration,
        SyncStatusResponse? syncStatus,
        CalendarSyncPlanPreviewResponse? plan)
    {
        var effectiveMode = plan?.SyncMode ?? integration.SyncMode;
        var effectiveReadPolicy = plan?.ReadPolicy ?? integration.ReadPolicy;
        var effectiveWritePolicy = plan?.WritePolicy ?? integration.WritePolicy;
        var effectiveCalendarId = plan?.SelectedCalendarId ?? integration.SelectedCalendarId;

        return new GoogleCalendarIntegrationPresentation(
            StatusBadge: ResolveStatusBadge(integration, syncStatus),
            Headline: ResolveHeadline(integration),
            Detail: ResolveDetail(integration, effectiveCalendarId),
            PrimaryActionText: integration.Connected ? "Connected" : "Connect Google Calendar",
            SyncModeLabel: ResolveSyncModeLabel(effectiveMode),
            SyncModeDescription: ResolveSyncModeDescription(effectiveMode),
            PermissionsSummary: ResolvePermissionsSummary(integration),
            CalendarSummary: ResolveCalendarSummary(effectiveCalendarId),
            LastSyncSummary: ResolveLastSyncSummary(integration, syncStatus),
            SyncHealthSummary: ResolveSyncHealthSummary(integration, syncStatus),
            PlanSummary: ResolvePlanSummary(effectiveReadPolicy, effectiveWritePolicy));
    }

    public static string ResolveSyncModeLabel(string? syncMode)
        => syncMode switch
        {
            "pull_only" => "Google Calendar -> Tastile",
            "bidirectional" => "Two-way sync",
            _ => "Tastile -> Google Calendar",
        };

    public static string ResolveSyncModeDescription(string? syncMode)
        => syncMode switch
        {
            "pull_only" => "Google Calendar events are read into Tastile so your focus plan can avoid conflicts. Tastile does not write schedule blocks back.",
            "bidirectional" => "Tastile schedule blocks are written to Google Calendar, and Google Calendar events are read back into Tastile so conflicts stay visible.",
            _ => "Tastile schedules are written to Google Calendar. Google Calendar events stay read-only in Tastile.",
        };

    private static string ResolveStatusBadge(GoogleCalendarIntegrationResponse integration, SyncStatusResponse? syncStatus)
    {
        if (!integration.Connected)
        {
            return "Not connected";
        }

        if (syncStatus?.InProgress == true)
        {
            return "Syncing";
        }

        if (!string.IsNullOrWhiteSpace(syncStatus?.LastError))
        {
            return "Needs attention";
        }

        return "Connected";
    }

    private static string ResolveHeadline(GoogleCalendarIntegrationResponse integration)
    {
        if (!integration.Connected)
        {
            return "Connect Google Calendar to keep your plan in one place";
        }

        return string.IsNullOrWhiteSpace(integration.AccountEmail)
            ? "Connected to Google Calendar"
            : $"Connected as {integration.AccountEmail}";
    }

    private static string ResolveDetail(GoogleCalendarIntegrationResponse integration, string? selectedCalendarId)
    {
        if (!integration.Connected)
        {
            return "No Google account is linked yet. Connect once to choose a calendar and decide how Tastile should sync.";
        }

        return $"{ResolveCalendarSummary(selectedCalendarId)} selected. Tastile can {ResolveAccessPhrase(integration)}.";
    }

    private static string ResolvePermissionsSummary(GoogleCalendarIntegrationResponse integration)
    {
        if (!integration.Connected)
        {
            return "Permission details appear after you connect Google Calendar.";
        }

        return ResolveAccessPhrase(integration) switch
        {
            var phrase when phrase.Contains("read your busy time", StringComparison.OrdinalIgnoreCase)
                && phrase.Contains("write Tastile schedule blocks", StringComparison.OrdinalIgnoreCase)
                => "Reads busy events and writes Tastile-owned schedule blocks.",
            var phrase => char.ToUpperInvariant(phrase[0]) + phrase[1..] + ".",
        };
    }

    private static string ResolveCalendarSummary(string? selectedCalendarId)
    {
        if (string.IsNullOrWhiteSpace(selectedCalendarId) || string.Equals(selectedCalendarId, "primary", StringComparison.OrdinalIgnoreCase))
        {
            return "Primary calendar";
        }

        return $"Calendar: {selectedCalendarId}";
    }

    private static string ResolveLastSyncSummary(GoogleCalendarIntegrationResponse integration, SyncStatusResponse? syncStatus)
    {
        if (!integration.Connected)
        {
            return "Last sync: not available until you connect";
        }

        if (syncStatus?.InProgress == true)
        {
            return "Sync is running now...";
        }

        if (TryFormatTimestamp(syncStatus?.LastSuccessAt, out var lastSuccess))
        {
            return $"Last successful sync: {lastSuccess}";
        }

        if (TryFormatTimestamp(integration.LastSyncedAt, out var integrationSync))
        {
            return $"Last successful sync: {integrationSync}";
        }

        return "No sync has completed yet.";
    }

    private static string ResolveSyncHealthSummary(GoogleCalendarIntegrationResponse integration, SyncStatusResponse? syncStatus)
    {
        if (!integration.Connected)
        {
            return "Sync health appears after the first connection.";
        }

        if (!string.IsNullOrWhiteSpace(syncStatus?.LastError))
        {
            return $"Last sync error: {syncStatus.LastError}";
        }

        if (syncStatus?.LastResult is { } lastResult)
        {
            return $"Last run uploaded {lastResult.Uploaded}, downloaded {lastResult.Downloaded}, applied {lastResult.Applied}, failed {lastResult.Failed}.";
        }

        return "No sync activity recorded yet.";
    }

    private static string ResolvePlanSummary(string? readPolicy, string? writePolicy)
    {
        var readSummary = readPolicy switch
        {
            "import_only" => "Read Google events for awareness only.",
            "import_and_block_scheduling" => "Read Google events to protect focus blocks.",
            _ => "Google Calendar events are not read into Tastile.",
        };

        var writeSummary = writePolicy switch
        {
            "all_editable" => "Write back any editable scheduled block to Google Calendar.",
            "tastile_owned_only" => "Write only Tastile-owned schedule blocks back to Google Calendar.",
            _ => "Google Calendar stays read-only from Tastile.",
        };

        return $"{readSummary} {writeSummary}";
    }

    private static string ResolveAccessPhrase(GoogleCalendarIntegrationResponse integration)
    {
        return (integration.CanRead, integration.CanWrite) switch
        {
            (true, true) => "read your busy time and write Tastile schedule blocks",
            (true, false) => "read your busy time but not write anything back",
            (false, true) => "write Tastile schedule blocks but not read your busy time",
            _ => "connect, but it does not currently have calendar read or write access",
        };
    }

    private static bool TryFormatTimestamp(string? value, out string formatted)
    {
        formatted = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || !DateTimeOffset.TryParse(value, out var parsed))
        {
            return false;
        }

        formatted = parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        return true;
    }
}
