# MSIX packaging (desktop#61 / NF04-D09)

Status: **scaffold**. This document records the packaged-build design and the
manual verification checklist. Clean-machine install verification, signing,
and Store submission remain open work — see "Approval boundaries" below.

## Build

```powershell
# Unsigned dev package (sideload test only)
.\scripts\package-msix.ps1

# Dev-signed package (requires a trusted code-signing certificate thumbprint)
.\scripts\package-msix.ps1 -CertThumbprint <thumbprint>
```

The script runs `dotnet publish` with `AppxPackage=true` and
`WindowsPackageType=MSIX`; the project defaults stay unpackaged
(`AppxPackage=false`, `WindowsPackageType=None`) for development. Output lands
in `artifacts\msix\tastile-desktop-<version>-<rid>.msix`.

A manual CI entry point is `.github/workflows/msix.yml`
(`workflow_dispatch` only) which builds the unsigned package on the
self-hosted Windows runner and uploads it as a workflow artifact.

## Packaged OS integration design

`src/TastileDesktop/Package.appxmanifest` declares what the packaged build
needs; `Identity` version is kept in sync with the csproj by
`InstallerConfigTests`.

| Feature | Packaged mechanism | Code path |
| --- | --- | --- |
| Launch at startup | `windows.startupTask` extension (`TaskId="TastileStartup"`, default disabled so the user opts in) | `StartupRegistrationService.TryApplyStartupTask` uses `Windows.ApplicationModel.StartupTask`; unpackaged builds fall back to the HKCU Run key |
| `tastile://` protocol | `windows.protocol` extension (`Name="tastile"`) | `App.OnLaunched` reads `AppInstance.GetActivatedEventArgs()` for `ProtocolActivatedEventArgs` and folds the URI into the existing legacy-callback drain; unpackaged builds keep the HKCU `Software\Classes\tastile` registration in `ProtocolHandler` |
| Toast notifications | Unchanged: `Microsoft.Toolkit.Uwp.Notifications` toast XML + AUMID | Packaged builds get the identity from the package; verify `ToastNotificationManagerCompat` activation still reaches `NotificationService` (toast activator CLSID may need a `windows.toastNotificationActivation` extension — see checklist) |
| Tray icon | Unchanged: `H.NotifyIcon.WinUI` + `TrayIconService` | Verify the tray icon survives packged-process lifetime rules (no console/window station surprises) |
| Forced intervention dialog | Unchanged: full-screen `InterventionWindow` | Verify topmost/z-order behavior inside the packaged app container |
| Full trust | `runFullTrust` restricted capability | Required so registry, named pipes, WebView2 user-data folder, and SQLite paths behave like the unpackaged build |

## Manual verification checklist (clean Windows machine)

Run each item on a machine **without** the Inno Setup build installed, using
either a dev-signed MSIX or the Store package once available.

1. **Install**: `Add-AppxPackage .\tastile-desktop-<version>-x64.msix` succeeds
   on a clean profile; app appears in Start menu with the declared logos.
2. **First launch**: app starts, sign-in flow completes, main window + tray
   icon appear.
3. **Tray**: right-click menu works; "Open"/"Quit" behave as unpackaged;
   closing the window keeps the tray resident (per desktop#60 lifecycle).
4. **Toast**: trigger a schedule notification; toast appears with action
   buttons; clicking a button activates the app and dispatches the action.
5. **Intervention dialog**: trigger an intervention; the unavoidable dialog
   renders above other windows and cannot be dismissed accidentally.
6. **Protocol callback**: run `start tastile://auth/callback?code=test` (or the
   current deep-link shape); the packaged app activates and receives the URI
   via `ProtocolActivatedEventArgs` (check `%TEMP%\tastile-desktop.log`).
7. **Auto-start**: enable "Launch at startup" in Settings; confirm the entry
   appears under Task Manager → Startup apps as `TastileStartup`; reboot and
   confirm the app starts minimized; disable and confirm it no longer starts.
8. **Update path**: confirm whether in-app update should be disabled for the
   Store/MSIX channel (Store self-updates) — decision needed before release.

## Signing and distribution eligibility

- **Dev/sideload**: self-signed cert installed into `TrustedPeople` on the
  test machine; never ship a dev cert.
- **Store**: Partner Center resigns with the Store certificate; see
  `docs/store-submission.md` (desktop#62).
- **Direct MSIX**: requires a standard/EV code-signing certificate — a cost
  item to confirm before choosing this path (desktop#65 decision).

## Approval boundaries

- Purchasing signing certificates, creating Partner Center / Store accounts,
  and any public submission are **not** done by this scaffold.
- `package-msix.ps1` and `msix.yml` produce unsigned artifacts for
  verification only.
