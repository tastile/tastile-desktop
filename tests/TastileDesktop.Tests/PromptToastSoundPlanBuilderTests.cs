using TastileDesktop.Services;
namespace TastileDesktop.Tests;

public sealed class PromptToastSoundPlanBuilderTests
{
    [Fact]
    public void Create_UsesSystemBeepDefaults_WhenInputsAreInvalid()
    {
        var plan = PromptToastSoundPlanBuilder.Create(
            enabled: true,
            source: "invalid",
            filePath: " ",
            playbackMode: "invalid",
            durationSeconds: 0,
            repeatCount: 0,
            repeatIntervalSeconds: 0);

        Assert.True(plan.Enabled);
        Assert.Equal(PromptToastSoundSources.SystemBeep, plan.Source);
        Assert.Equal(PromptToastSoundPlaybackModes.FixedCount, plan.PlaybackMode);
        Assert.Null(plan.FilePath);
        Assert.Equal(1, plan.DurationSeconds);
        Assert.Equal(1, plan.RepeatCount);
        Assert.Equal(1, plan.RepeatIntervalSeconds);
    }

    [Fact]
    public void Create_NormalizesCustomFilePath_AndClampsRanges()
    {
        var plan = PromptToastSoundPlanBuilder.Create(
            enabled: true,
            source: PromptToastSoundSources.CustomFile,
            filePath: " C:\\temp\\sound.mp3 ",
            playbackMode: PromptToastSoundPlaybackModes.FixedCount,
            durationSeconds: 99,
            repeatCount: 99,
            repeatIntervalSeconds: 99);

        Assert.Equal(PromptToastSoundSources.CustomFile, plan.Source);
        Assert.Equal("C:\\temp\\sound.mp3", plan.FilePath);
        Assert.Equal(PromptToastSoundPlaybackModes.FixedCount, plan.PlaybackMode);
        Assert.Equal(30, plan.DurationSeconds);
        Assert.Equal(10, plan.RepeatCount);
        Assert.Equal(30, plan.RepeatIntervalSeconds);
    }

    [Fact]
    public void Create_NormalizesUntilResponseMode()
    {
        var plan = PromptToastSoundPlanBuilder.Create(
            enabled: true,
            source: PromptToastSoundSources.SystemBeep,
            filePath: null,
            playbackMode: PromptToastSoundPlaybackModes.UntilPromptResponse,
            durationSeconds: 2,
            repeatCount: 3,
            repeatIntervalSeconds: 5);

        Assert.Equal(PromptToastSoundPlaybackModes.UntilPromptResponse, plan.PlaybackMode);
    }
}
