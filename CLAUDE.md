# Tastile Desktop

Windows native client for Tastile execution control system.

## Tech Stack
- C# / WinUI 3 (Windows App SDK)
- CommunityToolkit.Mvvm
- System.Text.Json for API communication

## Architecture
- Services/ — Core API client, notification service, OS intervention
- ViewModels/ — MVVM view models
- Views/ — XAML pages (Now, Prompt, Tiles, Settings)
- Models/ — API response models

## Key Responsibilities
- OS-level intervention (focus capture, system tray, notifications)
- Prompt display and response
- Active tile visualization
- Quick memo input

## Commands
- `dotnet build` — Build solution
- `dotnet run --project src/TastileDesktop` — Run app
