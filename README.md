# Tastile Desktop

Windows native client for the Tastile execution control system.

## What this repository contains

- `src/TastileDesktop`: WinUI 3 desktop application
- `tests/TastileDesktop.Tests`: unit tests for client-side logic
- `installer`: Inno Setup packaging assets
- `scripts`: local validation and release helper scripts
- `.github/workflows`: CI and release automation

## Tech stack

- C# / WinUI 3
- Windows App SDK 1.7
- .NET 9 application target, built with SDK pinned in `global.json`
- CommunityToolkit.Mvvm
- H.NotifyIcon.WinUI
- Rust daemon integration via sibling `tastile-core` checkout

## Prerequisites

- Windows 11
- .NET SDK `10.0.104` or newer in the same feature band
- Rust stable toolchain with `x86_64-pc-windows-msvc`
- Inno Setup 6 for installer builds
- `tastile-core` checked out as a sibling directory:

```text
../tastile-core
../tastile-desktop
```

## Local development

Run unit tests only:

```powershell
.\scripts\check.ps1 -SkipDesktopBuild
```

Run the standard local validation suite:

```powershell
.\scripts\check.ps1
```

`check.ps1` runs unit tests and then validates both desktop build targets after cleaning stale build artifacts

- default output build (same path used by `dotnet run`)
- `-r win-x64` build (packaging/runtime parity)
- generated `TimelineWindow.g.cs` connector safety check so toolbar handlers are not rewired into XAML connector casts

The same connector safety check is also enforced directly in `src/TastileDesktop/TastileDesktop.csproj` during normal desktop builds

Build the desktop app directly:

```powershell
dotnet build .\src\TastileDesktop\TastileDesktop.csproj -r win-x64
```

Run the desktop app:

```powershell
dotnet run --project .\src\TastileDesktop\TastileDesktop.csproj
```

### Runtime profile separation

Desktop supports profile isolation via environment variables

- `TASTILE_PROFILE=prod` (default) uses `%APPDATA%\Tastile`
- `TASTILE_PROFILE=dev` uses `%APPDATA%\Tastile-dev`

For local unpackaged runs we recommend

```powershell
$env:TASTILE_PROFILE="dev"
$env:TASTILE_DAEMON_PORT="3141"
dotnet run --project .\src\TastileDesktop\TastileDesktop.csproj
```

Optional profile-scoped secrets to avoid mixing production credentials

- `TASTILE_DEV_SUPABASE_URL`
- `TASTILE_DEV_SUPABASE_PUBLISHABLE_KEY` (recommended)
- `TASTILE_DEV_SUPABASE_ANON_KEY`
- `TASTILE_DEV_TASTILE_UPDATE_URL`

Create a release installer:

```powershell
.\scripts\build-desktop-installer.ps1 -Version 0.2.0
```

Use the same version for the app build, installer filename, and hosted update manifest so the desktop client can compare versions correctly.

## Update publication

Installer upload and update-manifest publication are handled by:

- `.github/workflows/publish-update-manifest.yml`
- `scripts/publish-update-manifest.ps1`

The workflow runs on GitHub release publication and also supports manual `workflow_dispatch`. For release events, it derives the version from the release tag, uploads the matching installer, and writes the hosted `manifest.json`.

The app checks a hosted `manifest.json` and opens the installer download URL when an update is available.

## Architecture notes

The desktop client is intentionally thin. Scheduling, prompting, and execution decisions belong to the daemon; the WinUI app is responsible for:

- rendering daemon state
- surfacing prompts and interventions
- handling desktop-only UX such as tray, overlays, and startup integration
- packaging the daemon alongside the app for local execution

## Contribution flow

See [CONTRIBUTING.md](CONTRIBUTING.md) for repository conventions, validation expectations, and release hygiene.
