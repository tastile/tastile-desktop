namespace TastileDesktop.Services;

/// <summary>
/// Per-tile timezone rendering. The desktop client is a presentation layer
/// over the timezone-agnostic API; each tile carries its own IANA display
/// timezone (e.g. "Asia/Tokyo"). We MUST honor that timezone when rendering
/// tile times and MUST NOT fall back to the machine's local timezone as a
/// substitute.
///
/// On Windows the .NET runtime knows Windows tz ids ("Tokyo Standard Time"),
/// not IANA ids. The runtime's <see cref="TimeZoneInfo.TryConvertIanaIdToWindowsId"/>
/// bridge (added in .NET 6) handles the IANA → Windows id translation. We
/// cache the resolved <see cref="TimeZoneInfo"/> per IANA id so we don't pay
/// the lookup on every paint.
/// </summary>
public static class TileTimezoneFormatter
{
    private static readonly Dictionary<string, TimeZoneInfo> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Format a UTC <see cref="DateTimeOffset"/> as the wall-clock string in
    /// the given IANA timezone. Returns the local-time formatting if tz is
    /// null/unknown.
    /// </summary>
    public static string Format(
        DateTimeOffset utcInstant,
        string? tz,
        string format = "HH:mm")
    {
        var zone = Resolve(tz);
        if (zone is null)
        {
            return utcInstant.ToLocalTime().ToString(format);
        }
        var local = TimeZoneInfo.ConvertTime(utcInstant, zone);
        return local.ToString(format);
    }

    /// <summary>
    /// Format a nullable UTC <see cref="DateTimeOffset"/> in the given IANA
    /// timezone. Returns null if the input is null.
    /// </summary>
    public static string? Format(
        DateTimeOffset? utcInstant,
        string? tz,
        string format = "HH:mm")
    {
        return utcInstant.HasValue ? Format(utcInstant.Value, tz, format) : null;
    }

    /// <summary>
    /// Resolve an IANA timezone name to a <see cref="TimeZoneInfo"/>, or
    /// null if it cannot be resolved. The result is cached.
    /// </summary>
    public static TimeZoneInfo? Resolve(string? ianaId)
    {
        if (string.IsNullOrWhiteSpace(ianaId))
        {
            return null;
        }
        if (_cache.TryGetValue(ianaId, out var cached))
        {
            return cached;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(ianaId, out var windowsId) && windowsId is not null)
        {
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                _cache[ianaId] = zone;
                return zone;
            }
            catch (TimeZoneNotFoundException)
            {
                // fall through
            }
            catch (InvalidTimeZoneException)
            {
                // fall through
            }
        }

        // Some IDs are already Windows ids ("Tokyo Standard Time", "UTC").
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
            _cache[ianaId] = zone;
            return zone;
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        return null;
    }
}
