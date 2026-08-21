using System.IO;

namespace TastileDesktop.Tests;

public sealed class SettingsLayoutTests
{
    [Fact]
    public void SettingsWindow_UsesFixedRightColumnWidth_ForInteractiveRows()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "SettingsWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("<TextBlock x:Uid=\"Settings_ToastReminder\" Text=\"Toast reminder\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<NumberBox Grid.Column=\"1\" Value=\"{x:Bind ViewModel.ToastNotifyMinutes, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"60\" SpinButtonPlacementMode=\"Inline\" Width=\"160\" HorizontalAlignment=\"Right\" />", xaml);

        Assert.Contains("<TextBlock x:Uid=\"Settings_ForceIntervention\" Text=\"Force intervention\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<NumberBox Grid.Column=\"1\" Value=\"{x:Bind ViewModel.InterventionMinutes, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"120\" SpinButtonPlacementMode=\"Inline\" Width=\"160\" HorizontalAlignment=\"Right\" />", xaml);

        Assert.Contains("<TextBlock x:Uid=\"Settings_RepeatIntervention\" Text=\"Repeat intervention\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<NumberBox Grid.Column=\"1\" Value=\"{x:Bind ViewModel.InterventionRepeatMinutes, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"60\" SpinButtonPlacementMode=\"Inline\" Width=\"160\" HorizontalAlignment=\"Right\" />", xaml);

        Assert.Contains("<TextBlock x:Uid=\"Settings_PromptEdgeOverlay\" Text=\"Prompt edge overlay\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<ToggleSwitch Grid.Column=\"1\" IsOn=\"{x:Bind ViewModel.PromptOverlayEnabled, Mode=TwoWay}\" HorizontalAlignment=\"Right\" />", xaml);
        Assert.Contains("<TextBlock x:Uid=\"Settings_PromptToastSound\" Text=\"Prompt toast sound\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<ToggleSwitch Grid.Column=\"1\" IsOn=\"{x:Bind ViewModel.PromptToastSoundEnabled, Mode=TwoWay}\" HorizontalAlignment=\"Right\" />", xaml);
        Assert.Contains("<TextBlock x:Uid=\"Settings_PromptToastSoundSource\" Text=\"Prompt toast sound source\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<ComboBox Grid.Column=\"1\" Width=\"160\" SelectedValuePath=\"Tag\" SelectedValue=\"{x:Bind ViewModel.PromptToastSoundSource, Mode=TwoWay}\" HorizontalAlignment=\"Right\">", xaml);
        Assert.Contains("<TextBlock x:Uid=\"Settings_PromptToastSoundRepeatMode\" Text=\"Prompt toast sound repeat mode\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<TextBlock x:Uid=\"Settings_PromptToastSoundLength\" Text=\"Prompt toast sound length\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<NumberBox Grid.Column=\"1\" Value=\"{x:Bind ViewModel.PromptToastSoundDurationSeconds, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"30\" SpinButtonPlacementMode=\"Inline\" Width=\"160\" HorizontalAlignment=\"Right\" />", xaml);
        Assert.Contains("<TextBlock x:Uid=\"Settings_PromptToastRepeatInterval\" Text=\"Prompt toast repeat interval\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<TextBlock x:Uid=\"Settings_PromptToastRepeat\" Text=\"Prompt toast repeat\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<NumberBox Grid.Column=\"1\" Value=\"{x:Bind ViewModel.PromptToastSoundRepeatCount, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"10\" SpinButtonPlacementMode=\"Inline\" Width=\"160\" HorizontalAlignment=\"Right\" />", xaml);

        Assert.Contains("<TextBlock x:Uid=\"Settings_OverlayDuration\" Text=\"Overlay duration\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<NumberBox Grid.Column=\"1\" Value=\"{x:Bind ViewModel.PromptOverlayDurationSeconds, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"15\" SpinButtonPlacementMode=\"Inline\" Width=\"160\" HorizontalAlignment=\"Right\" />", xaml);

        Assert.Contains("<TextBlock x:Uid=\"Settings_DefaultBreakTime\" Text=\"Default break time\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<NumberBox Grid.Column=\"1\" Value=\"{x:Bind ViewModel.DefaultBreakMinutes, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"60\" SpinButtonPlacementMode=\"Inline\" Width=\"160\" HorizontalAlignment=\"Right\" />", xaml);

        Assert.Contains("<TextBlock x:Uid=\"Settings_IdlePromptAfter\" Text=\"Idle prompt after\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<NumberBox Grid.Column=\"1\" Value=\"{x:Bind ViewModel.IdlePromptMinutes, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"60\" SpinButtonPlacementMode=\"Inline\" Width=\"160\" HorizontalAlignment=\"Right\" />", xaml);

        Assert.Contains("<TextBlock x:Uid=\"Settings_LaunchAtStartup\" Text=\"Launch at startup\" VerticalAlignment=\"Center\" />", xaml);
        Assert.Contains("<ToggleSwitch Grid.Column=\"1\" IsOn=\"{x:Bind ViewModel.LaunchAtStartup, Mode=TwoWay}\" HorizontalAlignment=\"Right\" />", xaml);
    }

    [Fact]
    public void SettingsWindow_RuntimePathsSection_ExposesAuthAndDataLocations()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "Views", "SettingsWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        // The runtime paths panel is the only diagnostic that remains after
        // the daemon-era sync section was removed.
        Assert.Contains("Text=\"{x:Bind ViewModel.RuntimeProfileText, Mode=OneTime}\"", xaml);
        Assert.Contains("Text=\"{x:Bind ViewModel.RuntimeAppDataDirText, Mode=OneTime}\"", xaml);
        Assert.Contains("Text=\"{x:Bind ViewModel.RuntimeSessionPathText, Mode=OneTime}\"", xaml);
        Assert.Contains("Auth credentials (DPAPI)", xaml);
        // Sync-specific diagnostics are gone.
        Assert.DoesNotContain("x:Name=\"SyncStateTextBlock\"", xaml);
        Assert.DoesNotContain("x:Name=\"SyncLastAttemptTextBlock\"", xaml);
        Assert.DoesNotContain("x:Name=\"SyncLastSuccessTextBlock\"", xaml);
    }
}
