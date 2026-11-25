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
- Windows App SDK 1.8
- .NET 9 application target, built with SDK pinned in `global.json`
- CommunityToolkit.Mvvm
- H.NotifyIcon.WinUI
- Thin presentation layer over the `tastile-core` API (no local daemon)

## Prerequisites

- Windows 11
- .NET SDK `10.0.104` or newer in the same feature band
- Better Auth credentials (email+password, obtained from the web login surface) — see `CLAUDE.md`
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
infisical --domain=https://secrets.rebuildup.dev run --env=dev --path=/tastile/desktop -- dotnet run --project .\src\TastileDesktop\TastileDesktop.csproj
```

Authenticate to Infisical with `infisical login` before building. Run the
installer build with the shared development values so local builds use the
same remote source as CI:

```powershell
infisical --domain=https://secrets.rebuildup.dev run --env=dev --path=/tastile/desktop -- pwsh -File .\scripts\build-desktop-installer.ps1 -Version 0.2.0
```

Create a release installer:

```powershell
.\scripts\build-desktop-installer.ps1 -Version 0.2.0
```

Use the same version for the app build, installer filename, and hosted update manifest so the desktop client can compare versions correctly.

## Update publication

Installer upload and update-manifest publication are handled by
`.github/workflows/release.yml`. Per ADR-0025 and desktop#65, **GitHub
Releases is the canonical distribution channel**: the workflow uploads the
installer and a `manifest.json` asset (whose `download_url` points at the
immutable release asset) to the GitHub Release and verifies both.

The app polls the canonical feed:

```text
https://github.com/tastile/tastile-desktop/releases/latest/download/manifest.json
```

`releases/latest/download/` always resolves to the newest release, so no
separate stable pointer has to be maintained. Override with
`TASTILE_DESKTOP_UPDATE_BASE_URL` / `TASTILE_UPDATE_URL` when needed.

For the migration window, the workflow also mirrors the installer and the
legacy R2 stable manifests (`channels/stable/desktop.json`,
`updates/desktop/manifest.json`) **only when the R2 credentials are present**
in Infisical. R2 is an optional interim mirror for pre-cutover clients, not a
required dependency — see `docs/r2-distribution.md`.

The GitHub Release is the archival source. A rollback republishes or marks a
previous release as latest, or uploads a corrected `manifest.json`; versioned
installer assets are never deleted or overwritten.

## Architecture notes

The desktop client is intentionally thin. Scheduling, prompting, and execution decisions belong to the `tastile-core` API; the WinUI app is responsible for:

- rendering daemon state
- surfacing prompts and interventions
- handling desktop-only UX such as tray, overlays, and startup integration

The desktop connects to a `tastile-core` API instance via `TASTILE_API_BASE_URL` (no default; `https://api.tastile.app` in production, `https://staging-api.tastile.app` in staging). In production the API is the EC2-hosted `tastile-core` daemon; in dev it can point at `http://localhost:3140`. Auth uses Better Auth (email+password through the web login surface; social sign-in is disabled) or system-browser Authorization Code + PKCE (`client_id=tastile-desktop`, scoped Core API token via `/api/cli/token`) — see `CLAUDE.md` for the connection model and env-var key list.

## Contribution flow

See [CONTRIBUTING.md](CONTRIBUTING.md) for repository conventions, validation expectations, and release hygiene.
