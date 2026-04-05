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
            durationSeconds: 0,
            repeatCount: 0);

        Assert.True(plan.Enabled);
        Assert.Equal(PromptToastSoundSources.SystemBeep, plan.Source);
        Assert.Null(plan.FilePath);
        Assert.Equal(1, plan.DurationSeconds);
        Assert.Equal(1, plan.RepeatCount);
    }

    [Fact]
    public void Create_NormalizesCustomFilePath_AndClampsRanges()
    {
        var plan = PromptToastSoundPlanBuilder.Create(
            enabled: true,
            source: PromptToastSoundSources.CustomFile,
            filePath: " C:\\temp\\sound.mp3 ",
            durationSeconds: 99,
            repeatCount: 99);

        Assert.Equal(PromptToastSoundSources.CustomFile, plan.Source);
        Assert.Equal("C:\\temp\\sound.mp3", plan.FilePath);
        Assert.Equal(30, plan.DurationSeconds);
        Assert.Equal(10, plan.RepeatCount);
    }
}
