namespace TastileDesktop.Services;

public static class PromptToastSoundSources
{
    public const string SystemBeep = "system-beep";
    public const string CustomFile = "custom-file";
}

public readonly record struct PromptToastSoundPlan(
    bool Enabled,
    string Source,
    string? FilePath,
    int DurationSeconds,
    int RepeatCount);

public static class PromptToastSoundPlanBuilder
{
    public static PromptToastSoundPlan Create(
        bool enabled,
        string? source,
        string? filePath,
        int durationSeconds,
        int repeatCount)
    {
        var normalizedSource = NormalizeSource(source);
        var normalizedPath = string.IsNullOrWhiteSpace(filePath) ? null : filePath.Trim();

        if (normalizedSource == PromptToastSoundSources.SystemBeep)
        {
            normalizedPath = null;
        }

        return new PromptToastSoundPlan(
            Enabled: enabled,
            Source: normalizedSource,
            FilePath: normalizedPath,
            DurationSeconds: Math.Clamp(durationSeconds, 1, 30),
            RepeatCount: Math.Clamp(repeatCount, 1, 10));
    }

    public static string NormalizeSource(string? source)
    {
        if (string.Equals(source, PromptToastSoundSources.CustomFile, StringComparison.OrdinalIgnoreCase))
        {
            return PromptToastSoundSources.CustomFile;
        }

        return PromptToastSoundSources.SystemBeep;
    }
}
