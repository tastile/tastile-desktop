namespace TastileDesktop.Services;

public enum TimelineStatusActionKind
{
    Ignore,
    RequestPrompt
}

public sealed record TimelineStatusActionDecision(
    TimelineStatusActionKind Kind,
    string? TileId);

public static class TimelineStatusActionResolver
{
    public static TimelineStatusActionDecision Resolve(string? tileId, string? lifecycle)
    {
        if (string.IsNullOrWhiteSpace(tileId))
        {
            return new TimelineStatusActionDecision(TimelineStatusActionKind.Ignore, null);
        }

        var normalized = lifecycle?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized == "ready" || normalized == "started")
        {
            return new TimelineStatusActionDecision(TimelineStatusActionKind.RequestPrompt, tileId);
        }

        return new TimelineStatusActionDecision(TimelineStatusActionKind.Ignore, tileId);
    }
}
