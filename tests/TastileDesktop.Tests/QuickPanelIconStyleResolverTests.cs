using System.IO;
using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class QuickPanelIconStyleResolverTests
{
    [Fact]
    public void Resolve_PrimaryCreationAction_UsesTextFillColorPrimaryBrush()
    {
        var style = QuickPanelIconStyleResolver.Resolve(QuickPanelActionRole.PrimaryCreation);

        Assert.Equal("TextFillColorPrimaryBrush", style.ForegroundBrushKey);
    }

    [Fact]
    public void Resolve_SecondaryUtilityAction_UsesTextFillColorTertiaryBrush()
    {
        var style = QuickPanelIconStyleResolver.Resolve(QuickPanelActionRole.SecondaryUtility);

        Assert.Equal("TextFillColorTertiaryBrush", style.ForegroundBrushKey);
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
