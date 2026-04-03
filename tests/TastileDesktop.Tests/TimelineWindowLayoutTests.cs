using System.IO;
using Xunit;

namespace TastileDesktop.Tests;

public sealed class TimelineWindowLayoutTests
{
    [Fact]
    public void TimelineWindow_UsesCanvasItemContainerBindings_ForMarkerAndBlockPositioning()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.TimelineHourMarkers, Mode=OneWay}\"", xaml);
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.TimelineBlocks, Mode=OneWay}\"", xaml);

        Assert.Contains("TranslateTransform Y=\"{x:Bind Top}\"", xaml);
        Assert.Contains("TranslateTransform X=\"{x:Bind Left}\" Y=\"{x:Bind Top}\"", xaml);
    }

    [Fact]
    public void TimelineWindow_UsesButtonStatusAffordance_InsteadOfPassiveIcon()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("Click=\"OnTimelineBlockStatusClick\"", xaml);
        Assert.DoesNotContain("ToolTipService.ToolTip=\"{x:Bind StatusIconToolTip}\"", xaml);
    }

    [Fact]
    public void TimelineWindow_StatusClick_DelegatesLifecycleDecisionToResolver()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "TimelineWindow.xaml.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("var lifecycle = block?.Lifecycle;", source);
        Assert.Contains("TimelineStatusActionResolver.Resolve(tileId, lifecycle)", source);
        Assert.DoesNotContain("if (lifecycle == \"done\")", source);
    }
}
