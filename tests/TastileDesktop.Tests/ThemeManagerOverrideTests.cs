using Xunit;

namespace TastileDesktop.Tests;

public class ThemeManagerOverrideTests
{
    [Fact]
    public void ThemeManager_NoLongerReferences_ObsoleteAccentKeys()
    {
        var source = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "TastileDesktop", "Services", "ThemeManager.cs"));

        Assert.DoesNotContain("\"AccentBrush\"", source);
        Assert.DoesNotContain("\"AppPrimaryBrush\"", source);
        Assert.DoesNotContain("\"AppPrimaryHoverBrush\"", source);
        Assert.Contains("\"OverrideAccentFillBrush\"", source);
        Assert.Contains("\"OverrideAccentFillSecondaryBrush\"", source);
    }
}
