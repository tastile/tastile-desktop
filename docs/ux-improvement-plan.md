# tastile-desktop UX 改善プラン

## Context

tastile-desktop は起動するものの、ユーザーが「何をすればいいか分からない」状態
具体的な問題:
1. **タイムラインがない** — 今日何をしたか全く見えない
2. **タスク作成方法が分からない** — テキストボックスが目立たず、何を入力すべきか不明
3. **通知が出ない** — 何も操作しなければ何も起きないので使い方が伝わらない

pomodoroom の最低限の UX (タイムライン + タスク作成の明確さ) を参考に改善する

## 変更ファイル一覧

| ファイル | 変更内容 |
|---------|---------|
| `Models/ApiModels.cs` | TimelineSegment record 追加 |
| `Services/CoreApiClient.cs` | GetEventsRawAsync() 追加 |
| `Services/PollingService.cs` | event_count 変更検知 + TimelineChanged イベント |
| `ViewModels/MainViewModel.cs` | TimelineSegments, IdleGuidanceText, CreateAndStart, NewTileNextAction |
| `MainWindow.xaml` | レイアウト再構成 (NOW / CREATE / TODAY / TILES) |
| `MainWindow.xaml.cs` | Enter キーハンドラ追加 |

全ファイル `src/TastileDesktop/` 配下

---

## Step 1: Events API + タイムラインデータモデル

### 1.1 ApiModels.cs にタイムラインモデル追加

```csharp
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

// Timeline segment (computed client-side from daemon events)
public record TimelineSegment
{
    public required string Kind { get; init; }       // "work" | "break" | "idle"
    public required string? TileTitle { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }          // null = ongoing (mutable via `with`)
    public bool IsActive => EndedAt == null;

    public string TimeText => StartedAt.ToLocalTime().ToString("HH:mm");
    public string DurationText
    {
        get
        {
            var end = EndedAt ?? DateTime.UtcNow;
            var min = (int)(end - StartedAt).TotalMinutes;
            return min < 1 ? "<1m" : $"{min}m";
        }
    }
    public string StatusText => IsActive ? "▸ now" : DurationText;
    public string DisplayTitle => Kind switch
    {
        "work" => TileTitle ?? "Working",
        "break" => "Break",
        _ => "Idle"
    };

    // For XAML x:Bind in DataTemplate — use Color, not SolidColorBrush
    public Windows.UI.Color BadgeColor => Kind switch
    {
        "work" => Windows.UI.Color.FromArgb(255, 0, 120, 212),
        "break" => Windows.UI.Color.FromArgb(255, 16, 124, 16),
        _ => Windows.UI.Color.FromArgb(255, 128, 128, 128),
    };
    public SolidColorBrush StatusForeground => IsActive
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 124, 16))
        : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
}
```

**注意**: `record` にすることで `with` 式が使える (EndedAt を差し替えた新インスタンス生成)

### 1.2 CoreApiClient.cs に GetEventsRawAsync 追加

`using System.Text.Json;` を追加

```csharp
// Returns raw JSON because Event uses serde tagged enum
public async Task<JsonElement?> GetEventsRawAsync()
{
    try
    {
        var json = await _httpClient.GetStringAsync("/debug/events");
        return JsonDocument.Parse(json).RootElement;
    }
    catch { return null; }
}
```

### 検証
- [ ] ビルドが通る
- [ ] `GetEventsRawAsync()` がデーモンから JSON を取得できる

---

## Step 2: ViewModel にタイムライン + 新機能追加

### 2.1 using 追加

```csharp
using System.Text.Json;
using Microsoft.UI.Dispatching;
```

### 2.2 新しいフィールドとプロパティ

```csharp
// Observable properties
[ObservableProperty]
private string _newTileNextAction = string.Empty;

// Collections
public ObservableCollection<TimelineSegment> TimelineSegments { get; } = new();

// Computed properties
public bool IsTimelineEmpty => TimelineSegments.Count == 0;
public bool IsTilesEmpty => Tiles.Count == 0;

public Visibility HasNextAction =>
    !string.IsNullOrEmpty(ActiveTileNextAction) ? Visibility.Visible : Visibility.Collapsed;

public string IdleGuidanceText
{
    get
    {
        if (_allTiles.Count == 0)
            return "Create your first tile above to get started.";
        var ready = _allTiles.Count(t =>
            t.Lifecycle.Equals("Ready", StringComparison.OrdinalIgnoreCase));
        if (ready > 0)
            return $"{ready} tile(s) ready — click one below to start.";
        return "All tiles done. Create a new one!";
    }
}
```

