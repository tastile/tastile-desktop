param(
    [Parameter(Mandatory = $false)][string]$Version = "0.1.0"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "artifacts\desktop-publish"
$installerOut = Join-Path $repoRoot "artifacts\installer"
$issPath = Join-Path $repoRoot "installer\TastileDesktop.iss"
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

if (!(Test-Path $iscc)) {
    throw "Inno Setup compiler not found at '$iscc'. Install Inno Setup 6."
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $installerOut -Force | Out-Null

Push-Location $repoRoot
try {
    dotnet publish "src\TastileDesktop\TastileDesktop.csproj" `
        -c Release `
        -p:AppxPackage=false `
        -p:WindowsPackageType=None `
        -o $publishDir

    & $iscc "/DSourceDir=$publishDir" "/DOutputDir=$installerOut" "/DAppVersion=$Version" $issPath

    $installerPath = Join-Path $installerOut "tastile-desktop-$Version-setup.exe"
    if (!(Test-Path $installerPath)) {
        throw "Installer output missing: $installerPath"
    }

    Write-Host "Installer built: $installerPath"
}
finally {
    Pop-Location
}
