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

Build the desktop app directly:

```powershell
dotnet build .\src\TastileDesktop\TastileDesktop.csproj -r win-x64
```

Run the desktop app:

```powershell
dotnet run --project .\src\TastileDesktop\TastileDesktop.csproj
```

Create a release installer:

```powershell
.\scripts\build-desktop-installer.ps1 -Version 0.1.0
```

## Update publication

Installer upload and update-manifest publication are handled by:

- `.github/workflows/publish-update-manifest.yml`
- `scripts/publish-update-manifest.ps1`

The app checks a hosted `manifest.json` and opens the installer download URL when an update is available.

## Architecture notes

The desktop client is intentionally thin. Scheduling, prompting, and execution decisions belong to the daemon; the WinUI app is responsible for:

- rendering daemon state
- surfacing prompts and interventions
- handling desktop-only UX such as tray, overlays, and startup integration
- packaging the daemon alongside the app for local execution

## Contribution flow

See [CONTRIBUTING.md](CONTRIBUTING.md) for repository conventions, validation expectations, and release hygiene.
