using TastileDesktop.Services;
using TastileDesktop.Models;

namespace TastileDesktop.Tests;

public sealed class CreateTileParityResolverTests
{
    [Fact]
    public void SplitProjectAndTags_SeparatesProjectPrefixFromTags()
    {
        var (project, tags) = CreateTileParityResolver.SplitProjectAndTags(
            ["project:client-app", "urgent", "backend"]);

        Assert.Equal("client-app", project);
        Assert.Equal(["urgent", "backend"], tags);
    }

    [Fact]
    public void SplitProjectAndTags_IgnoresDuplicateTagsAndBlankValues()
    {
        var (project, tags) = CreateTileParityResolver.SplitProjectAndTags(
            [" ", "project:core", "urgent", "URGENT", ""]);

        Assert.Equal("core", project);
        Assert.Single(tags);
        Assert.Equal("urgent", tags[0]);
    }

    [Fact]
    public void BuildRequest_DoesNotForceFixedWindow_WhenDurationIsShorterThanWindow()
    {
        var now = DateTimeOffset.Now;
        var draft = new CreateTileDraft(
            Title: "Breakfast",
            ObjectiveMode: "finish_once",
            UseStartAt: true,
            UseEndAt: true,
            StartAt: now,
            EndAt: now.AddHours(3),
            TileKind: "work",
            WorkHours: 1,
            WorkMinutes: 0,
            DurationManuallyEdited: true,
            BreakSplitsWork: false);

        var request = CreateTileParityResolver.BuildRequest(draft, isJapanese: true);
        Assert.NotNull(request.Temporal);
        Assert.Null(request.Temporal!.FixedStart);
        Assert.Null(request.Temporal.FixedEnd);
        Assert.NotNull(request.Temporal.ActiveStart);
        Assert.NotNull(request.Temporal.ActiveEnd);
    }
}
