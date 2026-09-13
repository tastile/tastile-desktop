# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

# Tastile Desktop

Windows native client for Tastile execution control system. Connects to the
AWS-hosted `tastile-core` API with native BetterAuth email/password sign-in;
no local daemon process.

## Tech Stack
- C# / WinUI 3 (Windows App SDK 1.8)
- Target framework `net9.0-windows10.0.26100.0`, SDK pinned via `global.json` (`rollForward: latestFeature`)
- CommunityToolkit.Mvvm, H.NotifyIcon.WinUI (tray), Microsoft.Toolkit.Uwp.Notifications (toast)
- `System.Security.Cryptography.ProtectedData` (DPAPI), System.Text.Json (API)
- Tests: xUnit + `Microsoft.NET.Test.Sdk`

## Architecture

```
TastileDesktop/                                AWS remote API
├── App.xaml.cs                               ──HTTPS + Bearer JWT──▶  beta.tastile.app  ─▶  tastile-core
├── Services/                                                                       (no local daemon)
│   ├── CoreApiClient.cs            # Bearer + 401-refresh-retry HTTPS client
│   ├── BetterAuthAuthService.cs    # Native BetterAuth session + refresh + signout\n│   ├── BetterAuthHttpClient.cs     # BetterAuth HTTP endpoints + API-token bridge
│   ├── SecureTokenStore.cs         # DPAPI-protected credentials
│   ├── EventDrivenPoller.cs        # User-action / focus / idle refresh (no wall-clock tick)
│   ├── AuthService.cs              # Facade over BetterAuthAuthService
│   ├── AppSettings.cs              # env-var-driven runtime config
│   ├── InterventionEngine.cs       # Escalation logic (toast → intervention)
│   ├── NotificationService.cs      # Windows toast notifications
│   ├── SettingsService.cs          # JSON settings persistence (%APPDATA%/Tastile)
│   └── TrayIconService.cs          # System tray icon + context menu
├── Models/
│   ├── ApiModels.cs                # AWS API DTOs
│   └── AuthSession.cs              # id_token / refresh_token / sub / email / exp
├── ViewModels/
│   ├── MainViewModel.cs            # Main window state + commands
│   └── SettingsViewModel.cs        # Settings form binding
└── Views/
    ├── AuthWindow.xaml             # Single-step "sign in" entry point
    ├── SettingsWindow.xaml         # Settings + runtime paths panel
    └── InterventionWindow.xaml     # Unavoidable full-screen dialog
```

Sibling support dirs under `src/TastileDesktop/`: `Controls/`, `Converters/`,
`Helpers/`, `Styles/`, `Properties/`, `Assets/`. `ProtocolHandler.cs` lives at
the project root and handles `tastile://` URL activation.

`AppUpdateService` + `AppUpdateServiceTests` add a hosted-manifest update
pipeline (see `scripts/publish-update-manifest.ps1` and
`.github/workflows/release.yml`); this is the only piece of state the desktop
owns outside the API.

## Connection Model

- **Auth**: BetterAuth native email/password flow. Session/API tokens are saved
  via DPAPI in `%LOCALAPPDATA%\Tastile\Auth\credentials.bin`.
  `CoreApiClient` adds the BetterAuth session token as Bearer and retries once
  on 401 after validating the session.
- **API base URL**: `TASTILE_API_BASE_URL` (default `https://beta.tastile.app`).
  For local dev, set `TASTILE_API_BASE_URL=http://127.0.0.1:3140`.
- **Web auth base URL**: `TASTILE_WEB_BASE_URL` (see `AppSettings`). Social
  sign-in is intentionally not exposed until an installed-app handoff can
  persist the BetterAuth session.
- **Refresh strategy**: `EventDrivenPoller` issues the 4-endpoint refresh
  bundle only in response to (a) user commands, (b) window activation
  (`MainWindow.Activated` / `TilesWindow.Activated` with 1s debounce), or
  (c) a single `DispatcherQueueTimer` after `TASTILE_POLL_IDLE_SECONDS`
  (default 60, set 0 to disable). No background child process. No 1s tick.
- **SSE**: opt-in via `TASTILE_ENABLE_SSE=1` (default off).

## Key Features

