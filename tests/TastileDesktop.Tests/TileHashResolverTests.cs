using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class TileHashResolverTests
{
    private static TileView NewTile(string id, string? projected)
    {
        return new TileView(
            Id: id,
            Title: "Tile " + id,
            Lifecycle: "ready",
            NextAction: null,
            DoneDefinition: null,
            WorkedMinutes: 0,
            BreakMinutes: 0,
            SemanticRole: "work",
            Labels: null,
            ObjectiveMode: null,
            TargetWorkMin: null,
            TargetRestMin: null,
            DoneRule: null,
            ResumeNote: null,
            ProjectedNextStartAt: projected,
            Temporal: new TemporalConditions(null, null, null, null, null, null),
            Interruption: null,
            Automation: null,
            Recurrence: null,
            Objective: null);
    }

    [Fact]
    public void Build_ChangesWhenProjectedNextStartChanges()
    {
        var a = new TilesResponse(
            Tiles: [NewTile("t1", "2026-04-02T05:00:00Z")],
            NextActionableTileId: null,
            NextActionableStartAt: null);
        var b = new TilesResponse(
            Tiles: [NewTile("t1", "2026-04-02T05:05:00Z")],
            NextActionableTileId: null,
            NextActionableStartAt: null);

        var hashA = TileHashResolver.Build(a);
        var hashB = TileHashResolver.Build(b);

        Assert.NotEqual(hashA, hashB);
    }
}
