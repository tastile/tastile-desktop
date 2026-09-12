<!-- .github/PULL_REQUEST_TEMPLATE.md
     Tastile Desktop PR template.
     See `AGENTS.md` "Reviewer policy" section and PROMPT.ja.md §26. -->

## Summary

<!-- 1–3 sentence summary of the change -->

## Linked Issue

- #

## Target Release

- release branch: `release-`
- version: `0.0.0`

## Type of change

- [ ] Bug fix (non-breaking)
- [ ] New feature (non-breaking)
- [ ] Breaking change — flag `breaking-change` label
- [ ] Documentation only
- [ ] Build / CI / release infra

## Reviewer policy (PROMPT.ja.md §26)

`@rebuildup` is the only contributor with merge authority on
`tastile/tastile-desktop`. There is no separate human reviewer available.
Per the canonical contract, the following alternative review path is in force:

- **AI reviewers** — Copilot + coderabbitai (configured at repo level)
- **Required status checks** — `verify-head` (release-X-Y-Z pattern on
  `main`-targeting PRs) + CI
- **Manual verification** — `verify-tastile-change` Skill invoked immediately
  before marking ready-to-merge
- **Final review** — PR author self-attests via the verification steps above;
  the lack of a separate human reviewer is recorded honestly here

## Acceptance criteria

<!-- Copy from the linked Issue body. Mark each item complete or out-of-scope. -->
- [ ]

## Test plan

- [ ] `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check.ps1 -SkipDesktopBuild` passes
- [ ] (if RID-specific) `dotnet build src/TastileDesktop/TastileDesktop.csproj -r win-x64` passes
- [ ] New / changed ViewModel, service, auth, tray, notification, or Core API surface has a focused xUnit test
- [ ] No embedded server credential, signing certificate, refresh token, or `*.pfx` in source, resources, `.csproj`, MSIX manifest, or published artifact

## Risk / rollback

<!--
  - Risk surface: auth, business logic moved from Core, public contract change
  - Rollback plan: revert commit (no schema migration / signing / publish)
-->

## Out of scope

<!-- Explicit non-scope so reviewers do not expect the change. -->