### OS-Level Intervention
- **System Tray**: H.NotifyIcon で常駐、右クリックメニューで操作
- **Toast Notifications**: 15分経過/休憩終了/Idle時に通知
- **Unavoidable Dialog**: 25分経過で強制表示、Xボタン無効、アクションボタンのみで閉じる
- **Focus Capture**: Topmost + Win32 SetForegroundWindow

### Intervention Escalation Flow
```
Work phase:
  15min → Toast (Continue/Break/Complete)
  25min → Unavoidable Dialog (Continue/Break/Complete)
  30min+ → Dialog every 5min

Break phase:
  End → Toast
  +1min → Unavoidable Dialog

Idle phase:
  5min → Toast
  10min → Unavoidable Dialog with Ready tiles
```

## Commands
```bash
# Canonical local validation (dotnet format + NuGet vulnerability scan +
# unit tests + dual desktop build + TimelineWindow connector safety check).
# This is what CI runs and what the workspace-level verify-tastile-change
# skill expects.
.\scripts\check.ps1

# Unit tests only (no desktop build — useful without a sibling tastile-core):
.\scripts\check.ps1 -SkipDesktopBuild

# Run a single xUnit test class / case:
dotnet test tests/TastileDesktop.Tests/TastileDesktop.Tests.csproj \
    --filter "FullyQualifiedName~AppUpdateServiceTests.VerifyHashMismatchIsRejected"

# Build a single RID (packaging/runtime parity with the installer):
dotnet build src/TastileDesktop/TastileDesktop.csproj -r win-x64

# Run unpackaged against prod API (default beta.tastile.app):
dotnet run --project src/TastileDesktop

# Run against the local tastile-core daemon during development:
$env:TASTILE_PROFILE="dev"
$env:TASTILE_API_BASE_URL="http://127.0.0.1:3140"
dotnet run --project src/TastileDesktop

# Release installer:
.\scripts\build-desktop-installer.ps1 -Version 0.3.13
```

`TASTILE_PROFILE=prod` (default) uses `%APPDATA%\Tastile`; `dev` uses
`%APPDATA%\Tastile-dev` — keep the two isolated when running side by side.
Use the same version string for the app build, installer filename, and
hosted update manifest so the desktop's version compare stays correct.

One test project exists: `tests/TastileDesktop.Tests` (broad resolver /
service / contract coverage, ~196 cases). `check.ps1` runs it together
with `dotnet format --verify-no-changes` and a NuGet vulnerability scan
before the desktop build and the `TimelineWindow` connector wiring check.

## Local data footprint

| Path | Purpose |
|---|---|
| `%APPDATA%\Tastile\settings.json` | UI preferences (theme, prompt toast, quick panel) |
| `%LOCALAPPDATA%\Tastile\Auth\credentials.bin` | DPAPI-protected id/refresh token |
| `%TEMP%\tastile-desktop.log` | Best-effort debug log |
| `%TEMP%\tastile-update-*.exe` | Downloaded installer (release upgrades) |

No SQLite database, no event log, no tile cache. The desktop is a thin
presentation layer over the AWS API.

## Settings
Stored in `%APPDATA%/Tastile/settings.json`:
- `ToastNotifyMinutes`: 15 (toast reminder timing)
- `InterventionMinutes`: 25 (force dialog timing)
- `DefaultBreakMinutes`: 5
- `IdlePromptMinutes`: 5
- `InterventionRepeatMinutes`: 5
- `LaunchAtStartup`: false

## Claude Code Plugins (project scope)

この repository の `.claude/settings.json` で enabled:

| Plugin | 提供元 | 主用途 |
| --- | --- | --- |
| `winui@win-dev-skills` | `microsoft/win-dev-skills` | WinUI 3 / Fluent Design 設計・実装・レビュー・UI テスト |
| `microsoft-docs@claude-plugins-official` | 同 marketplace 内 | Microsoft Learn docs + Microsoft Learn MCP server |

### 起動トリガー

UI 改善 / Fluent Design 整合性レビュー / XAML レイアウト相談 / accessibility 確認 /
新画面追加 / 既存 Control の置き換え検討が出たら、`@winui-dev` agent または
`winui-design` / `winui-code-review` / `winui-ui-testing` skill を起動する。

### 典型プロンプト

