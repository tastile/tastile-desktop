param(
    [Parameter(Mandatory = $false)][string]$Version = "",
    [Parameter(Mandatory = $false)][ValidateSet("win-x64", "win-arm64", "win-x86")][string]$RuntimeIdentifier = "win-x64",
    [Parameter(Mandatory = $false)][string]$CertThumbprint = ""
)

<#
.SYNOPSIS
    Builds the MSIX package for the desktop app (desktop#61 scaffold).

.DESCRIPTION
    Produces an unsigned (or dev-signed) .msix under artifacts\msix\. This is a
    packaging scaffold only: production signing, Microsoft Store submission,
    and clean-machine install verification are tracked separately and require
    explicit approval. See docs/msix-packaging.md for the verification
    checklist and signing notes.

    Requires Windows with the .NET SDK (see global.json) and the Windows SDK
    packaging tools that ship with Microsoft.Windows.SDK.BuildTools.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$Version = ($Version ?? "").Trim()
if ([string]::IsNullOrWhiteSpace($Version)) {
    $csprojPath = Join-Path $repoRoot "src\TastileDesktop\TastileDesktop.csproj"
    [xml]$csproj = Get-Content -Path $csprojPath -Raw
    $Version = [string]($csproj.Project.PropertyGroup.Version | Select-Object -First 1)
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not determine desktop app version from src\TastileDesktop\TastileDesktop.csproj"
}
if ($Version.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
    $Version = $Version.Substring(1)
}

$platformByRid = @{
    "win-x64"   = "x64"
    "win-arm64" = "ARM64"
    "win-x86"   = "x86"
}
$msbuildPlatform = $platformByRid[$RuntimeIdentifier]

$msixOut = Join-Path $repoRoot "artifacts\msix"
$buildOutDir = Join-Path $repoRoot "artifacts\msix-build"
New-Item -ItemType Directory -Path $msixOut -Force | Out-Null
New-Item -ItemType Directory -Path $buildOutDir -Force | Out-Null

Push-Location $repoRoot
try {
    # AppxPackage + WindowsPackageType=MSIX switches the publish output from
    # the unpackaged layout to a packaged .msix. The project keeps
    # AppxPackage=false / WindowsPackageType=None by default for development.
    dotnet publish "src\TastileDesktop\TastileDesktop.csproj" `
        -c Release `
        -r $RuntimeIdentifier `
        -o $buildOutDir `
        -p:Version=$Version `
        -p:Platform=$msbuildPlatform `
        -p:AppxPackage=true `
        -p:WindowsPackageType=MSIX `
        -p:PublishSingleFile=false

    # The MSIX tooling may emit the package next to the publish output or
    # under bin\; collect whichever .msix was produced.
    $msixCandidates = @(
        Get-ChildItem -Path $buildOutDir -Filter "*.msix" -Recurse -ErrorAction SilentlyContinue
        Get-ChildItem -Path (Join-Path $repoRoot "src\TastileDesktop\bin") -Filter "*.msix" -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -gt (Get-Date).AddHours(-1) }
    ) | Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if ($null -eq $msixCandidates) {
        throw "MSIX output not found. Expected a .msix under $buildOutDir or src\TastileDesktop\bin."
    }

    $msixName = "tastile-desktop-$Version-$RuntimeIdentifier.msix"
    $msixPath = Join-Path $msixOut $msixName
    Copy-Item $msixCandidates.FullName $msixPath -Force

    if (-not [string]::IsNullOrWhiteSpace($CertThumbprint)) {
        $signtool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
        if ([string]::IsNullOrWhiteSpace($signtool)) {
            throw "signtool.exe not found; install the Windows SDK signing tools to use -CertThumbprint."
        }
        & $signtool sign /fd SHA256 /sha1 $CertThumbprint /tr "http://timestamp.digicert.com" /td SHA256 $msixPath
        if ($LASTEXITCODE -ne 0) { throw "signtool sign failed with exit code $LASTEXITCODE" }
        Write-Host "Signed MSIX with certificate thumbprint $CertThumbprint"
    }
    else {
        Write-Warning "MSIX is unsigned. Sideloading requires a trusted dev certificate; see docs/msix-packaging.md."
    }

    Write-Host "MSIX package: $msixPath"
}
finally {
    Pop-Location
}
