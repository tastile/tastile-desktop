# Step 8 残作業修正プラン

## 前提

Step 8.1 (Assets) のみ完了。8.2-8.5 の未実装/不完全部分を修正する
全て `src/TastileDesktop/` 内のファイルが対象

---

## Fix 1: 右クリックメニューのロジック完成 (8.2)

### 現状
- MainWindow.xaml に MenuFlyout の UI は存在する (Start/Complete/Defer/Delete)
- Start / Complete のハンドラは動作する
- Defer のハンドラがステータスメッセージ表示のみ (実際の API コールなし)
- Delete のハンドラが "not yet implemented" メッセージのみ

### 修正ファイル

**`ViewModels/MainViewModel.cs`** に追加:
```csharp
[RelayCommand]
private async Task DeferTile(string tileId)
{
    try
    {
        var result = await _api.DeferTileAsync(tileId);
        if (result != null && !result.Ok)
            StatusMessage = $"Error: {result.Error}";
        else
            StatusMessage = "Tile deferred";
    }
    catch (Exception ex)
    {
        StatusMessage = $"Error: {ex.Message}";
    }
}

[RelayCommand]
private async Task DeleteTile(string tileId)
{
    try
    {
        // daemon に /commands/tile/delete がない場合は defer で代替
        var result = await _api.DeferTileAsync(tileId, reason: "deleted");
        if (result != null && !result.Ok)
            StatusMessage = $"Error: {result.Error}";
        else
            StatusMessage = "Tile deleted";
    }
    catch (Exception ex)
    {
        StatusMessage = $"Error: {ex.Message}";
    }
}
```

**`MainWindow.xaml.cs`** のハンドラ修正:
```csharp
// OnDeferTileMenuClick: 現在のスタブを置換
private void OnDeferTileMenuClick(object sender, RoutedEventArgs e)
{
    if (sender is MenuFlyoutItem item && item.Tag is string tileId)
        _ = ViewModel.DeferTileCommand.ExecuteAsync(tileId);
}

// OnDeleteTileMenuClick: 現在のスタブを置換
private void OnDeleteTileMenuClick(object sender, RoutedEventArgs e)
{
    if (sender is MenuFlyoutItem item && item.Tag is string tileId)
        _ = ViewModel.DeleteTileCommand.ExecuteAsync(tileId);
}
```

### 検証
- [ ] タイル右クリック → Defer → ステータスに "Tile deferred" 表示
- [ ] タイル右クリック → Delete → ステータスに "Tile deleted" 表示
- [ ] Defer/Delete 後にタイル一覧が更新される

---

## Fix 2: ライフサイクルフィルター (8.3)

### 現状
完全に未実装

### 修正ファイル

**`ViewModels/MainViewModel.cs`** に追加:

フィールド:
```csharp
private List<TileListItem> _allTiles = new();

[ObservableProperty]
private string _selectedFilter = "All";
```

フィルター用プロパティ (RadioButton バインド用):
```csharp
public bool IsFilterAll
{
    get => SelectedFilter == "All";
    set { if (value) SelectedFilter = "All"; }
}
public bool IsFilterReady
{
    get => SelectedFilter == "Ready";
    set { if (value) SelectedFilter = "Ready"; }
}
public bool IsFilterStarted
{
    get => SelectedFilter == "Started";
    set { if (value) SelectedFilter = "Started"; }
}
public bool IsFilterDone
{
    get => SelectedFilter == "Done";
    set { if (value) SelectedFilter = "Done"; }
}
```

OnSelectedFilterChanged + ApplyFilter:
```csharp
partial void OnSelectedFilterChanged(string value)
{
    OnPropertyChanged(nameof(IsFilterAll));
    OnPropertyChanged(nameof(IsFilterReady));
    OnPropertyChanged(nameof(IsFilterStarted));
    OnPropertyChanged(nameof(IsFilterDone));
    ApplyFilter();
}

private void ApplyFilter()
{
    var source = _allTiles;
    if (SelectedFilter != "All")
        source = source.Where(t => t.Lifecycle == SelectedFilter).ToList();

    Tiles.Clear();
    foreach (var tile in source)
        Tiles.Add(tile);
}
```

既存の UpdateTileList (または OnTilesChanged) を変更:
```csharp
// タイル更新時に _allTiles を保存してからフィルター適用
private void OnTilesChanged(TilesResponse? tiles)
{
    if (tiles?.Tiles == null) return;

    _allTiles = tiles.Tiles.Select(t => new TileListItem
    {
        Id = t.Id,
        Title = t.Title,
        Lifecycle = t.Lifecycle,
        WorkedMinutes = t.WorkedMinutes,
        NextAction = t.NextAction,
    }).ToList();

    ApplyFilter();
}
```

**`MainWindow.xaml`** にフィルター UI 追加:

"Tiles" テキストの行をフィルター付きに変更:
```xml
<!-- 既存の <TextBlock Text="Tiles" .../> を以下に置換 -->
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <TextBlock Text="Tiles" Style="{StaticResource BodyStrongTextBlockStyle}" VerticalAlignment="Center" />
    <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="4"
                HorizontalAlignment="Right">
        <RadioButton Content="All" GroupName="Filter"
                     IsChecked="{x:Bind ViewModel.IsFilterAll, Mode=TwoWay}" MinWidth="0" Padding="8,4" />
        <RadioButton Content="Ready" GroupName="Filter"
                     IsChecked="{x:Bind ViewModel.IsFilterReady, Mode=TwoWay}" MinWidth="0" Padding="8,4" />
        <RadioButton Content="Started" GroupName="Filter"
                     IsChecked="{x:Bind ViewModel.IsFilterStarted, Mode=TwoWay}" MinWidth="0" Padding="8,4" />
        <RadioButton Content="Done" GroupName="Filter"
                     IsChecked="{x:Bind ViewModel.IsFilterDone, Mode=TwoWay}" MinWidth="0" Padding="8,4" />
    </StackPanel>
</Grid>
```

