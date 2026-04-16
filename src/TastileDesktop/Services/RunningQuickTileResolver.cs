using TastileDesktop.Models;
using TastileDesktop.ViewModels;

namespace TastileDesktop.Services;

public static class RunningQuickTileResolver
{
    public static IReadOnlyList<TileListItem> Resolve(
        IReadOnlyList<TileListItem> allTiles,
        IReadOnlyList<TileView>? executionTilesInProgress)
    {
        if (executionTilesInProgress is { Count: > 0 })
        {
            return executionTilesInProgress
                .Select(tile =>
                {
                    var existing = allTiles.FirstOrDefault(item =>
                        string.Equals(item.Id, tile.Id, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.Lifecycle = "Started";
                        return existing;
                    }

                    var mapped = TileListItemMapper.Map(tile);
                    mapped.Lifecycle = "Started";
                    return mapped;
                })
                .ToList();
        }

        return allTiles
            .Where(tile => tile.Lifecycle.Equals("Started", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
