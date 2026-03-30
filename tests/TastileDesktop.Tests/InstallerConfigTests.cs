using System.Text.RegularExpressions;

namespace TastileDesktop.Tests;

public sealed class InstallerConfigTests
{
    [Fact]
    public void InstallerConfig_UsesStableProductIdentityAndCleanDisplayName()
    {
        var issPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\installer\TastileDesktop.iss"));
        var iss = File.ReadAllText(issPath);

        Assert.Contains("AppId={{F2F2B6E3-5D2B-4B66-8C4B-A4A8A65C54E9}}", iss);
        Assert.Contains("AppName=Tastile", iss);
        Assert.Contains("AppVerName=Tastile", iss);
        Assert.Contains("DefaultDirName={autopf}\\Tastile", iss);
        Assert.Contains("DefaultGroupName=Tastile", iss);
        Assert.Contains("CloseApplications=yes", iss);
        Assert.DoesNotMatch(new Regex(@"AppName=Tastile Desktop", RegexOptions.Multiline), iss);
        Assert.DoesNotMatch(new Regex(@"DefaultDirName=\{autopf\}\\Tastile Desktop", RegexOptions.Multiline), iss);
        Assert.DoesNotMatch(new Regex(@"Tastile Desktop", RegexOptions.Multiline), iss);
    }
}