### 2.3 CreateAndStartTileCommand (新規)

```csharp
[RelayCommand]
private async Task CreateAndStartTile()
{
    if (string.IsNullOrWhiteSpace(NewTileTitle)) return;
    try
    {
        var result = await _api.CreateTileAsync(
            NewTileTitle.Trim(),
            string.IsNullOrWhiteSpace(NewTileNextAction) ? null : NewTileNextAction.Trim());
        if (result?.Ok != true) { StatusMessage = $"Error: {result?.Error}"; return; }

        // Refresh tiles to find the newly created one
        var tiles = await _api.GetTilesAsync();
        var newTile = tiles?.Tiles
            .Where(t => t.Lifecycle == "Ready")
            .FirstOrDefault(t => t.Title == NewTileTitle.Trim());
        if (newTile != null)
            await _api.StartTileAsync(newTile.Id);

        NewTileTitle = "";
        NewTileNextAction = "";
        StatusMessage = "Tile created and started";
        await _pollingService.PollAsync();
    }
    catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
}
```

### 2.4 既存 CreateTileAsync を修正

next_action を渡すように変更:
```csharp
var result = await _api.CreateTileAsync(
    title,
    string.IsNullOrWhiteSpace(NewTileNextAction) ? null : NewTileNextAction.Trim());
// 成功時に NewTileNextAction = "" もクリア
```

### 2.5 タイムライン構築メソッド

```csharp
public async Task RefreshTimelineAsync()
{
    var raw = await _api.GetEventsRawAsync();
    if (raw == null) return;

    var segments = new List<TimelineSegment>();
    var today = DateTime.UtcNow.Date;

    // tile_id → title lookup
    var titleMap = _allTiles.ToDictionary(t => t.Id, t => t.Title);

    if (raw.Value.TryGetProperty("events", out var events))
    {
        TimelineSegment? openWork = null;
        TimelineSegment? openBreak = null;

        foreach (var envelope in events.EnumerateArray())
        {
            if (!envelope.TryGetProperty("event", out var ev)) continue;
            if (!ev.TryGetProperty("type", out var typeEl)) continue;
            var type = typeEl.GetString();
            var occurredAt = envelope.TryGetProperty("occurred_at", out var occ)
                ? DateTime.Parse(occ.GetString()!) : DateTime.UtcNow;

            switch (type)
            {
                case "segment_started":
                {
                    var mode = ev.TryGetProperty("mode", out var m) ? m.GetString() : "work";
                    var tileId = ev.TryGetProperty("tile_id", out var tid) ? tid.GetString() : null;
                    var startedAt = ev.TryGetProperty("started_at", out var sa)
                        ? DateTime.Parse(sa.GetString()!) : occurredAt;

                    if (startedAt.Date < today) break;

                    titleMap.TryGetValue(tileId ?? "", out var title);
                    var seg = new TimelineSegment
                    {
                        Kind = mode ?? "work",
                        TileTitle = title,
                        StartedAt = startedAt,
                        EndedAt = null,
                    };
                    segments.Add(seg);
                    if (mode == "work") openWork = seg;
                    break;
                }
                case "segment_ended":
                {
                    var endedAt = ev.TryGetProperty("ended_at", out var ea)
                        ? DateTime.Parse(ea.GetString()!) : occurredAt;
                    if (openWork != null)
                    {
                        var idx = segments.IndexOf(openWork);
                        if (idx >= 0)
                            segments[idx] = openWork with { EndedAt = endedAt };
                        openWork = null;
                    }
                    break;
                }
                case "break_started":
                {
                    var startedAt = ev.TryGetProperty("started_at", out var sa)
                        ? DateTime.Parse(sa.GetString()!) : occurredAt;
                    if (startedAt.Date < today) break;

                    var seg = new TimelineSegment
                    {
                        Kind = "break",
                        TileTitle = null,
                        StartedAt = startedAt,
                        EndedAt = null,
                    };
                    segments.Add(seg);
                    openBreak = seg;
                    break;
                }
                case "break_ended":
                {
                    var endedAt = ev.TryGetProperty("ended_at", out var ea)
                        ? DateTime.Parse(ea.GetString()!) : occurredAt;
                    if (openBreak != null)
                    {
                        var idx = segments.IndexOf(openBreak);
                        if (idx >= 0)
                            segments[idx] = openBreak with { EndedAt = endedAt };
                        openBreak = null;
                    }
                    break;
                }
            }
        }
    }

    segments.Sort((a, b) => a.StartedAt.CompareTo(b.StartedAt));

    // Update on UI thread
    if (DispatcherQueue.GetForCurrentThread() != null)
    {
        TimelineSegments.Clear();
        foreach (var seg in segments)
            TimelineSegments.Add(seg);
        OnPropertyChanged(nameof(IsTimelineEmpty));
    }
    else
    {
        DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
        {
            TimelineSegments.Clear();
            foreach (var seg in segments)
                TimelineSegments.Add(seg);
            OnPropertyChanged(nameof(IsTimelineEmpty));
        });
    }
}
```