### 検証
- [ ] All 選択時に全タイルが表示される
- [ ] Ready 選択時に Ready タイルのみ表示される
- [ ] Started / Done も同様
- [ ] タイル操作 (Start/Complete 等) 後にフィルターが維持される

---

## Fix 3: タイル詳細表示 (8.4)

### 現状
タイルリストに Title + Lifecycle バッジ + WorkedMinutes のみ。next_action が表示されない

### 修正ファイル

**`ViewModels/MainViewModel.cs`** の TileListItem にプロパティ追加:
```csharp
public sealed class TileListItem
{
    // ... 既存プロパティ ...
    public required string? NextAction { get; init; }

    public string NextActionText => !string.IsNullOrEmpty(NextAction) ? $"→ {NextAction}" : "";
    public Visibility HasNextAction =>
        !string.IsNullOrEmpty(NextAction) ? Visibility.Visible : Visibility.Collapsed;
}
```

TileListItem 生成箇所に NextAction を追加:
```csharp
new TileListItem
{
    Id = t.Id,
    Title = t.Title,
    Lifecycle = t.Lifecycle,
    WorkedMinutes = t.WorkedMinutes,
    NextAction = t.NextAction,  // ← 追加
}
```

**`MainWindow.xaml`** の DataTemplate 変更:

Title の TextBlock を StackPanel に置換:
```xml
<!-- 既存: -->
<!-- <TextBlock Text="{x:Bind Title}" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" /> -->

<!-- 変更後: -->
<StackPanel Grid.Column="0" VerticalAlignment="Center">
    <TextBlock Text="{x:Bind Title}" TextTrimming="CharacterEllipsis" />
    <TextBlock Text="{x:Bind NextActionText}" FontSize="12"
               Foreground="{ThemeResource TextFillColorSecondaryBrush}"
               Visibility="{x:Bind HasNextAction}" />
</StackPanel>
```

### 検証
- [ ] next_action がある タイルは Title の下に "→ {next_action}" が表示される
- [ ] next_action がない タイルは1行表示のまま

---

## Fix 4: スタートアップ起動 (8.5)

### 現状
SettingsViewModel.UpdateStartupTaskAsync() がプレースホルダーのみ。App.xaml.cs に --minimized 処理なし

### 修正ファイル

**`ViewModels/SettingsViewModel.cs`** の UpdateStartupTaskAsync を置換:
```csharp
private void UpdateStartupTask(bool enable)
{
    try
    {
        const string valueName = "Tastile";
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);

        if (key == null) return;

        if (enable)
        {
            var exePath = Environment.ProcessPath;
            if (exePath != null)
                key.SetValue(valueName, $"\"{exePath}\" --minimized");
        }
        else
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Startup task registration failed: {ex.Message}");
    }
}
```

SaveCommand 内で呼び出す:
```csharp
// 既存の SaveCommand ハンドラ内に追加
UpdateStartupTask(LaunchAtStartup);
```

**`App.xaml.cs`** の OnLaunched を変更:
```csharp
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    _mainWindow = new MainWindow();
    _trayIconService = new TrayIconService(_mainWindow.ViewModel, new Services.CoreApiClient());
    _trayIconService.Initialize(_mainWindow);
    _mainWindow.Closed += OnMainWindowClosed;

    // Check for --minimized flag
    var cmdArgs = Environment.GetCommandLineArgs();
    if (cmdArgs.Contains("--minimized"))
    {
        // Start minimized to tray only
        // Do not call _mainWindow.Activate()
    }
    else
    {
        _mainWindow.Activate();
    }
}
```

### 検証
- [ ] Settings で Launch at startup を ON → レジストリに登録される
- [ ] Settings で Launch at startup を OFF → レジストリから削除される
- [ ] `--minimized` 付きで起動 → ウィンドウ非表示、トレイのみ
- [ ] 通常起動 → ウィンドウ表示

---

## 実行順序

```
Fix 1 (右クリック完成)  → 最小の変更、他に依存なし
Fix 3 (タイル詳細表示)  → TileListItem の変更が Fix 2 と共有
Fix 2 (フィルター)      → _allTiles + ApplyFilter の導入が一番大きな変更
Fix 4 (スタートアップ)  → 独立、最後でOK
```

## 変更ファイルまとめ

| ファイル | Fix | 変更内容 |
|---------|-----|---------|
| `ViewModels/MainViewModel.cs` | 1,2,3 | DeferTile/DeleteTile コマンド、フィルター、NextAction |
| `MainWindow.xaml.cs` | 1 | Defer/Delete ハンドラ修正 |
| `MainWindow.xaml` | 2,3 | フィルター UI、next_action 2行目 |
| `ViewModels/SettingsViewModel.cs` | 4 | レジストリ登録コード |
| `App.xaml.cs` | 4 | --minimized フラグ処理 |
