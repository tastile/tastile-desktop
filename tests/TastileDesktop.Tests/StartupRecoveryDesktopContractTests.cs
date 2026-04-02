namespace TastileDesktop.Tests;

public sealed class StartupRecoveryDesktopContractTests
{
    [Fact]
    public void MainViewModelSource_ClearsDismissReplayBlockAfterStartupDismiss()
    {
        var source = File.ReadAllText(@"C:\Users\rebui\Desktop\tastile\tastile-desktop\src\TastileDesktop\ViewModels\MainViewModel.cs");

        Assert.Contains("if (_toastDismissedByAction && prompt.Prompt.PromptId == _lastHandledPromptId)", source);
        Assert.DoesNotContain("Skipping - already dismissed by action", source);
        Assert.Contains("_toastDismissedByAction = false;", source);
        Assert.Contains("_lastHandledPromptId = null;", source);
    }

    [Fact]
    public void TilesWindowSource_HandlesConfirmStopAtInStartupRecoverySwitch()
    {
        var source = File.ReadAllText(@"C:\Users\rebui\Desktop\tastile\tastile-desktop\src\TastileDesktop\Views\TilesWindow.xaml.cs");

        Assert.Contains("case \"CONFIRM_STOP_AT\":", source);
    }
}
