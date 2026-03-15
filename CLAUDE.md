# Tastile Desktop

Windows native client for Tastile execution control system.

## Tech Stack
- C# / WinUI 3 (Windows App SDK 1.7)
- .NET 10
- CommunityToolkit.Mvvm
- H.NotifyIcon.WinUI (system tray)
- Microsoft.Toolkit.Uwp.Notifications (toast notifications)
- System.Text.Json for API communication

## Architecture

```
TastileDesktop/
├── Services/
│   ├── CoreApiClient.cs          # Daemon HTTP API client (localhost:3140)
│   ├── DaemonManager.cs          # Daemon process lifecycle
│   ├── PollingService.cs         # 2s polling + event dispatch
│   ├── InterventionEngine.cs     # Escalation logic (toast → intervention)
│   ├── NotificationService.cs    # Windows toast notifications
│   ├── SettingsService.cs        # JSON settings persistence (%APPDATA%/Tastile)
│   └── TrayIconService.cs        # System tray icon + context menu
├── ViewModels/
│   ├── MainViewModel.cs          # Main window state + commands
│   └── SettingsViewModel.cs      # Settings form binding
├── Views/
│   ├── InterventionWindow.xaml   # Unavoidable full-screen dialog
│   └── SettingsWindow.xaml       # Settings panel
├── Models/
│   └── ApiModels.cs              # Daemon API DTOs
└── MainWindow.xaml               # Main UI with tile list, execution status
```

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

# Run
dotnet run --project src/TastileDesktop

# Publish (Release)
dotnet publish -c Release -r win-x64
```

## Settings
Stored in `%APPDATA%/Tastile/settings.json`:
- `ToastNotifyMinutes`: 15 (toast reminder timing)
- `InterventionMinutes`: 25 (force dialog timing)
- `DefaultBreakMinutes`: 5
- `IdlePromptMinutes`: 5
- `InterventionRepeatMinutes`: 5
- `LaunchAtStartup`: false
