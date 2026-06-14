using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class SecurityLockPolicyTests
{
    [Fact]
    public void ShouldRequireUnlock_ReturnsTrue_WhenElapsedPastTimeout()
    {
        var now = DateTimeOffset.Parse("2026-06-14T00:20:00Z");

        var require = SecurityLockPolicy.ShouldRequireUnlock(
            enabled: true,
            timeoutMinutes: 10,
            lastClosedAtUtc: "2026-06-14T00:09:59Z",
            nowUtc: now,
            isStartupLaunch: false);

        Assert.True(require);
    }

    [Fact]
    public void ShouldRequireUnlock_SkipsStartupLaunch()
    {
        var now = DateTimeOffset.Parse("2026-06-14T00:20:00Z");

        var require = SecurityLockPolicy.ShouldRequireUnlock(
            enabled: true,
            timeoutMinutes: 10,
            lastClosedAtUtc: "2026-06-14T00:00:00Z",
            nowUtc: now,
            isStartupLaunch: true);

        Assert.False(require);
    }
}
