using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class PollingServiceTimelineDiffTests
{
    [Fact]
    public void HasTimelineChanged_DetectsTitleOnlyUpdate()
    {
        var oldTimeline = new TimelineTodayResponse(
            Items:
            [
                new TimelineItemView(
                    Kind: "scheduled",
                    TileId: "tile-1",
                    SemanticRole: "work",
                    Title: "Before",
                    StartedAt: "2026-04-11T01:00:00Z",
                    EndedAt: "2026-04-11T01:30:00Z",
                    DurationMin: 30,
                    IsActive: false)
            ],
            RangeStart: "2026-04-11T00:00:00Z",
            RangeEnd: "2026-04-12T00:00:00Z");

        var newTimeline = oldTimeline with
        {
            Items =
            [
                oldTimeline.Items[0] with { Title = "After" }
            ]
        };

        var changed = TimelineDiffResolver.HasTimelineChanged(oldTimeline, newTimeline);

        Assert.True(changed, "title update must trigger timeline refresh");
    }
}
