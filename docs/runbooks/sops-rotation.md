# sops Rotation Procedure

1. **Update plain text locally**

   ```bash
   aws sso login
   sops --decrypt .env.development.sops > .env.development
   ```

2. **Edit the plain file** to reflect the new secret(s).

3. **Re-encrypt in place**

   ```bash
   sops --encrypt --in-place --kms "arn:aws:kms:ap-northeast-1:<account>:key/<dev-key-id>" .env.development
   ```

4. **Verify diff**

   ```bash
   git diff .env.development.sops
   ```

   Reviewer confirms the `ENC[...]` block changed and not unrelated keys.

5. **Open PR**. The `sops-decrypt --check` job verifies decryptability without
   emitting plaintext. After merge, the next CI run on `develop` regenerates
   the artifact.

6. **Cross-repo secret drift check**

   If the rotated value is `TASTILE_WEB_BRIDGE_SECRET` or another cross-repo
   secret, repeat steps 1-5 in every consumer repo on the same day. Use
   `gh search code "TASTILE_WEB_BRIDGE_SECRET"` to enumerate consumers.

7. **Production deploy**: the release workflow consumes the artifact and
   ships the new plain file via the existing systemd EnvironmentFile deploy
   path. Confirm via `sudo systemctl show tastile-web -p EnvironmentFiles`.

## Rollback

`sops` decrypt with stale ciphertext → AccessDenied or wrong plaintext.
Re-encrypt from a known-good backup of the old plain file; the diff will
revert to the previous ciphertext. Force a new release.