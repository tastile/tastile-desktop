namespace TastileDesktop.Services;

using System.Globalization;
using System.Text.RegularExpressions;

public static class PromptExpiryParser
{
    private static readonly Regex FractionRegex = new(
        @"^(?<head>.+\.\d{7})\d+(?<tail>(?:Z|[+-]\d{2}:\d{2}))$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParseExpiryIso8601(string raw, out DateTimeOffset parsedUtc)
    {
        parsedUtc = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = NormalizeFraction(raw.Trim());
        if (!DateTimeOffset.TryParse(
                normalized,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return false;
        }

        parsedUtc = parsed.ToUniversalTime();
        return true;
    }

    private static string NormalizeFraction(string value)
    {
        var match = FractionRegex.Match(value);
        if (!match.Success)
        {
            return value;
        }

        return match.Groups["head"].Value + match.Groups["tail"].Value;
    }
}

