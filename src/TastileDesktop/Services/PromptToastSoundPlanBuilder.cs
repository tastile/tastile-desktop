namespace TastileDesktop.Services;

public static class PromptToastSoundSources
{
    public const string SystemBeep = "system-beep";
    public const string CustomFile = "custom-file";
}

public static class PromptToastSoundPlaybackModes
{
    public const string FixedCount = "fixed-count";
    public const string UntilPromptResponse = "until-prompt-response";
}

public readonly record struct PromptToastSoundPlan(
    bool Enabled,
    string Source,
    string? FilePath,
    string PlaybackMode,
    int DurationSeconds,
    int RepeatCount,
    int RepeatIntervalSeconds);

public static class PromptToastSoundPlanBuilder
{
    public static PromptToastSoundPlan Create(
        bool enabled,
        string? source,
        string? filePath,
        string? playbackMode,
        int durationSeconds,
        int repeatCount,
        int repeatIntervalSeconds)
    {
        var normalizedSource = NormalizeSource(source);
        var normalizedPlaybackMode = NormalizePlaybackMode(playbackMode);
        var normalizedPath = string.IsNullOrWhiteSpace(filePath) ? null : filePath.Trim();

        if (normalizedSource == PromptToastSoundSources.SystemBeep)
        {
            normalizedPath = null;
        }

        return new PromptToastSoundPlan(
            Enabled: enabled,
            Source: normalizedSource,
            FilePath: normalizedPath,
            PlaybackMode: normalizedPlaybackMode,
            DurationSeconds: Math.Clamp(durationSeconds, 1, 30),
            RepeatCount: Math.Clamp(repeatCount, 1, 10),
            RepeatIntervalSeconds: Math.Clamp(repeatIntervalSeconds, 1, 30));
    }

    public static string NormalizeSource(string? source)
    {
        if (string.Equals(source, PromptToastSoundSources.CustomFile, StringComparison.OrdinalIgnoreCase))
        {
            return PromptToastSoundSources.CustomFile;
        }

        return PromptToastSoundSources.SystemBeep;
    }

    public static string NormalizePlaybackMode(string? playbackMode)
    {
        if (string.Equals(playbackMode, PromptToastSoundPlaybackModes.UntilPromptResponse, StringComparison.OrdinalIgnoreCase))
        {
            return PromptToastSoundPlaybackModes.UntilPromptResponse;
        }

        return PromptToastSoundPlaybackModes.FixedCount;
    }
}