**注意**: DispatcherQueue の扱いは実行時に動作確認が必要。PollingService が DispatcherTimer (UIスレッド) で動くなら、そこから呼べばディスパッチ不要

### 2.6 OnActiveTileChanged / OnTilesChanged でガイダンス更新

```csharp
// 既存の OnActiveTileChanged 内に追加:
OnPropertyChanged(nameof(IdleGuidanceText));
OnPropertyChanged(nameof(HasNextAction));

// 既存の OnTilesChanged 内に追加:
OnPropertyChanged(nameof(IdleGuidanceText));
OnPropertyChanged(nameof(IsTilesEmpty));
```

### 検証
- [ ] CreateAndStartTile でタイル作成 + 即開始される
- [ ] RefreshTimelineAsync でイベントからセグメント一覧が構築される
- [ ] IdleGuidanceText がタイル状態に応じて変わる

---

## Step 3: PollingService にタイムライン更新トリガー追加

### 3.1 PollingService.cs の変更

フィールド追加:
```csharp
private int _lastEventCount = 0;
```

イベント追加:
```csharp
public event Action? TimelineChanged;
```

PollAsync 内、`var activeTask = _api.GetActiveTileAsync();` の前に execution を取得して判定:
```csharp
// Timeline update check via execution endpoint
var execution = await _api.GetExecutionAsync();
if (execution != null && execution.EventCount != _lastEventCount)
{
    _lastEventCount = execution.EventCount;
    TimelineChanged?.Invoke();
}
```

### 3.2 MainViewModel で購読

InitializeAsync 内:
```csharp
_pollingService.TimelineChanged += async () =>
{
    await RefreshTimelineAsync();
};
```

### 検証
- [ ] タイル操作後に event_count が増加 → TimelineChanged 発火
- [ ] タイムラインが自動更新される
- [ ] event_count が変わらない間は無駄な API コールなし

---

## Step 4: MainWindow.xaml レイアウト再構成

既存セクション順: Header → Divider → Status Panel → New Tile → Tile List → Divider → Memo
新セクション順: Header → **NOW** → **CREATE** → **TODAY** → **TILES** → **MEMO**

### 4.1 xmlns 追加

```xml
xmlns:models="using:TastileDesktop.Models"
```

### 4.2 NOW セクション (既存 Status Panel 改善)

主な変更:
- タイマーテキストを **FontSize=24** に拡大 (現在は13)
- セクションヘッダー "NOW" 追加
- Idle 時に `IdleGuidanceText` をバインドして表示
- next_action 表示に `HasNextAction` Visibility を使用

