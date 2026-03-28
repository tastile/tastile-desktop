namespace TastileDesktop.Services;

public sealed record RunningTileSnapshot(string Id, string Title);

public static class RunningTileSelection
{
    public static string? SelectMainRunningTileId(
        IReadOnlyList<RunningTileSnapshot> runningTiles,
        string? focusedTileId,
        string? executionMainTileId)
    {
        if (runningTiles.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(executionMainTileId)
            && runningTiles.Any(tile => string.Equals(tile.Id, executionMainTileId, StringComparison.OrdinalIgnoreCase)))
        {
            return runningTiles
                .First(tile => string.Equals(tile.Id, executionMainTileId, StringComparison.OrdinalIgnoreCase))
                .Id;
        }

        if (!string.IsNullOrWhiteSpace(focusedTileId)
            && runningTiles.Any(tile => string.Equals(tile.Id, focusedTileId, StringComparison.OrdinalIgnoreCase)))
        {
            return runningTiles
                .First(tile => string.Equals(tile.Id, focusedTileId, StringComparison.OrdinalIgnoreCase))
                .Id;
        }

        return runningTiles[0].Id;
    }
}
