# Desktop distribution: GitHub Releases canonical, R2 interim mirror

Per ADR-0025 and desktop#65, **GitHub Releases is the canonical distribution
and update channel** for the Windows desktop app. Microsoft Store / MSIX is
the general-audience distribution path (see `docs/msix-packaging.md` and
`docs/store-submission.md`); GitHub Releases remains the archival and
update-metadata source.

The release workflow (`.github/workflows/release.yml`) therefore:

1. builds the Inno Setup installer on the Windows runner;
2. materializes `manifest.json` whose `download_url` points at the immutable
   GitHub Release asset
   `https://github.com/tastile/tastile-desktop/releases/download/v<version>/tastile-desktop-<version>-setup.exe`;
3. uploads the installer **and** `manifest.json` to the GitHub Release and
   verifies both assets;
4. optionally mirrors the installer and stable manifests to Cloudflare R2.

## Canonical update feed

The desktop app polls:

```text
https://github.com/tastile/tastile-desktop/releases/latest/download/manifest.json
```

`releases/latest/download/` always resolves to the newest published release,
so the "latest" pointer is the GitHub Release itself — no separate stable
manifest has to be maintained or repointed. Override the feed with
`TASTILE_DESKTOP_UPDATE_BASE_URL` (full manifest URL or base URL) or the
runtime `TASTILE_UPDATE_URL` variable when needed.

## Optional R2 interim mirror

Clients installed before the GitHub Releases cutover still poll the legacy
R2 manifests. The workflow keeps publishing them **only while the R2
credentials are present** in Infisical at `/tastile/desktop`:

```text
CLOUDFLARE_ACCOUNT_ID
CLOUDFLARE_R2_BUCKET
CLOUDFLARE_R2_ACCESS_KEY_ID
CLOUDFLARE_R2_SECRET_ACCESS_KEY
DOWNLOAD_PUBLIC_BASE_URL=https://download.tastile.app
```

A `Check optional R2 mirror configuration` step probes for all of these
values; when any is missing or `DOWNLOAD_PUBLIC_BASE_URL` is not HTTPS, every
R2 step is skipped and the release still succeeds through GitHub Releases
alone. R2 is therefore no longer a required dependency.

When enabled, the mirror preserves the previous one-way order:

1. conditionally upload the versioned installer with `--if-none-match '*'`;
2. fetch the public installer and compare SHA-256;
3. update `channels/stable/desktop.json` and `updates/desktop/manifest.json`
   last, from `r2-manifest.json` (identical schema, R2 download URL);
4. fetch the public manifests and verify pointer and hash.

## Migration safety

- **Old clients** (pre-cutover, polling `download.tastile.app`) keep receiving
  updates as long as the R2 mirror stays enabled. Their next update moves them
  to a build whose default feed is GitHub Releases.
- **New clients** never touch R2; removing the R2 secrets from Infisical only
  disables the mirror.
- The R2 mirror can be retired once the installed base has moved past the
  cutover release; until then keep the secrets in place.

## Rollback

- Canonical: mark or re-publish a previous GitHub Release as latest, or
  upload a corrected `manifest.json` to the current release. Never delete the
  versioned installer asset.
- Mirror: restore a previously verified stable manifest that points to an
  existing immutable R2 artifact. Do not delete or overwrite versioned
  installer objects.
