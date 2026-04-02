namespace TastileDesktop.Services;

public static class CountdownTextResolver
{
    public static string Resolve(string? mainTileEndsAt, DateTimeOffset? nextActionableStartAt, DateTimeOffset nowUtc)
    {
        if (!string.IsNullOrWhiteSpace(mainTileEndsAt)
            && DateTimeOffset.TryParse(mainTileEndsAt, out var endsAt))
        {
            return Format(endsAt - nowUtc);
        }

        if (nextActionableStartAt.HasValue)
        {
            return Format(nextActionableStartAt.Value - nowUtc);
        }

        return "00:00";
    }

    public static string Format(TimeSpan remaining)
    {
        if (remaining.TotalSeconds <= 0) return "00:00";
        return remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}"
            : $"{remaining.Minutes:00}:{remaining.Seconds:00}";
    }
}