```text
@winui-dev

現在のWinUI 3アプリのUIをレビューしてください。

Fluent Design / Windows 11のネイティブアプリとして、
- layout / spacing / typography / control selection / visual hierarchy
- Light / Dark / High Contrast
- accessibility
- responsive window sizing
を確認してください。

winui-design と winui-search を使い、
WinUI Gallery / Community Toolkit の既存パターンを優先し、
独自 UI を増やすのではなく WinUI 標準の表現に寄せてください。

必要なら実装まで修正し、最後に UI testing とスクリーンショットで確認してください。
```

### 制約 (hard rule)

- View / Control XAML では色の `#RRGGBB` 直書き禁止。中央の token 定義 (`App.xaml`) 以外は `ThemeResource` 経由。
- `NavigationView` を全画面で使うのは避ける。`SelectorBar` があるのに独自 segmented control を作らない。
- 独自 `ControlTemplate` で標準 Control を再実装しない (Fluent 標準を優先)。
- `ContentDialog` / `TeachingTip` / `InfoBar` を用途で使い分ける。
- `microsoft-learn` MCP は Claude Code 側で `microsoft-docs` plugin 経由で取得する
  (本 repo `opencode.json` の `microsoft-learn` は `opencode` CLI 用で別物)。
- `frontend-design` (Web frontend 向け) は **有効化しない**。WinUI には `winui-design` を主役に据える。
- `ui-ux-pro-max` / `figma` / `csharp-lsp` 等の追加は要 ADR。\n\n## Reviewer policy (PROMPT.ja.md §26)

`tastile/tastile-desktop` is a solo project — `@rebuildup` is the only
contributor with merge authority. There is no separate human reviewer
available. Per the canonical contract, the following alternative review
path is in force on every PR:

- **AI reviewers** — Copilot + coderabbitai (configured at repo level)
- **Required status checks** — `verify-head` on `main`-targeting PRs
  (enforces `release-X-Y-Z` head pattern, see
  `.github/workflows/release-head-check.yml`) + existing CI
- **Manual verification** — the workspace-level `verify-tastile-change`
  Skill is invoked immediately before marking a PR ready-to-merge
- **Final review** — the PR author self-attests via the verification
  steps above; the lack of a separate human reviewer is recorded
  honestly in `.github/PULL_REQUEST_TEMPLATE.md`

`CODEOWNERS` is intentionally **not** created: a single-owner file
would be a formal self-reviewer, which PROMPT.ja.md §26 explicitly
rejects. The alternative path above substitutes.

## Repository labels and milestones (ADR-0009)

Custom labels created for sprint and Kanban bookkeeping:

- **Priority** — `priority: P0` / `P1` / `P2`
- **Size** — `size: S` / `M` / `L`
- **Area** — `area: auth` / `api` / `ui` / `test` / `build` / `release` / `i18n`
- **Target version** — `target-version: 0.6.0`
- **Workflow flags** — `release-only`, `breaking-change`

Milestones track per-release sprints. Current active milestone:
`v0.6.0`.

Project v2 board is **active**: <https://github.com/orgs/tastile/projects/2>
("Tastile Desktop Sprint Board", linked to this repo). Default Status
field (`Todo` / `In Progress` / `Done`) drives the Kanban. Required
custom fields per ADR-0009:

- **Priority** — `P0` / `P1` / `P2`
- **Size** — `S` / `M` / `L`
- **Target Version** — `0.6.0`
- **Area** — `auth` / `api` / `ui` / `test` / `build` / `release` / `i18n`
- **Execution Generation** — numeric, used for fencing per
  PROMPT.ja.md §17

Status cannot transition from `Todo` to `In Progress` until all required
custom fields are populated. WIP cap on `In Progress` is a follow-up.

## Branch and PR rules (ADR-0007)

- `main` is the released / integrated state. No direct push.
- `release-<major>-<minor>-<patch>` is the active sprint trunk.
- Ticket branch name = Issue number only (no `feature/`, `fix/`, `docs/`
  prefix, no slug). See Issue #20 for the policy rationale and
  exception list.
- Every durable ticket branch gets a published remote head + Draft PR
  immediately after its first meaningful commit (canonical start
  procedure from PROMPT.ja.md §12).
- Stacked ticket PRs are allowed within the same target release;
  intermediate predecessor-branch merges never close a downstream
  Issue — only `release-x-y-z -> main` landing closes it.\n