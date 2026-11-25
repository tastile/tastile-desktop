# Microsoft Store submission preparation (desktop#62 / NF04-D10)

Status: **preparation scaffold**. This document collects everything needed to
submit Tastile Desktop to the Microsoft Store / MSIX channel. **No Partner
Center account creation, package submission, or public listing happens
without explicit approval** — those steps are approval-gated.

Prerequisite: the MSIX packaging scaffold from desktop#61
(`docs/msix-packaging.md`, `scripts/package-msix.ps1`,
`.github/workflows/msix.yml`) must produce a clean-machine-verified package
first.

## 1. Account eligibility

| Item | Status / action |
| --- | --- |
| Partner Center account | Not yet created. Choose **Individual** vs **Company** — Company requires business verification documents; Individual requires identity verification. Decision needed before registration. |
| Account cost | Verify current Partner Center pricing at registration time (fees have changed; do not assume). Record the actual cost in the release budget — ADR-0025 caps *cloud* spend at ¥0 but Store fees are a separate line item. |
| Developer name | Reserve the app name "Tastile" early — names are first-come. |
| Payout / tax | Not needed for a free app, but the account profile still requires contact and (for Company) tax/banking info if paid features are ever added. |

## 2. Package identity

`Package.appxmanifest` currently ships placeholders that must be replaced
before Store submission:

- `Identity/Publisher="CN=Tastile"` → must become the **Publisher CN assigned
  by Partner Center** for the developer account (shown in Partner Center →
  Product identity). Partner Center can also generate the full identity block.
- `Identity/Name="Tastile.ExecutionControl"` → may be kept or aligned with the
  Store product identity; keep it stable once shipped (updates must keep the
  same identity).
- Version stays 4-part numeric (`0.7.0.0`) — already enforced by
  `InstallerConfigTests` and the release workflow.

For Store submissions, Partner Center can inject the correct publisher at
packaging time; for direct MSIX sideloading the manifest publisher must match
the signing certificate subject.

## 3. Signing

| Path | Signing |
| --- | --- |
| Microsoft Store | Partner Center re-signs the package with the Store certificate. Upload an **unsigned or dev-signed** `.msix`/`.msixbundle`; do not buy a certificate for the Store path. |
| Direct MSIX download | Requires a standard or EV code-signing certificate trusted by SmartScreen — a real cost; only pursue if Store is not the primary channel. |

No certificate purchase is authorized by this scaffold.

## 4. Update mechanism

- Store-delivered builds update through the Store client automatically;
  in-app `AppUpdateService` polling GitHub Releases is designed for the
  unpackaged/Inno Setup channel.
- **Open decision** (tracked in `docs/msix-packaging.md` checklist): packaged
  builds should detect package identity and either disable the in-app updater
  or point it at the Store channel. Do not let a packaged build silently
  sideload an unsigned `.exe` update.

## 5. Store listing / product information

Draft fields to prepare (fill before submission):

- **Description** (short + long), keywords, category (Productivity).
- **Screenshots**: at least one set per required device family; capture from
  the packaged build (Start menu tile, main window, tray, intervention
  dialog).
- **Privacy policy URL**: required — the app handles account credentials and
  schedule data. Host on the product site (Cloudflare Pages, per ADR-0025).
- **Support contact**: support URL/email.
- **Age rating**: complete the questionnaire (no objectionable content
  expected; account sign-in does not change the rating path).
- **Declarations**: app communicates with `tastile-core` API over HTTPS;
  declares `internetClient`; uses notifications and background-ish tray
  presence — answer the Store's "background activity" questions accurately.
- **Accessibility / UI notes**: include notes about the intervention dialog
  and tray-resident behavior for reviewers.

## 6. Review requirements checklist

- [ ] Package installs and launches on a clean Windows 10/11 machine.
- [ ] All declared capabilities match actual usage (`internetClient`,
      `runFullTrust` is a restricted capability — justify in the submission
      notes; full-trust desktop apps are an accepted packaged-app pattern).
- [ ] No crashes on launch, sign-in, or core flows during certification.
- [ ] Privacy policy URL live and matches data practices.
- [ ] Account-free trial path or clear sign-in requirement documented for
      reviewers (provide a test account if certification requires one).
- [ ] Listing metadata complete (description, screenshots, support contact).

## 7. Approval boundaries

- Creating the Partner Center account, reserving the name, submitting packages,
  and responding to certification are **explicit-approval** actions.
- This document and the packaging scaffold are the preparation deliverable;
  submission itself is tracked as a separate, human-gated step.
