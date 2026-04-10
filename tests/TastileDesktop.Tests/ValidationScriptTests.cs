using System.IO;
using Xunit;

namespace TastileDesktop.Tests;

public sealed class ValidationScriptTests
{
    [Fact]
    public void CheckScript_CleansDesktopBuildArtifactsBeforeValidationBuilds()
    {
        var scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "check.ps1"));
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("Remove-Item -Recurse -Force $desktopObjDir, $desktopBinDir", script);
    }

    [Fact]
    public void CheckScript_ValidatesDefaultAndRidBuilds_AndGuardsTimelineConnectorWiring()
    {
        var scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "check.ps1"));
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("function Invoke-Step", script);
        Assert.Contains("if ($LASTEXITCODE -ne 0)", script);
        Assert.Contains("dotnet build $desktopProject", script);
        Assert.Contains("dotnet build $desktopProject -r win-x64", script);
        Assert.Contains("TimelineWindow.g.cs", script);
        Assert.Contains("OnNavigatePreviousClick", script);
        Assert.Contains("OnRangeSelectionChanged", script);
        Assert.Contains("this.HourMarkersItemsControl =", script);
        Assert.Contains("this.TimelineBlocksItemsControl =", script);
        Assert.Contains("this.WeekHourMarkersItemsControl =", script);
        Assert.Contains("$allowedAssignedFields = @(", script);
        Assert.Contains("WeekTimelineColumnsHost", script);
        Assert.Contains("TimelineCanvasHost", script);
        Assert.Contains("unexpected assignments", script);
    }

    [Fact]
    public void DesktopProject_EnforcesTimelineConnectorValidation_DuringBuild()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TastileDesktop", "TastileDesktop.csproj"));
        var project = File.ReadAllText(projectPath);

        Assert.Contains("<EnableTimelineConnectorValidation>true</EnableTimelineConnectorValidation>", project);
        Assert.Contains("Name=\"ValidateTimelineGeneratedConnectorWiring\"", project);
        Assert.Contains("AfterTargets=\"MarkupCompilePass2\"", project);
        Assert.Contains("TimelineWindow.g.cs", project);
        Assert.Contains("OnNavigatePreviousClick", project);
        Assert.Contains("OnRangeSelectionChanged", project);
        Assert.Contains("this.HourMarkersItemsControl =", project);
        Assert.Contains("this.TimelineBlocksItemsControl =", project);
        Assert.Contains("this.WeekHourMarkersItemsControl =", project);
        Assert.Contains("$allowedAssignedFields = @(", project);
        Assert.Contains("WeekTimelineColumnsHost", project);
        Assert.Contains("TimelineCanvasHost", project);
        Assert.Contains("unexpected assignments", project);
    }
}
