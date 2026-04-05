using System.IO;

namespace TastileDesktop.Tests;

public sealed class SettingsLayoutTests
{
    [Fact]
    public void SettingsWindow_UsesFixedRightColumnWidth_ForInteractiveRows()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "SettingsWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("<TextBlock Text=\"Toast reminder\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<NumberBox Grid.Column=\"1\" Value=\"{x:Bind ViewModel.ToastNotifyMinutes, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"60\" SpinButtonPlacementMode=\"Inline\" Width=\"160\" HorizontalAlignment=\"Right\" />", xaml);

        Assert.Contains("<TextBlock Text=\"Force intervention\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<NumberBox Grid.Column=\"1\" Value=\"{x:Bind ViewModel.InterventionMinutes, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"120\" SpinButtonPlacementMode=\"Inline\" Width=\"160\" HorizontalAlignment=\"Right\" />", xaml);

        Assert.Contains("<TextBlock Text=\"Repeat intervention\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<NumberBox Grid.Column=\"1\" Value=\"{x:Bind ViewModel.InterventionRepeatMinutes, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"60\" SpinButtonPlacementMode=\"Inline\" Width=\"160\" HorizontalAlignment=\"Right\" />", xaml);

        Assert.Contains("<TextBlock Text=\"Prompt edge overlay\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<ToggleSwitch Grid.Column=\"1\" IsOn=\"{x:Bind ViewModel.PromptOverlayEnabled, Mode=TwoWay}\" HorizontalAlignment=\"Right\" />", xaml);
        Assert.Contains("<TextBlock Text=\"Prompt toast sound\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<ToggleSwitch Grid.Column=\"1\" IsOn=\"{x:Bind ViewModel.PromptToastSoundEnabled, Mode=TwoWay}\" HorizontalAlignment=\"Right\" />", xaml);
        Assert.Contains("<TextBlock Text=\"Prompt toast sound source\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<TextBlock Text=\"Prompt toast sound length\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<TextBlock Text=\"Prompt toast repeat\" VerticalAlignment=\"Center\" />", xaml);

        Assert.Contains("<TextBlock Text=\"Overlay duration\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<NumberBox Grid.Column=\"1\" Value=\"{x:Bind ViewModel.PromptOverlayDurationSeconds, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"15\" SpinButtonPlacementMode=\"Inline\" Width=\"160\" HorizontalAlignment=\"Right\" />", xaml);

        Assert.Contains("<TextBlock Text=\"Default break time\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<NumberBox Grid.Column=\"1\" Value=\"{x:Bind ViewModel.DefaultBreakMinutes, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"60\" SpinButtonPlacementMode=\"Inline\" Width=\"160\" HorizontalAlignment=\"Right\" />", xaml);

        Assert.Contains("<TextBlock Text=\"Idle prompt after\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<NumberBox Grid.Column=\"1\" Value=\"{x:Bind ViewModel.IdlePromptMinutes, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"60\" SpinButtonPlacementMode=\"Inline\" Width=\"160\" HorizontalAlignment=\"Right\" />", xaml);

        Assert.Contains("<TextBlock Text=\"Launch at startup\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<ToggleSwitch Grid.Column=\"1\" IsOn=\"{x:Bind ViewModel.LaunchAtStartup, Mode=TwoWay}\" HorizontalAlignment=\"Right\" />", xaml);
    }

    [Fact]
    public void SettingsWindow_UsesRightAlignedSummaryValues_InSyncRows()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "SettingsWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("x:Name=\"SyncStateTextBlock\" Grid.Column=\"1\" Text=\"Unknown\" Foreground=\"{StaticResource AppForegroundMutedBrush}\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Right\"", xaml);
        Assert.Contains("x:Name=\"SyncLastAttemptTextBlock\" Grid.Column=\"1\" Text=\"-\" Foreground=\"{StaticResource AppForegroundMutedBrush}\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Right\"", xaml);
        Assert.Contains("x:Name=\"SyncLastSuccessTextBlock\" Grid.Column=\"1\" Text=\"-\" Foreground=\"{StaticResource AppForegroundMutedBrush}\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Right\"", xaml);
    }
}
