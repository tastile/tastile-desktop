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
}
