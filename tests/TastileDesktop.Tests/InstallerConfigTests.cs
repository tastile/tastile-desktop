using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TastileDesktop.Tests;

public sealed class InstallerConfigTests
{
    [Fact]
    public void WindowsManifests_MatchProjectVersion()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\TastileDesktop\TastileDesktop.csproj"));
        var appManifestPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\TastileDesktop\app.manifest"));
        var packageManifestPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\TastileDesktop\Package.appxmanifest"));

        var version = XDocument.Load(projectPath)
            .Descendants("Version")
            .Single()
            .Value;

        XNamespace assemblyNs = "urn:schemas-microsoft-com:asm.v1";
        var appManifestVersion = XDocument.Load(appManifestPath)
            .Root?
            .Element(assemblyNs + "assemblyIdentity")?
            .Attribute("version")?
            .Value;

        XNamespace packageNs = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        var packageManifestVersion = XDocument.Load(packageManifestPath)
            .Root?
            .Element(packageNs + "Identity")?
            .Attribute("Version")?
            .Value;

        Assert.Equal(version, appManifestVersion);
        Assert.Equal(version, packageManifestVersion);
    }

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

    [Fact]
    public void ReleaseWorkflow_EmbedsInstallerSha256InManifest()
    {
        var workflowPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\.github\workflows\release.yml"));
        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("INSTALLER_SHA256=$(sha256sum \"$INSTALLER_PATH\"", workflow);
        Assert.Contains("--arg sha256 \"$INSTALLER_SHA256\"", workflow);
        Assert.Matches(new Regex(@"sha256:\s*\$sha256", RegexOptions.Multiline), workflow);
    }

    [Fact]
    public void ReleaseWorkflow_RejectsUnsafeVersionsBeforePublishing()
    {
        var workflowPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\.github\workflows\release.yml"));
        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("$version -notmatch '^[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+$'", workflow);
        Assert.Contains("[int]::TryParse($component, [ref]$parsedComponent)", workflow);
        Assert.Contains("$parsedComponent -gt 65535", workflow);
        Assert.DoesNotContain("$version -notmatch '^\\d+", workflow);
    }

    [Fact]
    public void ReleaseWorkflow_RequiresResolvedVersionToMatchProjectVersion()
    {
        var workflowPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\.github\workflows\release.yml"));
        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains(@"src\TastileDesktop\TastileDesktop.csproj", workflow);
        Assert.Contains("$projectVersion", workflow);
        Assert.Contains("if ($version -ne $projectVersion)", workflow);
    }

    [Fact]
    public void ReleaseWorkflow_VerifiesPublishedManifestAndInstallerHash()
    {
        var workflowPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\.github\workflows\release.yml"));
        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("ACTUAL_VERSION=$(jq -r '.latest_version // .latest // empty'", workflow);
        Assert.Contains("DOWNLOAD_URL=$(jq -r '.download_url // empty'", workflow);
        Assert.Contains("EXPECTED_SHA256=$(jq -r '.sha256 // empty'", workflow);
        Assert.Contains("[0-9A-Fa-f]{64}", workflow);
        Assert.Contains("[[ \"$DOWNLOAD_URL\" =~ ^https:// ]]", workflow);
        Assert.Contains("LOCAL_INSTALLER=\"dist/update/tastile-desktop-${VERSION}-setup.exe\"", workflow);
        Assert.Contains("LOCAL_SHA256=$(sha256sum \"$LOCAL_INSTALLER\"", workflow);
        Assert.Contains("\"${EXPECTED_SHA256,,}\" != \"${LOCAL_SHA256,,}\"", workflow);
        Assert.Contains("curl -fsSL \"$DOWNLOAD_URL\" -o \"$PUBLIC_INSTALLER\"", workflow);
        Assert.Contains("ACTUAL_SHA256=$(sha256sum \"$PUBLIC_INSTALLER\"", workflow);
        Assert.Contains("\"${ACTUAL_SHA256,,}\" != \"${LOCAL_SHA256,,}\"", workflow);
    }
}
