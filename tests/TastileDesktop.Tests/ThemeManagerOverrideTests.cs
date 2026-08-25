using System.Reflection;
using Xunit;

namespace TastileDesktop.Tests;

public class ThemeManagerOverrideTests
{
    [Fact]
    public void ThemeManager_NoLongerReferences_ObsoleteAccentKeys()
    {
        var asm = typeof(TastileDesktop.Services.ThemeManager).Assembly;
        var themeManager = asm.GetType("TastileDesktop.Services.ThemeManager")!;
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