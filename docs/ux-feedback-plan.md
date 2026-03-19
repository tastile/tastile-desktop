# tastile-desktop UX 改善 フィードバックプラン

## 実装状況: 全 Step 完了、ビルド成功

### 変更済みファイル一覧

| ファイル | 変更内容 |
|---------|---------|
| `Models/ApiModels.cs` | TimelineSegment record 追加 (BadgeColor, StatusForeground, DisplayTitle etc.) |
| `Services/CoreApiClient.cs` | `GetEventsRawAsync()` 追加 (`/debug/events` の raw JSON 取得) |
| `Services/PollingService.cs` | `_lastEventCount` + `TimelineChanged` イベント + event_count 変更検知 |
| `ViewModels/MainViewModel.cs` | NewTileNextAction, TimelineSegments, IdleGuidanceText, CreateAndStartTile, RefreshTimelineAsync, IsTilesEmpty, HasNextAction |
| `MainWindow.xaml` | NOW/CREATE/TODAY/TILES/MEMO 5セクション構成 |
| `MainWindow.xaml.cs` | `OnCreateTileKeyDown` Enter キーハンドラ |

---

## 手動検証チェックリスト

### 事前準備
```bash
cd tastile-desktop
dotnet run -r win-x64 --project src/TastileDesktop
```
デーモンが自動起動されるか確認 (localhost:3140)

### 1. 初回起動時の表示確認
- [ ] NOW セクション: "Idle" + "Create your first tile above to get started." が表示される
- [ ] CREATE セクション: "What will you work on?" プレースホルダーが見える
- [ ] "First action (optional)" テキストボックスが見える
- [ ] "Create" と "Create & Start" ボタンが並んでいる
- [ ] TODAY セクション: "No activity yet. Create a tile and start working!" が表示される
- [ ] TILES セクション: "No tiles yet. Create one above!" が表示される
- [ ] MEMO セクション: 既存通り表示される

### 2. タイル作成 (Create のみ)
- [ ] "What will you work on?" にタイトルを入力
- [ ] Enter キー → タイルが作成される (TILES に追加)
- [ ] テキストボックスがクリアされる
- [ ] NOW の Idle ガイダンスが "1 tile(s) ready — click one below to start." に更新される
- [ ] TILES の "No tiles yet" が消える

### 3. Create & Start
- [ ] タイトル入力 → "Create & Start" クリック
- [ ] タイルが作成され即座に開始される
- [ ] NOW が WORKING 状態になる
- [ ] タイマーが **FontSize=24** で大きく表示される (以前の13pxより明確に大きい)
- [ ] タイトルが表示される
- [ ] "Complete" と "Break (5 min)" ボタンが表示される

### 4. next_action の表示
- [ ] "First action (optional)" に値を入れて Create & Start
- [ ] NOW セクションで next_action が表示される
- [ ] TILES リストでも "→ {next_action}" が2行目に表示される

### 5. タイムライン (TODAY)
- [ ] WORKING 状態中に TODAY セクションに "HH:mm ██ {title} ▸ now" が表示される
- [ ] 時刻 (HH:mm) が Consolas フォントで表示される
- [ ] 色付きバー: 青 (work) / 緑 (break) / グレー (idle)
- [ ] "▸ now" が緑色で表示される (進行中)

### 6. Break → タイムライン更新
- [ ] "Break (5 min)" クリック → NOW が BREAK 状態
- [ ] BREAK タイマーが FontSize=24 で表示される
- [ ] TODAY にブレークセグメントが追加される (緑バー)
- [ ] "End Break" → NOW が WORKING に戻る
- [ ] タイムラインにブレーク終了が反映 (duration 表示)

### 7. Complete → タイムライン更新
- [ ] "Complete" クリック → NOW が Idle に戻る
- [ ] タイムラインの作業セグメントが終了状態 (duration 表示、"▸ now" → "{N}m")
- [ ] Idle ガイダンスが状態に応じて更新される

### 8. 複数セグメント
- [ ] 別のタイルを作成 → Start → タイムラインに2つ目のセグメント追加
- [ ] タイムラインが時系列順 (上が古い、下が新しい)
- [ ] 最大高さ 240px でスクロール可能

---

## 既知の問題・リスク

### 高リスク
1. **`/debug/events` の JSON 構造不一致の可能性**: `segment_started`/`segment_ended` がデーモンバージョンによって構造が異なる可能性。実データで確認が必要
2. **タイムラインのイベント重複**: `segment_started(mode=break)` と `break_started` の両方が発火する場合、`openBreak != null` チェックで片方をスキップするが、イベント順序によっては漏れる可能性

### 中リスク
3. **`x:Bind` の SolidColorBrush バインド**: `Foreground="{x:Bind StatusForeground}"` が DataTemplate 内で動作するか。動かない場合は `StatusForegroundBrush` を文字列 (Hex) にして Converter 経由にする必要がある
4. **`bool → Visibility` の自動変換**: `IsTimelineEmpty`, `IsTilesEmpty` が bool で XAML の Visibility にバインドされている。WinUI 3 の x:Bind は bool→Visibility 自動変換するが、バージョンによっては動かない場合がある。既存コードで `IsIdle` (bool) が Visibility に直接バインドされているので同じパターンで動くはず

### 低リスク
5. **タイムライン更新の async void**: `_pollingService.TimelineChanged += async () => { await RefreshTimelineAsync(); }` は async void ラムダ。例外がハンドルされない可能性
6. **DateTime.Parse のロケール依存**: ISO8601 形式なら問題ないが、`DateTime.Parse` はカルチャ依存。`DateTime.Parse(..., CultureInfo.InvariantCulture)` にすべき

---

## 修正案 (次のイテレーション向け)

### P0: 動作に必須
- [ ] 実際にデーモンを起動してタイムラインの JSON パース結果をデバッグ出力で確認
- [ ] `SolidColorBrush` バインドが動かない場合、`StatusForeground` を `string` (Hex) に変更し、XAML 側で Converter を使用

### P1: 品質改善
- [ ] `DateTime.Parse` に `CultureInfo.InvariantCulture` を追加
- [ ] async void ラムダを `SafeFireAndForget` パターンに変更
- [ ] タイムラインが当日分のみ表示されることのテスト

### P2: UX 改善
- [ ] タイムライン項目クリック → 詳細表示 or タイルへジャンプ
- [ ] TODAY セクションの合計作業時間サマリー表示
- [ ] CREATE セクションの TextBox にフォーカスを当てるショートカット (Ctrl+N など)
- [ ] WORKING 状態の NOW セクションに背景色をつけてさらに目立たせる

---

## ビルド & 起動コマンド

```bash
cd tastile-desktop

# ビルド
dotnet build -r win-x64 src/TastileDesktop/TastileDesktop.csproj

# 起動
dotnet run -r win-x64 --project src/TastileDesktop
```
