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
        Assert.Contains("Content=\"Check for updates\" Click=\"OnCheckUpdateClick\" HorizontalAlignment=\"Stretch\"", xaml);
        // The sync section was removed when the desktop stopped talking to a
        // local daemon. Server-driven sync has no client-side action to wire.
        Assert.DoesNotContain("Click=\"OnSyncNowClick\"", xaml);
        Assert.DoesNotContain("Click=\"OnRefreshSyncStatusClick\"", xaml);
        Assert.DoesNotContain("Click=\"OnResetLocalSyncDataClick\"", xaml);
        Assert.DoesNotContain("Click=\"OnRedownloadRemoteSyncDataClick\"", xaml);
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

        // The new auth model is single-step: open the browser, complete sign-in
        // in the Cognito Hosted UI, return here. The old "Connect Google Calendar"
        // copy belonged to the daemon-era ProviderToken flow.
        Assert.Contains("Sign in to Tastile", xaml);
        Assert.Contains("Tastile opens your browser to complete sign-in", xaml);
        Assert.DoesNotContain("Connect Google Calendar", xaml);
    }
}
