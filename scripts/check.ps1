param(
    [switch]$SkipDesktopBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action,
        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw $FailureMessage
    }
}

function Assert-NoTimelineToolbarConnectorWiring {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DesktopObjDir
    )

    $generatedFiles = Get-ChildItem -Path $DesktopObjDir -Recurse -Filter "TimelineWindow.g.cs" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like "*\Views\TimelineWindow.g.cs" }

    if ($null -eq $generatedFiles -or $generatedFiles.Count -eq 0) {
        throw "TimelineWindow.g.cs was not generated under '$DesktopObjDir\Debug'."
    }

    $forbiddenMarkers = @(
        "OnNavigatePreviousClick",
        "OnNavigateTodayClick",
        "OnNavigateNextClick",
        "OnViewDayClick",
        "OnViewWeekClick",
        "OnViewMonthClick",
        "OnViewYearClick",
        "OnRangeSelectionChanged",
        "OnZoomOutClick",
        "OnZoomInClick",
        "this.HourMarkersItemsControl =",
        "this.TimelineBlocksItemsControl =",
        "this.WeekHourMarkersItemsControl ="
    )

    $allowedAssignedFields = @(
        "TitleBarArea",
        "TimelineRootGrid",
        "ToolbarPanel",
        "TimelineScrollViewer",
        "MonthCalendarHost",
        "WeekTimelineScrollViewer",
        "YearCalendarHost",
        "LoadingOverlay",
        "WeekTimelineColumnsHost",
        "TimelineCanvasHost"
    )

    foreach ($generatedFile in $generatedFiles) {
        $content = Get-Content -Raw -Path $generatedFile.FullName
        foreach ($marker in $forbiddenMarkers) {
            if ($content.Contains($marker)) {
                throw "TimelineWindow connector wiring regression detected in '$($generatedFile.FullName)': found '$marker'."
            }
        }

        $assignments = [System.Text.RegularExpressions.Regex]::Matches($content, "this\.(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*global::WinRT\.CastExtensions\.As<") |
            ForEach-Object { $_.Groups["name"].Value } |
            Sort-Object -Unique
        $unexpected = @($assignments | Where-Object { $_ -notin $allowedAssignedFields })
        if ($unexpected.Count -gt 0) {
            throw "TimelineWindow connector wiring regression detected in '$($generatedFile.FullName)': unexpected assignments [$($unexpected -join ', ')]."
        }
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$desktopProject = Join-Path $repoRoot "src\TastileDesktop\TastileDesktop.csproj"
$testProject = Join-Path $repoRoot "tests\TastileDesktop.Tests\TastileDesktop.Tests.csproj"
$desktopProjectDir = Split-Path -Parent $desktopProject
$desktopObjDir = Join-Path $desktopProjectDir "obj"
$desktopBinDir = Join-Path $desktopProjectDir "bin"

Push-Location $repoRoot
try {
    Write-Host "==> Running desktop unit tests"
    Invoke-Step -Action { dotnet test $testProject } -FailureMessage "Desktop unit tests failed."

    if ($SkipDesktopBuild) {
        Write-Host "==> Skipping desktop build"
        return
    }

    # The desktop no longer bundles a local tastile-daemon binary; the
    # build is self-contained and does not need a sibling tastile-core
    # checkout on the build machine. The CLI check above still runs.

    Write-Host "==> Cleaning desktop build artifacts"
    if ((Test-Path $desktopObjDir) -or (Test-Path $desktopBinDir)) {
        Remove-Item -Recurse -Force $desktopObjDir, $desktopBinDir -ErrorAction SilentlyContinue
    }

    Write-Host "==> Building desktop application (default output)"
    Invoke-Step -Action { dotnet build $desktopProject } -FailureMessage "Desktop default build failed."

    Write-Host "==> Building desktop application (win-x64)"
    Invoke-Step -Action { dotnet build $desktopProject -r win-x64 } -FailureMessage "Desktop win-x64 build failed."

    Write-Host "==> Validating TimelineWindow generated connector wiring"
    Assert-NoTimelineToolbarConnectorWiring -DesktopObjDir $desktopObjDir
}
finally {
    Pop-Location
}
