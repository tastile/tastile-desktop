using Windows.Security.Credentials.UI;

namespace TastileDesktop.Services;

public sealed class SecurityLockService
{
    public async Task<bool> RequestUnlockAsync(TastileSettings settings, bool isStartupLaunch)
    {
        if (!SecurityLockPolicy.ShouldRequireUnlock(
                settings.SecurityLockEnabled,
                settings.SecurityLockTimeoutMinutes,
                settings.SecurityLockLastClosedAtUtc,
                DateTimeOffset.UtcNow,
                isStartupLaunch))
        {
            return true;
        }

        var availability = await UserConsentVerifier.CheckAvailabilityAsync();
        if (availability != UserConsentVerifierAvailability.Available)
        {
            App.DebugLog($"Security lock skipped: Windows user verification unavailable ({availability}).");
            return true;
        }

        var result = await UserConsentVerifier.RequestVerificationAsync("Unlock Tastile");
        return result == UserConsentVerificationResult.Verified;
    }
}
