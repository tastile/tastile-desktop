namespace TastileDesktop.Services;

public static class TileDurationResolver
{
    public static string Resolve(string? semanticRole, int? targetWorkMin, int? targetRestMin)
    {
        var duration = string.Equals(semanticRole, "break", StringComparison.OrdinalIgnoreCase)
            ? targetRestMin
            : targetWorkMin;

        return duration.HasValue && duration.Value > 0
            ? $"{duration.Value}m"
            : "unspecified";
    }
}
