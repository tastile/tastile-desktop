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

        Assert.Contains("ARTIFACT_KEY=\"releases/desktop/${VERSION}/tastile-desktop-${VERSION}-setup.exe\"", workflow);
        Assert.Contains("channels/stable/desktop.json", workflow);
        Assert.Contains("environment: production", workflow);
        Assert.Contains("ACTUAL_VERSION=$(jq -r '.latest_version // empty'", workflow);
        Assert.Contains("DOWNLOAD_URL=$(jq -r '.download_url // empty'", workflow);
        Assert.Contains("EXPECTED_SHA256=$(jq -r '.sha256 // empty'", workflow);
        Assert.Contains("curl --fail --silent --show-error --location \"$DOWNLOAD_URL\" -o /tmp/public-installer.exe", workflow);
        Assert.Contains("test \"${ACTUAL_SHA256,,}\" = \"${EXPECTED_SHA256,,}\"", workflow);
        Assert.Contains("Publish stable manifests last", workflow);
        Assert.Contains("updates/desktop/manifest.json", workflow);
        Assert.Contains("--if-none-match '*'", workflow);
        Assert.Contains("--content-disposition", workflow);
        Assert.Contains("DOWNLOAD_PUBLIC_BASE_URL must use HTTPS", workflow);
        Assert.Contains("--target \"$GITHUB_SHA\"", workflow);
        Assert.Contains("Verify GitHub Release asset", workflow);
        Assert.Contains("environment: production", workflow);
    }

    [Fact]
    public void ReleaseWorkflow_PublishesCanonicalManifestOnGitHubRelease()
    {
        var workflowPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\.github\workflows\release.yml"));
        var workflow = File.ReadAllText(workflowPath);

        // GitHub Releases is the canonical distribution channel (desktop#65):
        // the update manifest must be uploaded as a release asset and verified.
        Assert.Contains("\"dist/update/manifest.json\"", workflow);
        Assert.Contains("Verify GitHub Release manifest asset", workflow);
        Assert.Contains("--pattern \"manifest.json\"", workflow);
        Assert.Contains("releases/download/v${VERSION}/tastile-desktop-${VERSION}-setup.exe", workflow);
    }

    [Fact]
    public void AppUpdateService_DefaultsToGitHubReleasesManifest()
    {
        var servicePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\TastileDesktop\Services\AppUpdateService.cs"));
        var service = File.ReadAllText(servicePath);

        Assert.Contains("https://github.com/tastile/tastile-desktop/releases/latest/download/manifest.json", service);
        Assert.DoesNotContain("download.tastile.app", service);
    }

    [Fact]
    public void ReleaseWorkflow_R2MirrorIsOptional()
    {
        var workflowPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\.github\workflows\release.yml"));
        var workflow = File.ReadAllText(workflowPath);

        // R2 remains an interim mirror for pre-cutover clients but must not be
        // a required dependency: every R2 step is gated on the configuration
        // probe output.
        Assert.Contains("Check optional R2 mirror configuration", workflow);
        Assert.Contains("id: r2check", workflow);
        Assert.Contains("if: steps.r2check.outputs.enabled == 'true'", workflow);
        Assert.Contains("dist/update/r2-manifest.json", workflow);
    }
}
