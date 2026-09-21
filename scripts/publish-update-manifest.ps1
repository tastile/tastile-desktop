param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $false)][string]$Ref = "release-0-7-0"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$') {
    throw "Version must use four numeric components, for example 0.3.13.0."
}

Write-Host "Trigger workflow: Cloudflare R2 desktop release"
Write-Host "Dispatching release.yml on ref $Ref"
Write-Host "  version        = $Version"
Write-Host "  artifact_key  = releases/desktop/$Version/tastile-desktop-$Version-setup.exe"
Write-Host "  manifest_keys = channels/stable/desktop.json, updates/desktop/manifest.json"
Write-Host ""
Write-Host "Required repo secrets:"
Write-Host "  CLOUDFLARE_ACCOUNT_ID"
Write-Host "  CLOUDFLARE_R2_BUCKET"
Write-Host "  CLOUDFLARE_R2_ACCESS_KEY_ID"
Write-Host "  CLOUDFLARE_R2_SECRET_ACCESS_KEY"
Write-Host "  DOWNLOAD_PUBLIC_BASE_URL"

gh workflow run release.yml --repo tastile/tastile-desktop --ref $Ref --field version=$Version
if ($LASTEXITCODE -ne 0) {
    throw "GitHub Actions workflow dispatch failed."
}
