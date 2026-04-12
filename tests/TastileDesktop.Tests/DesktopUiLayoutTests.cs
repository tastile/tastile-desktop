using System.Text.RegularExpressions;

namespace TastileDesktop.Tests;

public sealed class DesktopUiLayoutTests
{
    [Fact]
    public void MainWindow_QuickButtons_KeepCreateTileAndUseSingleIntegrationsEntry()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\TastileDesktop\MainWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("x:Name=\"CreateTileButton\"", xaml);
        Assert.Contains("Click=\"OnOpenCreateTileWindowClick\"", xaml);
        Assert.DoesNotContain("x:Name=\"RefreshButton\"", xaml);
        Assert.Single(Regex.Matches(xaml, "Click=\"OnOpenIntegrationsWindowClick\"").Cast<Match>());
    }

    [Fact]
    public void SettingsWindow_Updates_DoesNotExposeManifestUrlField()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\TastileDesktop\Views\SettingsWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.DoesNotContain("Manifest URL", xaml);
        Assert.DoesNotContain("UpdateManifestUrl", xaml);
        Assert.Contains("Check for updates", xaml);
    }

    [Fact]
    public void SettingsWindow_ActionButtons_UseFullWidthLayout()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\TastileDesktop\Views\SettingsWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.DoesNotContain("HorizontalAlignment=\"Left\"", xaml);
        Assert.Contains("Content=\"Sign in with Google\" Click=\"OnSignInGoogleClick\" HorizontalAlignment=\"Stretch\"", xaml);
        Assert.Contains("Content=\"Sign out\" Click=\"OnSignOutClick\" HorizontalAlignment=\"Stretch\"", xaml);
        Assert.Contains("Content=\"Sync now\" Click=\"OnSyncNowClick\" HorizontalAlignment=\"Stretch\"", xaml);
        Assert.Contains("Content=\"Refresh status\" Click=\"OnRefreshSyncStatusClick\" HorizontalAlignment=\"Stretch\"", xaml);
        Assert.Contains("Content=\"Check for updates\" Click=\"OnCheckUpdateClick\" HorizontalAlignment=\"Stretch\"", xaml);
    }

    [Fact]
    public void IntegrationsWindow_ExposesSyncModeAndTargetCalendarControls()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\TastileDesktop\Views\IntegrationsWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("x:Name=\"SyncModeComboBox\"", xaml);
        Assert.Contains("x:Name=\"TargetCalendarTextBox\"", xaml);
        Assert.Contains("x:Name=\"SavePolicyButton\"", xaml);
        Assert.Contains("x:Name=\"ConnectionHeadlineTextBlock\"", xaml);
        Assert.Contains("x:Name=\"ConnectionDetailTextBlock\"", xaml);
        Assert.Contains("x:Name=\"SyncModeDescriptionTextBlock\"", xaml);
        Assert.Contains("x:Name=\"LastSyncTextBlock\"", xaml);
        Assert.DoesNotContain("Content=\"push_only\"", xaml);
        Assert.DoesNotContain("Connect Disconnect Sync now をここから操作できます", xaml);
    }

    [Fact]
    public void AuthWindow_UsesStepByStepCalendarConnectionCopy()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\TastileDesktop\Views\AuthWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("Connect Google Calendar", xaml);
        Assert.Contains("Sign in with Google in your browser", xaml);
        Assert.Contains("Allow Tastile to access the calendar you want to sync", xaml);
        Assert.Contains("Return here after the browser says the connection is complete", xaml);
    }
}
