namespace TastileDesktop.Services;

/// <summary>
/// Renders short time labels for tile schedule rows. The label honors the
/// tile's own IANA timezone (carried on <c>TemporalConditions.Tz</c>) so a
/// JST tile displays as "08:00" regardless of where the host machine is.
/// </summary>
public static class TileTimeDisplayResolver
{
    public static string? ResolveNextStartLabel(string? projectedNextStartAt, string? tz)
    {
        var parsed = ParseUtc(projectedNextStartAt);
        if (parsed is null)
        {
            return null;
        }
        return TileTimezoneFormatter.Format(parsed.Value, tz, "MM/dd HH:mm");
    }

    public static string ResolveScheduledTimeDisplay(
        string? fixedStart,
        string? activeStart,
        string? projectedNextStartAt,
        string? tz)
    {
        var start = ParseUtc(fixedStart)
            ?? ParseUtc(activeStart)
            ?? ParseUtc(projectedNextStartAt);
        if (!start.HasValue)
        {
            return string.Empty;
        }
        return $"scheduled {TileTimezoneFormatter.Format(start.Value, tz, "HH:mm")}";
    }

    private static DateTimeOffset? ParseUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (!DateTimeOffset.TryParse(value, out var parsed))
        {
            return null;
        }
        return parsed.ToUniversalTime();
    }
}
