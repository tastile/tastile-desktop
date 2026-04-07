# Tastile Desktop Bug Fix Plan

**Goal:** Fix two bugs in tastile-desktop:
1. Tile deletion from Edit screen (CreateTileWindow) is not working
2. Active tile recognition after app restart is broken

**Architecture:**
- Issue 1: CreateTileWindow's DeleteButton click handler exists but API call may fail silently
- Issue 2: Desktop app may not properly initialize polling service on startup to fetch active execution state from daemon

**Tech Stack:** C# / WinUI 3 / .NET 9 (net9.0-windows10.0.26100.0)

---

### Task 1: Fix Tile Deletion from Edit Screen

**Files:**
- Modify: `src/TastileDesktop/Views/CreateTileWindow.xaml.cs:1018-1025`
- Test: Manual test with tastile-desktop

**Step 1: Examine OnDeleteClick handler**

Current code at line 1018:
```csharp
private async void OnDeleteClick(object sender, RoutedEventArgs e)
{
    if (!string.IsNullOrEmpty(_editTileId))
    {
        await _api.DeleteTileAsync(_editTileId);
        Close();
    }
}
```

This calls `_api.DeleteTileAsync(_editTileId)` but doesn't check the result or handle errors.

**Step 2: Add error handling and result check**

```csharp
private async void OnDeleteClick(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrEmpty(_editTileId)) return;
    
    try
    {
        var result = await _api.DeleteTileAsync(_editTileId);
        if (result?.Ok == true)
        {
            Close();
        }
        else
        {
            ShowError(_isJapanese ? "タイルの削除に失敗しました。" : "Failed to delete tile.");
        }
    }
    catch (Exception ex)
    {
        ShowError((_isJapanese ? "エラー: " : "Error: ") + ex.Message);
    }
}
```

**Step 3: Run build to verify no compile errors**

```bash
dotnet build -r win-x64
```

Expected: BUILD SUCCEEDED

**Step 4: Commit**

```bash
git add src/TastileDesktop/Views/CreateTileWindow.xaml.cs
git commit -m "fix: add error handling to tile deletion in CreateTileWindow"
```

---

### Task 2: Fix Active Tile Recognition on App Restart

**Files:**
- Modify: `src/TastileDesktop/ViewModels/MainViewModel.cs:713-716`
- Modify: `src/TastileDesktop/Services/PollingService.cs:101-106`
- Test: Manual restart test

**Step 1: Verify polling initialization in MainViewModel**

Current code at line 713-716:
```csharp
public async Task InitializeAsync()
{
    await _pollingService.StartAsync();
}
```

This should initialize polling and trigger initial poll. Let me verify this is being called in App.xaml.cs.

**Step 2: Check App.xaml.cs for MainViewModel initialization**

Looking at line 188:
```csharp
await _mainWindow.InitializeAsync();
```

This calls MainWindow.InitializeAsync(). Let me check what it does.

**Step 3: Verify MainWindow.InitializeAsync calls ViewModel InitializeAsync**

Search for InitializeAsync in MainWindow.xaml.cs...

**Step 4: If polling initialization is correct, check if daemon is returning active state**

The daemon should return active tile state via /read/execution-view endpoint on startup.

**Step 5: Add debug logging to verify polling receives correct state**

Add logging in OnExecutionViewChanged to trace received state.

**Step 6: Commit**

```bash
git add src/TastileDesktop/ViewModels/MainViewModel.cs src/TastileDesktop/Services/PollingService.cs
git commit -m "fix: add debug logging for execution state on startup"
```

---

### Task 3: Verify Both Fixes

**Step 1: Build and test deletion**

Run app, open Tiles window, click edit on a tile, click delete button.

**Step 2: Test restart with active tile**

Start a tile, close app (daemon continues running), reopen app, verify active tile is recognized.

**Step 3: Final commit**

```bash
git add .
git commit -m "fix: tile deletion and active tile recognition on restart"
```
