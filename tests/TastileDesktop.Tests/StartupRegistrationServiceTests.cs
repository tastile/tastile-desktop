using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class StartupRegistrationServiceTests
{
    [Fact]
    public void BuildCommand_QuotesExecutableAndAddsMinimizedFlag()
    {
        var command = StartupRegistrationService.BuildCommand(@"C:\Program Files\Tastile Desktop\TastileDesktop.exe");

        Assert.Equal(@"""C:\Program Files\Tastile Desktop\TastileDesktop.exe"" --minimized", command);
    }

    [Fact]
    public void RegistryValueName_MatchesInstallerRegistration()
    {
        Assert.Equal("TastileDesktop", StartupRegistrationService.RegistryValueName);
    }
}
