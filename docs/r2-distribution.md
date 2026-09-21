# Desktop distribution on Cloudflare R2

The desktop release workflow publishes directly to the R2 S3-compatible API.
The old S3 transfer bucket, presigned URL, SSM, and nginx path are not part of
the distribution path anymore.

Create an R2 bucket and a scoped API token that can read and write only that
bucket. Attach the custom domain `download.tastile.app` to the bucket and
verify that both the immutable artifact path and the stable manifest path are
publicly readable:

```text
https://download.tastile.app/releases/desktop/<version>/tastile-desktop-<version>-setup.exe
https://download.tastile.app/channels/stable/desktop.json
```

The legacy manifest URL `/updates/desktop/manifest.json` is published as the
same stable manifest during the migration window so already-installed clients
continue to receive updates.

Configure these GitHub Environment (`production`) secrets:

```text
CLOUDFLARE_ACCOUNT_ID
CLOUDFLARE_R2_BUCKET
CLOUDFLARE_R2_ACCESS_KEY_ID
CLOUDFLARE_R2_SECRET_ACCESS_KEY
DOWNLOAD_PUBLIC_BASE_URL=https://download.tastile.app
```

The existing `AWS_OIDC_ROLE_PRODUCTION` secret is used only by the reusable
SOPS decryption job that prepares the Windows build environment. It is not
used to publish or serve desktop artifacts.

Release order is deliberately one-way:

1. conditionally upload the versioned installer, rejecting an existing key;
2. fetch the public installer and compare SHA-256;
3. update `channels/stable/desktop.json` last;
4. fetch the public manifest and verify its pointer and hash;
5. attach the same installer to the GitHub Release.

To roll back, upload or restore a previously verified stable manifest that
points to an existing immutable artifact. Do not delete or overwrite the
versioned installer.
