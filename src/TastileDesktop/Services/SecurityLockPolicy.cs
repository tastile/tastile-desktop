namespace TastileDesktop.Services;

public static class SecurityLockPolicy
{
    public static bool ShouldRequireUnlock(
        bool enabled,
        int timeoutMinutes,
        string? lastClosedAtUtc,
        DateTimeOffset nowUtc,
        bool isStartupLaunch)
    {
        if (!enabled || isStartupLaunch)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(lastClosedAtUtc))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(lastClosedAtUtc, out var lastClosedAt))
        {
            return false;
        }

        var timeout = TimeSpan.FromMinutes(Math.Clamp(timeoutMinutes, 1, 240));
        return nowUtc.ToUniversalTime() - lastClosedAt.ToUniversalTime() >= timeout;
    }
}
