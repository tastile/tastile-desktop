Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$searchPaths = @(
    Join-Path $repoRoot "src/TastileDesktop/MainWindow.xaml"
    Join-Path $repoRoot "src/TastileDesktop/App.xaml"
    Join-Path $repoRoot "src/TastileDesktop/Styles"
    Join-Path $repoRoot "src/TastileDesktop/Views"
)
$excludeRegex = '\\(bin|obj)\\'

# R10 fix: matches \bin\ or \obj\ path segments via case-insensitive containment.
# R11 narrow scope: only check hex literals; defer removed-brush refs to Phase 2.
# R12 fix: the hex regex requires the `#` to be preceded by a brush-attribute name
# (Background, Foreground, BorderBrush, Fill, Color, Stroke, etc.) followed by `="`.
# This excludes string-content attributes like `PlaceholderText="#0078D4"` where the
# `#` is inside a quoted string but is not a brush value. Earlier attempt used just
# `="` as the boundary, which incorrectly matched string content.
$hexPattern = '(?<=(?:Background|Foreground|BorderBrush|Fill|Color|Stroke)\s*=\s*")#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?'

$failures = New-Object System.Collections.Generic.List[string]

function Test-File {
    param([string]$Path)
    $content = Get-Content -Raw -Path $Path
    $rel = $Path.Substring($repoRoot.Length).TrimStart('\', '/')
    $isAppXaml = $rel -ieq "src\TastileDesktop\App.xaml"

    if (-not $isAppXaml) {
        $hexMatches = [regex]::Matches($content, $hexPattern)
        foreach ($m in $hexMatches) {
            $lineNumber = ($content.Substring(0, $m.Index) -split "`n").Count
            $failures.Add("$rel`:$lineNumber`: literal hex '$($m.Value)' (use ThemeResource instead).") | Out-Null
        }
    }
}

function Search-Files {
    param([string[]]$Paths)
    foreach ($p in $Paths) {
        if (-not (Test-Path -LiteralPath $p)) { continue }
        if ((Get-Item -LiteralPath $p).PSIsContainer) {
            Get-ChildItem -LiteralPath $p -Recurse -Filter "*.xaml" -File |
                Where-Object { $_.FullName -notmatch $excludeRegex } |
                ForEach-Object { Test-File -Path $_.FullName }
        } else {
            if ($p -notmatch $excludeRegex) {
                Test-File -Path $p
            }
        }
    }
}

Search-Files -Paths $searchPaths

if ($failures.Count -gt 0) {
    Write-Host "==> UI token checks FAILED"
    $failures | ForEach-Object { Write-Host "    $_" }
    exit 1
}

Write-Host "==> UI token checks passed (0 hex literals outside App.xaml)"
exit 0
