# Tastile Desktop (WinUI 3)

Windows native client for Tastile.

## Quick Start

### 1. Start Mock API Server (for testing)

```powershell
cd mock-server
cargo run
```

Server runs on http://localhost:3140/

### 2. Start Desktop App

```powershell
cd src/TastileDesktop
dotnet run
```

## Features

### System Tray Icon
The tray icon shows connection status:
- **Green** "T": Connected to daemon
- **Red/Orange** "T": Disconnected

Hover to see tooltip with tile count.

### UI Panels

1. **Status Panel** - Shows current phase (Idle/Work/Break) with timer
2. **Timeline** - Today's activity timeline
3. **Create Tile** - Expandable panel for new tiles
4. **Tile List** - Filterable list with context menu
5. **Quick Memo** - Fast memo input

### Intervention System

- **Toast notifications** at 15 min (work) / break end
- **Intervention window** at 25 min (forced decision)

## Architecture

```
┌─────────────────┐      HTTP API      ┌─────────────────┐
│  WinUI 3 Client │ ◄────────────────► │  tastile-daemon │
│  (This app)     │   localhost:3140   │  (Rust backend) │
└─────────────────┘                    └─────────────────┘
```

Client responsibilities:
- Display daemon state
- Send user commands
- Show notifications
- Tray presence

No business logic in client - all decisions made by daemon.