```xml
<Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
        CornerRadius="8" Padding="20">
    <StackPanel Spacing="8">
        <TextBlock Text="NOW" FontWeight="Bold" FontSize="12"
                   Foreground="{ThemeResource TextFillColorSecondaryBrush}" />

        <!-- Idle -->
        <StackPanel Visibility="{x:Bind ViewModel.IsIdle, Mode=OneWay}" Spacing="4">
            <TextBlock Text="Idle" Style="{StaticResource SubtitleTextBlockStyle}" />
            <TextBlock Text="{x:Bind ViewModel.IdleGuidanceText, Mode=OneWay}"
                       Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                       TextWrapping="Wrap" />
        </StackPanel>

        <!-- Working -->
        <StackPanel Visibility="{x:Bind ViewModel.IsWorking, Mode=OneWay}" Spacing="4">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>
                <TextBlock Text="WORKING" FontWeight="Bold"
                           Foreground="{ThemeResource AccentTextFillColorPrimaryBrush}" />
                <TextBlock Grid.Column="1"
                           Text="{x:Bind ViewModel.WorkElapsedText, Mode=OneWay}"
                           FontFamily="Consolas" FontSize="24" FontWeight="Bold" />
            </Grid>
            <TextBlock Text="{x:Bind ViewModel.ActiveTileTitle, Mode=OneWay}"
                       Style="{StaticResource SubtitleTextBlockStyle}" />
            <TextBlock Text="{x:Bind ViewModel.ActiveTileNextAction, Mode=OneWay}"
                       Visibility="{x:Bind ViewModel.HasNextAction, Mode=OneWay}"
                       FontSize="14"
                       Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
            <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,8,0,0">
                <Button Content="Complete" Style="{StaticResource AccentButtonStyle}"
                        Command="{x:Bind ViewModel.CompleteTileCommand}" />
                <Button Content="Break (5 min)"
                        Command="{x:Bind ViewModel.StartBreakCommand}" />
            </StackPanel>
        </StackPanel>

        <!-- Break -->
        <StackPanel Visibility="{x:Bind ViewModel.IsOnBreak, Mode=OneWay}" Spacing="4">
            <TextBlock Text="BREAK" FontWeight="Bold" FontSize="18"
                       Foreground="{ThemeResource SystemFillColorCautionBrush}" />
            <TextBlock Text="{x:Bind ViewModel.BreakRemainingText, Mode=OneWay}"
                       FontFamily="Consolas" FontSize="24" />
            <Button Content="End Break" Style="{StaticResource AccentButtonStyle}"
                    Command="{x:Bind ViewModel.EndBreakCommand}" Margin="0,8,0,0" />
        </StackPanel>
    </StackPanel>
</Border>
```

### 4.3 CREATE セクション (既存 New Tile 改善)

```xml
<StackPanel Spacing="8">
    <TextBlock Text="CREATE" FontWeight="Bold" FontSize="12"
               Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
    <TextBox PlaceholderText="What will you work on?"
             Text="{x:Bind ViewModel.NewTileTitle, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
             KeyDown="OnCreateTileKeyDown" />
    <TextBox PlaceholderText="First action (optional)"
             Text="{x:Bind ViewModel.NewTileNextAction, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
    <StackPanel Orientation="Horizontal" Spacing="8">
        <Button Content="Create" Command="{x:Bind ViewModel.CreateTileCommand}" />
        <Button Content="Create &amp; Start" Style="{StaticResource AccentButtonStyle}"
                Command="{x:Bind ViewModel.CreateAndStartTileCommand}" />
    </StackPanel>
</StackPanel>
```

### 4.4 TODAY セクション (新規: タイムライン)

```xml
<StackPanel Spacing="8">
    <TextBlock Text="TODAY" FontWeight="Bold" FontSize="12"
               Foreground="{ThemeResource TextFillColorSecondaryBrush}" />

    <TextBlock Text="No activity yet. Create a tile and start working!"
               Visibility="{x:Bind ViewModel.IsTimelineEmpty, Mode=OneWay}"
               Foreground="{ThemeResource TextFillColorTertiaryBrush}"
               FontStyle="Italic" />

    <ListView ItemsSource="{x:Bind ViewModel.TimelineSegments, Mode=OneWay}"
              SelectionMode="None" MaxHeight="240">
        <ListView.ItemTemplate>
            <DataTemplate x:DataType="models:TimelineSegment">
                <Grid Padding="4,6" ColumnSpacing="12">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="44" />
                        <ColumnDefinition Width="4" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="Auto" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="{x:Bind TimeText}" FontFamily="Consolas" FontSize="12"
                               Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                               VerticalAlignment="Center" />
                    <Rectangle Grid.Column="1" Width="4" RadiusX="2" RadiusY="2"
                               VerticalAlignment="Stretch">
                        <Rectangle.Fill>
                            <SolidColorBrush Color="{x:Bind BadgeColor}" />
                        </Rectangle.Fill>
                    </Rectangle>
                    <TextBlock Grid.Column="2" Text="{x:Bind DisplayTitle}"
                               VerticalAlignment="Center" TextTrimming="CharacterEllipsis" />
                    <TextBlock Grid.Column="3" Text="{x:Bind StatusText}"
                               FontFamily="Consolas" FontSize="12"
                               VerticalAlignment="Center"
                               Foreground="{x:Bind StatusForeground}" />
                </Grid>
            </DataTemplate>
        </ListView.ItemTemplate>
    </ListView>
</StackPanel>
```

