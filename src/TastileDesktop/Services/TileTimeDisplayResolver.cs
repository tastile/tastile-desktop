namespace TastileDesktop.Services;

public static class TileTimeDisplayResolver
{
    private static DateTimeOffset? ParseLocal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (!DateTimeOffset.TryParse(value, out var parsed))
        {
            return null;
        }
        return parsed.ToLocalTime();
    }

    public static string? ResolveNextStartLabel(string? projectedNextStartAt)
    {
        var local = ParseLocal(projectedNextStartAt);
        return local?.ToString("MM/dd HH:mm");
    }

    public static string ResolveScheduledTimeDisplay(
        string? fixedStart,
        string? activeStart,
        string? projectedNextStartAt)
    {
        var start = ParseLocal(fixedStart) ?? ParseLocal(activeStart) ?? ParseLocal(projectedNextStartAt);
        return start.HasValue ? $"scheduled {start.Value:HH:mm}" : "";
    }
}
