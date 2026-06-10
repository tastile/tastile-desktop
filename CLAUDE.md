# Tastile Desktop

Windows native client for Tastile execution control system. Connects to the
new AWS-hosted `tastile-core` API with Cognito Hosted UI sign-in; no local
daemon process.

## Tech Stack
- C# / WinUI 3 (Windows App SDK 1.7)
- .NET 10
- CommunityToolkit.Mvvm
- H.NotifyIcon.WinUI (system tray)
- Microsoft.Toolkit.Uwp.Notifications (toast notifications)
- `System.Security.Cryptography.ProtectedData` (DPAPI token store)
- System.Text.Json for API communication

## Architecture

```
TastileDesktop/                                AWS remote API
├── App.xaml.cs                               ──HTTPS + Bearer JWT──▶  beta.tastile.app  ─▶  tastile-core
├── Services/                                                                       (no local daemon)
│   ├── CoreApiClient.cs            # Bearer + 401-refresh-retry HTTPS client
│   ├── CognitoAuthService.cs       # PKCE Hosted UI flow + refresh + signout
│   ├── SecureTokenStore.cs         # DPAPI-protected credentials
│   ├── EventDrivenPoller.cs        # User-action / focus / idle refresh (no wall-clock tick)
│   ├── AuthService.cs              # Facade over CognitoAuthService
│   ├── AppSettings.cs              # env-var-driven runtime config
│   ├── InterventionEngine.cs       # Escalation logic (toast → intervention)
│   ├── NotificationService.cs      # Windows toast notifications
│   ├── SettingsService.cs          # JSON settings persistence (%APPDATA%/Tastile)
│   └── TrayIconService.cs          # System tray icon + context menu
├── Models/
│   ├── ApiModels.cs                # AWS API DTOs
│   ├── CognitoConfig.cs            # Hosted UI / user pool config
│   └── AuthSession.cs              # id_token / refresh_token / sub / email / exp
├── ViewModels/
│   ├── MainViewModel.cs            # Main window state + commands
│   └── SettingsViewModel.cs        # Settings form binding
└── Views/
    ├── AuthWindow.xaml             # Single-step "sign in" entry point
    ├── SettingsWindow.xaml         # Settings + runtime paths panel
    └── InterventionWindow.xaml     # Unavoidable full-screen dialog
```

## Connection Model

- **Auth**: Cognito Hosted UI + PKCE (RFC 7636). Tokens saved via DPAPI in
  `%LOCALAPPDATA%\Tastile\Auth\credentials.bin`. `CoreApiClient` adds
  `Authorization: Bearer <id_token>` to every request and retries once on
  401 after refreshing.
- **API base URL**: `TASTILE_API_BASE_URL` (default `https://beta.tastile.app`).
  For local dev, set `TASTILE_API_BASE_URL=http://127.0.0.1:3140`.
- **Cognito**: configurable via `TASTILE_COGNITO_CLIENT_ID`,
  `TASTILE_COGNITO_USER_POOL_ID`, `TASTILE_COGNITO_HOSTED_UI_DOMAIN`,
  `TASTILE_COGNITO_REGION`, `TASTILE_COGNITO_CALLBACK_URL`. Default client ID
  is `2b9fkkb4u5di8veelnmjkmnldj` (shared with tastile-web).
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
# Build
dotnet build -r win-x64

# Run (prod, default beta.tastile.app)
dotnet run --project src/TastileDesktop

# Run with local daemon during development
$env:TASTILE_API_BASE_URL="http://127.0.0.1:3140"
dotnet run --project src/TastileDesktop

# Publish (Release)
dotnet publish -c Release -r win-x64
```

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
