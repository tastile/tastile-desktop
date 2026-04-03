using TastileDesktop.Services;
using System.IO;

namespace TastileDesktop.Tests;

public sealed class QuickPanelIconStyleResolverTests
{
    [Fact]
    public void Resolve_PrimaryCreationAction_UsesPrimaryForegroundBrush()
    {
        var style = QuickPanelIconStyleResolver.Resolve(QuickPanelActionRole.PrimaryCreation);

        Assert.Equal("PrimaryForegroundBrush", style.ForegroundBrushKey);
    }

    [Fact]
    public void Resolve_SecondaryUtilityAction_UsesTertiaryBrush()
    {
        var style = QuickPanelIconStyleResolver.Resolve(QuickPanelActionRole.SecondaryUtility);

        Assert.Equal("TertiaryForegroundBrush", style.ForegroundBrushKey);
    }

    [Fact]
    public void MainWindow_Xaml_BindsCreateAndIntegrationsToRoleBasedBrushProperties()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "MainWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("x:Name=\"IntegrationsButton\"", xaml);
        Assert.Contains("Foreground=\"{x:Bind IntegrationsActionIconForegroundBrush, Mode=OneWay}\"", xaml);
        Assert.Contains("x:Name=\"CreateTileButton\"", xaml);
        Assert.Contains("Foreground=\"{x:Bind CreateActionIconForegroundBrush, Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void MainWindow_CodeBehind_MapsRoleResolverToBrushProperties()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "MainWindow.xaml.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("CreateActionIconForegroundBrush = ResolveThemeBrush(QuickPanelIconStyleResolver.Resolve(QuickPanelActionRole.PrimaryCreation).ForegroundBrushKey);", source);
        Assert.Contains("IntegrationsActionIconForegroundBrush = ResolveThemeBrush(QuickPanelIconStyleResolver.Resolve(QuickPanelActionRole.SecondaryUtility).ForegroundBrushKey);", source);
    }
}
