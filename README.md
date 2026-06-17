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
- Thin presentation layer over the `tastile-core` API (no local daemon)

## Prerequisites

- Windows 11
- .NET SDK `10.0.104` or newer in the same feature band
- AWS Cognito Hosted UI credentials (Google OAuth federated identity) — see `CLAUDE.md`
- Inno Setup 6 for installer builds

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
$env:TASTILE_API_BASE_URL="http://127.0.0.1:3140"
dotnet run --project .\src\TastileDesktop\TastileDesktop.csproj
```

Create a release installer:

```powershell
.\scripts\build-desktop-installer.ps1 -Version 0.2.0
```

Use the same version for the app build, installer filename, and hosted update manifest so the desktop client can compare versions correctly.

## Update publication

Installer upload and update-manifest publication are handled by the GitHub Actions workflow:

- `.github/workflows/publish-update-manifest.yml`

The workflow runs on GitHub release publication and also supports manual `workflow_dispatch`. For release events, it derives the version from the release tag, attaches the matching installer to the GitHub Release, uploads the same installer to hosted storage, and writes the hosted `manifest.json`.

The app checks a hosted `manifest.json` and opens the installer download URL when an update is available.

## Architecture notes

The desktop client is intentionally thin. Scheduling, prompting, and execution decisions belong to the `tastile-core` API; the WinUI app is responsible for:

- rendering daemon state
- surfacing prompts and interventions
- handling desktop-only UX such as tray, overlays, and startup integration

The desktop connects to a `tastile-core` API instance via `TASTILE_API_BASE_URL` (default `https://beta.tastile.app`). In production the API is the EC2-hosted `tastile-core` daemon; in dev it can point at `http://localhost:3140`. Auth uses AWS Cognito Hosted UI (Google OAuth federated identity) — see `CLAUDE.md` for the connection model and env-var key list.

## Contribution flow

See [CONTRIBUTING.md](CONTRIBUTING.md) for repository conventions, validation expectations, and release hygiene.
