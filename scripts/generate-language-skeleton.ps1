# Copies the default (English) resx content into the 3 new language files
# for every section. This sets up the resource-manager scaffolding so the
# language picker can offer de / fr / pt-BR while translations are filled
# in incrementally. The QuickCreate section is then overwritten with the
# hand-crafted translations from translate-quickcreate.ps1.

param(
    [string]$RepoRoot = "C:\Users\rebui\Desktop\tastile\tastile-desktop"
)

$resourcesRoot = Join-Path $RepoRoot "src\TastileDesktop\Resources"
$newLangs = @('de', 'fr', 'pt-BR')

# Find all *.<section>.resx default files (no culture suffix)
$sectionDirs = Get-ChildItem -Path $resourcesRoot -Recurse -Directory | Where-Object {
    $_.Name -in @('App', 'Features', 'System')
}

$defaultFiles = foreach ($dir in $sectionDirs) {
    Get-ChildItem -Path $dir.FullName -File -Filter '*.resx' | Where-Object { $_.Name -notmatch '\.(en|ja|zh-CN|ko|es|de|fr|pt-BR)\.resx$' }
}

Write-Host "Default resx files: $($defaultFiles.Count)"
foreach ($defaultFile in $defaultFiles) {
    $baseName = $defaultFile.BaseName
    foreach ($lang in $newLangs) {
        $targetPath = Join-Path $defaultFile.DirectoryName "$baseName.$lang.resx"
        if (-not (Test-Path $targetPath)) {
            Copy-Item -Path $defaultFile.FullName -Destination $targetPath
            Write-Host "Created: $targetPath"
        }
        else {
            Write-Host "Skipped (exists): $targetPath"
        }
    }
}
