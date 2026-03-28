using System.Collections.Generic;
using TastileDesktop.Services;
using Xunit;

namespace TastileDesktop.Tests;

public sealed class RunningTileSelectionTests
{
    [Fact]
    public void SelectMainRunningTileId_PrefersExecutionMainTileOverAlphabeticalFirst()
    {
        var runningTiles = new List<RunningTileSnapshot>
        {
            new("work-a", "A Work"),
            new("break-1", "Break (5min)"),
        };

        var selected = RunningTileSelection.SelectMainRunningTileId(
            runningTiles,
            focusedTileId: null,
            executionMainTileId: "break-1");

        Assert.Equal("break-1", selected);
    }

    [Fact]
    public void SelectMainRunningTileId_FallsBackToFocusedWhenExecutionMainIsMissing()
    {
        var runningTiles = new List<RunningTileSnapshot>
        {
            new("work-a", "A Work"),
            new("work-b", "B Work"),
        };

        var selected = RunningTileSelection.SelectMainRunningTileId(
            runningTiles,
            focusedTileId: "work-b",
            executionMainTileId: "missing");

        Assert.Equal("work-b", selected);
    }
}
