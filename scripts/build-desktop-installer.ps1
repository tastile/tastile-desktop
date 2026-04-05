param(
[Parameter(Mandatory = $false)][string]$Version = "0.2.29"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Version = $Version.Trim()
if ($Version.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
    $Version = $Version.Substring(1)
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$buildOutDir = Join-Path $repoRoot "artifacts\desktop-build"
$installerOut = Join-Path $repoRoot "artifacts\installer"
$issPath = Join-Path $repoRoot "installer\TastileDesktop.iss"
$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($iscc)) {
    throw "Inno Setup compiler not found. Checked: $($isccCandidates -join ', '). Install Inno Setup 6."
}

New-Item -ItemType Directory -Path $buildOutDir -Force | Out-Null
New-Item -ItemType Directory -Path $installerOut -Force | Out-Null

Push-Location $repoRoot
try {
    dotnet publish "src\TastileDesktop\TastileDesktop.csproj" `
        -c Release `
        -o $buildOutDir `
        -p:Version=$Version `
        -p:DaemonRustTarget=x86_64-pc-windows-msvc `
        -p:DaemonBinaryPath=..\..\..\tastile-core\target\x86_64-pc-windows-msvc\release\tastile-daemon.exe `
        -p:PublishSingleFile=false `
        -p:AppxPackage=false `
        -p:WindowsPackageType=None

    $daemonSource = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "..\tastile-core\target\x86_64-pc-windows-msvc\release\tastile-daemon.exe"))
    if (!(Test-Path $daemonSource)) {
        throw "Bundled daemon missing: $daemonSource"
    }

    Copy-Item $daemonSource (Join-Path $buildOutDir "tastile-daemon.exe") -Force

    $installerIconDir = Join-Path $buildOutDir "Assets"
    New-Item -ItemType Directory -Path $installerIconDir -Force | Out-Null
    Copy-Item (Join-Path $repoRoot "src\TastileDesktop\Assets\tastile.ico") (Join-Path $installerIconDir "tastile.ico") -Force
    Copy-Item (Join-Path $repoRoot "src\TastileDesktop\Assets\tastile-tray.ico") (Join-Path $installerIconDir "tastile-tray.ico") -Force
    Copy-Item (Join-Path $repoRoot "src\TastileDesktop\Assets\tastile-tray.ico") (Join-Path $buildOutDir "tastile-tray.ico") -Force
    Copy-Item (Join-Path $repoRoot "src\TastileDesktop\Assets\tastile.ico") (Join-Path $buildOutDir "tastile-icon.ico") -Force

    & $iscc "/DSourceDir=$buildOutDir" "/DOutputDir=$installerOut" "/DAppVersion=$Version" $issPath

    $installerPath = Join-Path $installerOut "tastile-desktop-$Version-setup.exe"
    if (!(Test-Path $installerPath)) {
        throw "Installer output missing: $installerPath"
    }

    Write-Host "Installer built: $installerPath"
}
finally {
    Pop-Location
}
