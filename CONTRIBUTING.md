# Contributing

## Development prerequisites

- Windows 11
- .NET SDK `10.0.104` or newer in the same feature band
- Rust stable toolchain with `x86_64-pc-windows-msvc`
- Inno Setup 6 for installer builds
- A sibling checkout of `tastile-core` at `../tastile-core`

## Repository conventions

- Keep generated files out of Git. Build outputs, binlogs, test results, and local certificates are ignored.
- Treat `src/TastileDesktop` as the app project root and `tests/TastileDesktop.Tests` as the unit-test project.
- Do not hardcode local absolute paths in scripts or docs.
- Update docs when build, release, or developer setup changes.

## Local validation

Run the shared validation script before pushing changes:

```powershell
.\scripts\check.ps1
```

The validation script is strict by default and includes unit tests dual desktop builds (default and `win-x64`) after cleaning desktop `obj/bin` and generated connector safety checks for `TimelineWindow`

If you do not have a sibling `tastile-core` checkout, run unit tests only:

```powershell
.\scripts\check.ps1 -SkipDesktopBuild
```

## Release workflow

- Installer packaging uses [installer/TastileDesktop.iss](installer/TastileDesktop.iss).
- Manual update publication uses `.github/workflows/publish-update-manifest.yml`.
- Shipping changes should include:
  - passing unit tests
  - a successful desktop build
  - updated release notes or docs if user-facing behavior changed
