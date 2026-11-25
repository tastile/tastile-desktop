param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $false)][string]$Ref = "release-0-7-0"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$') {
    throw "Version must use four numeric components, for example 0.3.13.0."
}

Write-Host "Trigger workflow: GitHub Releases desktop release"
Write-Host "Dispatching release.yml on ref $Ref"
Write-Host "  version        = $Version"
Write-Host "  github_release = v$Version (installer + manifest.json assets)"
Write-Host "  r2_mirror      = optional interim mirror, enabled only when R2 secrets exist in Infisical"
Write-Host "    artifact_key  = releases/desktop/$Version/tastile-desktop-$Version-setup.exe"
Write-Host "    manifest_keys = channels/stable/desktop.json, updates/desktop/manifest.json"
Write-Host ""
Write-Host "GitHub Releases is the canonical distribution channel (ADR-0025 / desktop#65)."
Write-Host "The release workflow obtains publishing values from Infisical using GitHub OIDC."
Write-Host "Run migrate-production-secrets.yml once before the first release to transfer and verify existing values."

gh workflow run release.yml --repo tastile/tastile-desktop --ref $Ref --field version=$Version
if ($LASTEXITCODE -ne 0) {
    throw "GitHub Actions workflow dispatch failed."
}
