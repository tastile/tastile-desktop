namespace TastileDesktop.Tests;

using TastileDesktop.Services;

public sealed class PromptExpiryParserTests
{
    [Fact]
    public void TryParseExpiryIso8601_AcceptsNanosecondFraction()
    {
        var ok = PromptExpiryParser.TryParseExpiryIso8601(
            "2026-04-15T13:49:13.522123456+00:00",
            out var parsedUtc);

        Assert.True(ok);
        Assert.Equal(TimeSpan.Zero, parsedUtc.Offset);
        Assert.Equal(2026, parsedUtc.Year);
        Assert.Equal(4, parsedUtc.Month);
        Assert.Equal(15, parsedUtc.Day);
    }

    [Fact]
    public void TryParseExpiryIso8601_AcceptsZuluFormat()
    {
        var ok = PromptExpiryParser.TryParseExpiryIso8601(
            "2026-04-15T13:49:13.522123456Z",
            out var parsedUtc);

        Assert.True(ok);
        Assert.Equal(TimeSpan.Zero, parsedUtc.Offset);
    }
}

