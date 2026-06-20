param(
    [Parameter(Mandatory = $true)][string]$LatestVersion,
    [Parameter(Mandatory = $true)][string]$DownloadUrl,
    [Parameter(Mandatory = $false)][string]$Notes = "",
    [Parameter(Mandatory = $false)][string]$Platform = "desktop",
    [Parameter(Mandatory = $false)][string]$StoragePath = "",
    [Parameter(Mandatory = $false)][string]$AppStoragePath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Trigger workflow: Publish update manifest"
Write-Host "Use GitHub Actions workflow_dispatch for ad hoc publishes, or create a GitHub Release to publish automatically."
Write-Host "workflow_dispatch inputs:"
Write-Host "  platform       = $Platform"
Write-Host "  latest_version = $LatestVersion"
Write-Host "  download_url   = $DownloadUrl"
Write-Host "  notes          = $Notes"
if ([string]::IsNullOrWhiteSpace($StoragePath)) {
    Write-Host "  storage_path   = (empty => updates/$Platform/manifest.json)"
} else {
    Write-Host "  storage_path   = $StoragePath"
}
if ([string]::IsNullOrWhiteSpace($AppStoragePath)) {
    Write-Host "  app_storage_path = (empty => $Platform/tastile-$Platform-$LatestVersion.exe)"
} else {
    Write-Host "  app_storage_path = $AppStoragePath"
}
Write-Host ""
Write-Host "Required repo secrets:"
Write-Host "  AWS_ACCESS_KEY_ID"
Write-Host "  AWS_SECRET_ACCESS_KEY"
Write-Host "  S3_UPDATE_BUCKET"
Write-Host "  CORE_REPO_READ_TOKEN"