### 4.5 TILES セクション (既存のフィルター + リスト)

```xml
<!-- 既存の Tiles セクションの先頭に追加 -->
<TextBlock Text="TILES" FontWeight="Bold" FontSize="12"
           Foreground="{ThemeResource TextFillColorSecondaryBrush}" />

<!-- 空状態 (フィルター行の下、ListView の上) -->
<TextBlock Text="No tiles yet. Create one above!"
           Visibility="{x:Bind ViewModel.IsTilesEmpty, Mode=OneWay}"
           Foreground="{ThemeResource TextFillColorTertiaryBrush}"
           FontStyle="Italic" />
```

既存の `<TextBlock Text="Tiles" .../>` を "TILES" セクションヘッダーに置換

### 4.6 MEMO セクション (既存のまま、ラベル変更)

```xml
<!-- "Quick Memo" を "MEMO" に変更 -->
<TextBlock Text="MEMO" FontWeight="Bold" FontSize="12"
           Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
```

### 検証
- [ ] ビルド成功
- [ ] 起動時に NOW (Idle + ガイダンス), CREATE, TODAY (空状態), TILES (空状態), MEMO の5セクション表示
- [ ] タイマーが FontSize=24 で大きく表示される

---

## Step 5: MainWindow.xaml.cs + 仕上げ

### 5.1 Enter キーハンドラ

```csharp
private void OnCreateTileKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
{
    if (e.Key == Windows.System.VirtualKey.Enter)
    {
        _ = ViewModel.CreateTileCommand.ExecuteAsync(null);
        e.Handled = true;
    }
}
```

### 5.2 Visibility ↔ bool 変換の注意

WinUI 3 の `x:Bind` は `bool → Visibility` を自動変換する。
既存コードで `IsIdle`, `IsWorking`, `IsOnBreak` が `bool` で `Visibility` にバインドされている実績があるため、
`IsTimelineEmpty`, `IsTilesEmpty` も `bool` のまま `Visibility` バインドで動く

### 検証
- [ ] Enter キーでタイル作成できる
- [ ] タイル0件時に "No tiles yet" が表示される
- [ ] タイル作成後に表示が消える

---

## 全体検証手順

1. `dotnet build -r win-x64` でビルド成功
2. `dotnet run -r win-x64 --launch-profile "TastileDesktop (Unpackaged)"` で起動
3. **初回起動**: NOW="Idle" + "Create your first tile...", TODAY="No activity yet", TILES="No tiles yet"
4. **タイル作成**: "What will you work on?" に入力 → Enter → TILES に追加、Idle ガイダンス更新
5. **Create & Start**: タイトル入力 → "Create & Start" → NOW が WORKING 状態、タイマー FontSize=24 で表示
6. **タイムライン**: TODAY に "HH:mm ██ {title} ▸ now" が表示
7. **Break**: "Break (5 min)" → NOW が BREAK、タイムラインに Break セグメント追加
8. **Complete**: "Complete" → NOW が Idle、タイムラインにセグメント完了 (duration 表示)
9. **複数セグメント**: 別のタイル作成 → Start → タイムラインに複数行が時系列で表示

## 実行順序

```
Step 1 (API + モデル)         → 基盤、依存なし
Step 2 (ViewModel ロジック)   → Step 1 のモデルを使用
Step 3 (PollingService 統合)  → Step 2 のメソッドを呼ぶ
Step 4 (XAML レイアウト)      → Step 2 のバインディングを参照
Step 5 (仕上げ)               → Step 4 の UI に追加
```
