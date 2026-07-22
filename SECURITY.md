# Security Policy

## Supported Versions

| App version | Supported |
| ----------- | --------- |
| latest 0.x.y | ✅ Active |
| <latest 0.x.y | ❌ EOL    |

Only the latest `0.x.y` version receives security backports. The desktop
client does NOT auto-update silently; users opt in.

## Reporting a Vulnerability

**Please do not file public GitHub issues for security vulnerabilities.**

Use one of these channels:

1. **GitHub Security Advisories** (preferred; private thread with the
   maintainers): https://github.com/tastile/tastile-desktop/security/advisories/new
2. **X (Twitter) DM** to `@361do_sleep` for urgent pre-disclosure matters.

We acknowledge within 2 business days and aim to ship a fix within 30 days
for high-impact issues.

## Security Scope

This client is a thin shell over a user-provided `tastile-core` instance.
Auth boundaries (Cognito, session tokens) are configured at the
`tastile-core` instance; security issues in this repo are limited to:

* The WinUI 3 / WebView2 surface (DOM XSS, content-injection)
* The auto-update client (manifest signing, manifest re-use)
* Local credential storage (DPAPI / Credential Manager)

The Windows installer **signing certificate is NOT shipped in this repo.**
Releases are signed with a hardware token controlled by the release team
out-of-band; clones must either use their own certificate or run unsigned
(Windows SmartScreen will warn).

The hosted demo at `*.demo.tastile.app` runs in an isolated AWS account
with rate limits and a kill-switch.
