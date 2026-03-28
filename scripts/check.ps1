param(
    [switch]$SkipDesktopBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$desktopProject = Join-Path $repoRoot "src\TastileDesktop\TastileDesktop.csproj"
$testProject = Join-Path $repoRoot "tests\TastileDesktop.Tests\TastileDesktop.Tests.csproj"
$coreRepo = Join-Path (Split-Path $repoRoot -Parent) "tastile-core"

Push-Location $repoRoot
try {
    Write-Host "==> Running desktop unit tests"
    dotnet test $testProject

    if ($SkipDesktopBuild) {
        Write-Host "==> Skipping desktop build"
        return
    }

    if (!(Test-Path $coreRepo)) {
        throw "Desktop build requires a sibling tastile-core checkout at '$coreRepo'. Use -SkipDesktopBuild to run tests only."
    }

    Write-Host "==> Building desktop application"
    dotnet build $desktopProject -r win-x64
}
finally {
    Pop-Location
}
