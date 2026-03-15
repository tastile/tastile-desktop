# tastile-desktop 実装プラン: 不可避の介入システム

## コンセプト

Tastile Desktop は「Web を再現するGUI」ではない
pomodoroom の思想を継承し、**Windows ネイティブの力で介入する実行制御システム**として再構築する

### 設計原則
1. **不可避性**: OS レベルで割り込み、ユーザーが無視できない通知を出す
2. **常駐性**: システムトレイに常駐し、バックグラウンドで監視し続ける
3. **最小操作**: タイル操作は最小限のクリックで完了する
4. **daemon 依存**: ビジネスロジックは tastile-core (Rust daemon) に任せ、Desktop は UI + OS 介入に特化

---

## 現状

### 既にあるもの
- WinUI 3 プロジェクト骨格 (.NET 10, AGP 1.7)
- MainWindow: タイル一覧、作成、開始、完了、休憩、メモ
- CoreApiClient: daemon HTTP API クライアント (localhost:3140)
- DaemonManager: daemon プロセス管理
- ApiModels: TileView, ActiveTileResponse, ExecutionResponse, CommandResponse
- 2秒ポーリングによるリアルタイム更新

### daemon (tastile-core) が提供する API
**GET**:
- `/health` - ヘルスチェック
- `/read/tiles` - 全タイル
- `/read/active-tile` - アクティブタイル (phase, phase_started_at, phase_ends_at 含む)
- `/read/execution` - 実行状態
- `/debug/events` - イベント履歴
- `/version` - バージョン

**POST**:
- `/commands/tile/create` - タイル作成
- `/commands/tile/start` - タイル開始
- `/commands/tile/complete` - タイル完了
- `/commands/tile/defer` - タイル延期
- `/commands/tile/extend` - フェーズ延長
- `/commands/memo/attach` - メモ添付
- `/commands/break/start` - 休憩開始
- `/commands/break/end` - 休憩終了

### daemon の TickLoop
- 1秒間隔で実行
- プロンプト評価 (一定時間経過後の介入判定)
- Supabase 同期 (60秒間隔、オプション)

### 不足しているもの
- ❌ システムトレイ常駐
- ❌ トースト通知 (Windows ネイティブ)
- ❌ 不可避ダイアログ (topmost, フォーカスキャプチャ)
- ❌ 認証フロー
- ❌ MVVM 設計
- ❌ 複数画面 (設定, アカウント)
- ❌ アプリアイコン / アセット
- ❌ MSIX パッケージ

---

## アーキテクチャ

```
┌─────────────────────────────────────────┐
│            tastile-daemon               │
│  (Rust, localhost:3140)                 │
│  ┌─────────┐ ┌──────────┐ ┌─────────┐  │
│  │TickLoop │ │ EventStore│ │Supabase │  │
│  │(1s)     │ │ (SQLite) │ │Sync(60s)│  │
│  └────┬────┘ └──────────┘ └─────────┘  │
│       │ prompt_due event                │
└───────┼─────────────────────────────────┘
        │ HTTP (JSON)
┌───────┼─────────────────────────────────┐
│       ▼  tastile-desktop                │
│  ┌─────────────┐  ┌──────────────────┐  │
│  │PollingService│  │InterventionEngine│  │
│  │(2s interval) │  │(prompt detection)│  │
│  └──────┬──────┘  └────────┬─────────┘  │
│         │                  │            │
│  ┌──────▼──────┐  ┌───────▼────────┐   │
│  │ MainWindow  │  │InterventionWin │   │
│  │ (通常UI)    │  │(Topmost,不可避)│   │
│  └─────────────┘  └────────────────┘   │
│  ┌─────────────┐  ┌────────────────┐   │
│  │ TrayIcon    │  │ Toast通知      │   │
│  │ (常駐)      │  │ (Windows)      │   │
│  └─────────────┘  └────────────────┘   │
└─────────────────────────────────────────┘
```

---

## Step 1: MVVM リファクタ + サービス層

### 目的
MainWindow のモノリシックなコードを MVVM + サービスに分離し、拡張可能にする

### 1.1 サービス層の抽出

**`Services/PollingService.cs`** (新規)
- 2秒ポーリングを独立したサービスに
- `ActiveTileChanged` イベント
- `ExecutionStateChanged` イベント
- `ConnectionStatusChanged` イベント
- ポーリング結果をキャッシュし、各 ViewModel が購読

**`Services/CoreApiClient.cs`** (既存、変更なし)
- そのまま使う

**`Services/DaemonManager.cs`** (既存、変更なし)
- そのまま使う

### 1.2 ViewModel 作成

**`ViewModels/MainViewModel.cs`** (新規)
- `ObservableCollection<TileListItem> Tiles`
- `ActiveTileResponse? ActiveTile`
- `bool IsConnected`
- `string StatusMessage`
- `RelayCommand CreateTileCommand`
- `RelayCommand CompleteTileCommand`
- `RelayCommand StartBreakCommand`
- `RelayCommand EndBreakCommand`
- `RelayCommand<string> StartTileCommand`
- `RelayCommand SendMemoCommand`
- CommunityToolkit.Mvvm の `ObservableObject` + `RelayCommand` 使用

### 1.3 MainWindow.xaml.cs をスリム化
- ViewModel にバインド
- コードビハインドはウィンドウ管理のみ

### 検証
- ビルド成功
- 既存機能が動作すること

---

## Step 2: システムトレイ常駐

### 目的
ウィンドウを閉じてもバックグラウンドで動き続ける常駐アプリにする

### 2.1 `Services/TrayIconService.cs` (新規)
- `H.NotifyIcon.WinUI` パッケージ使用 (WinUI 3 対応のトレイアイコンライブラリ)
- NuGet: `H.NotifyIcon.WinUI` (最新版)
- トレイアイコン表示
- 右クリックメニュー:
  - 「Show Tastile」→ メインウィンドウを表示
  - 「Quick Create...」→ テキスト入力ダイアログ → タイル作成
  - 「Current: {タイル名}」→ 現在のアクティブタイル表示 (disabled)
  - 「Complete」→ アクティブタイルを完了
  - 「Break (5 min)」→ 休憩開始
  - 「Quit」→ daemon を停止してアプリ終了

### 2.2 ウィンドウのクローズ動作変更
- `MainWindow.Closed` でウィンドウを非表示にするだけ (アプリは終了しない)
- トレイアイコンは常に表示
- 「Quit」メニューのみで完全終了

### 2.3 スタートアップ起動 (オプション)
- Windows の StartupTask として登録可能にする
- 設定画面で ON/OFF 切り替え

### 依存追加 (app.csproj)
```xml
<PackageReference Include="H.NotifyIcon.WinUI" Version="2.2.0" />
```

### 検証
- ウィンドウ閉じてもトレイに残る
- トレイからウィンドウ復帰
- 右クリックメニューの全項目動作

---

## Step 3: 不可避の介入ダイアログ

### 目的
pomodoroom の核心機能。一定時間作業したら**回避不能なダイアログ**を表示し、ユーザーに判断を強制する

### 3.1 介入トリガーの検出

**`Services/InterventionEngine.cs`** (新規)
PollingService から `ActiveTileResponse` を受け取り、介入が必要か判定:

```
判定ロジック:
1. phase == "Work" かつ phase_started_at から 25分以上経過
   → 介入ダイアログ表示
2. phase == "Break" かつ phase_ends_at を過ぎている
   → 休憩終了ダイアログ表示
3. phase == "Idle" かつ 最後の完了から 5分以上経過
   → 次のタイル開始を促すダイアログ表示
```

- 介入の再表示間隔: 5分 (無視されたら再度表示)
- 介入中にユーザーがアクションしたらリセット

### 3.2 介入ウィンドウ

**`Views/InterventionWindow.xaml`** + **`InterventionWindow.xaml.cs`** (新規)

特性:
- **Topmost**: 常に最前面
- **フルスクリーンに近いオーバーレイ**: 他のウィンドウを操作させない
- **閉じるボタンなし**: X ボタンを無効化、アクションボタンのみで閉じる
- **ESC 無効**: キーボードで閉じられない
- **半透明背景**: デスクトップ全体を覆う暗いオーバーレイ

表示内容 (Work 介入の場合):
```
┌─────────────────────────────────────┐
│                                     │
│   You've been working for 25 min    │
│   on: "タイル名"                     │
│                                     │
│   [ Continue (25 min) ]             │
│   [ Take a Break (5 min) ]          │
│   [ Complete ]                      │
│                                     │
└─────────────────────────────────────┘
```

表示内容 (Idle 介入の場合):
```
┌─────────────────────────────────────┐
│                                     │
│   What should you work on?          │
│                                     │
│   Ready tiles:                      │
│   [ タイル1 ]                       │
│   [ タイル2 ]                       │
│   [ Create new tile... ]            │
│                                     │
└─────────────────────────────────────┘
```

### 3.3 実装上の注意
- `AppWindow.SetPresenter(OverlappedPresenter)` で Topmost 設定
- `OverlappedPresenter.IsAlwaysOnTop = true`
- `OverlappedPresenter.IsResizable = false`
- `OverlappedPresenter.IsMinimizable = false`
- `SetForegroundWindow` Win32 API で強制フォーカス

### 検証
- 25分経過で介入ダイアログ出現
- X ボタン / ESC で閉じられないこと
- アクションボタンでのみ閉じること
- 5分後に再表示されること

---

## Step 4: Windows トースト通知

### 目的
介入ダイアログの前段として、穏やかな通知を出す。無視されたら Step 3 の不可避ダイアログへエスカレート

### 4.1 通知サービス

**`Services/NotificationService.cs`** (新規)
- `Microsoft.Toolkit.Uwp.Notifications` パッケージ (WinUI 3 対応)
- NuGet: `Microsoft.Toolkit.Uwp.Notifications`

通知パターン:
1. **15分経過** → トースト通知 「15 min on "タイル名". Keep going!」
   - アクションボタン: Continue / Break / Complete
2. **25分経過** → 不可避ダイアログ (Step 3)
3. **休憩終了** → トースト通知 「Break is over. Ready to continue?」
   - 無視されたら 1分後に不可避ダイアログ
4. **Idle 5分** → トースト通知 「What's next?」
   - 無視されたら 5分後に不可避ダイアログ

### 4.2 通知アクションハンドリング
- トースト通知のボタンクリックを受け取り、daemon に API コール
- `ToastNotificationManagerCompat.OnActivated` でハンドリング

### 4.3 エスカレーションフロー
```
0分    作業開始
15分   トースト通知 (穏やか)
25分   不可避ダイアログ (強制)
30分   再度不可避ダイアログ (5分ごと)
```

### 依存追加 (app.csproj)
```xml
<PackageReference Include="Microsoft.Toolkit.Uwp.Notifications" Version="7.1.3" />
```

### 検証
- 15分でトースト通知
- トースト通知のボタン動作
- 25分で不可避ダイアログにエスカレート

---

## Step 5: 認証 + Supabase 同期設定

### 目的
daemon の Supabase 同期をデスクトップ UI から設定可能にする
(認証自体は daemon が処理、Desktop は設定 UI のみ)

### 5.1 daemon の同期 API 確認

daemon が提供する同期関連のエンドポイントを確認:
- 同期状態の取得 API があれば使う
- なければ daemon 側に追加が必要 (別プラン)

### 5.2 設定画面

**`Views/SettingsWindow.xaml`** + **`SettingsWindow.xaml.cs`** (新規)

設定項目:
- **介入タイミング**: 作業 N 分後に通知 (デフォルト 15分)
- **不可避ダイアログ**: 作業 N 分後に強制表示 (デフォルト 25分)
- **休憩時間**: デフォルト休憩 N 分 (デフォルト 5分)
- **Idle 介入**: Idle N 分後に通知 (デフォルト 5分)
- **スタートアップ起動**: ON/OFF
- **Supabase 同期**: ON/OFF + ステータス表示

### 5.3 設定の永続化

**`Services/SettingsService.cs`** (新規)
- `%APPDATA%\Tastile\settings.json` に保存
- JSON シリアライズ/デシリアライズ
- デフォルト値付き

```csharp
public record TastileSettings
{
    public int ToastNotifyMinutes { get; init; } = 15;
    public int InterventionMinutes { get; init; } = 25;
    public int DefaultBreakMinutes { get; init; } = 5;
    public int IdlePromptMinutes { get; init; } = 5;
    public int InterventionRepeatMinutes { get; init; } = 5;
    public bool LaunchAtStartup { get; init; } = false;
}
```

### 検証
- 設定画面の表示と保存
- 設定値が通知/介入タイミングに反映されること

---

## Step 6: UI 仕上げ + パッケージング

### 6.1 アプリアイコン / アセット
- Square44x44Logo.png
- Square150x150Logo.png
- Wide310x150Logo.png
- StoreLogo.png
- SplashScreen.png
- トレイアイコン用 .ico ファイル

### 6.2 MainWindow UI 改善
- タイルのスワイプ削除 or 右クリックメニュー (完了/延期/削除)
- タイル詳細表示 (next_action, done_definition)
- 検索/フィルター (Ready/Started/Done)

### 6.3 ダークテーマ対応
- 現状 Theme.kt (Android) のように system テーマ追従
- WinUI 3 は標準で対応済み、確認のみ

### 6.4 MSIX パッケージ生成
```bash
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=false
```
- 署名設定
- tastile-daemon.exe のバンドル確認

### 6.5 CLAUDE.md 更新

### 検証
- MSIX パッケージ生成成功
- インストール → 起動 → 全機能動作

---

## 依存関係フロー

```
Step 1 (MVVM) → Step 2 (トレイ) → Step 3 (介入ダイアログ) → Step 4 (トースト通知)
                                                            → Step 5 (設定)
                                    Step 3 + 4 + 5 → Step 6 (仕上げ)
```

Step 3 と 4 は密に関連 (エスカレーション連携)
Step 5 は Step 3-4 の設定値を定義するため、並列よりは直列推奨

---

## ファイル作成/変更リスト

```
TastileDesktop/
├── Services/
│   ├── CoreApiClient.cs          # 既存 (変更なし)
│   ├── DaemonManager.cs          # 既存 (変更なし)
│   ├── PollingService.cs         # Step 1 新規
│   ├── InterventionEngine.cs     # Step 3 新規
│   ├── NotificationService.cs    # Step 4 新規
│   ├── SettingsService.cs        # Step 5 新規
│   └── TrayIconService.cs        # Step 2 新規
├── ViewModels/
│   ├── MainViewModel.cs          # Step 1 新規
│   └── SettingsViewModel.cs      # Step 5 新規
├── Views/
│   ├── InterventionWindow.xaml    # Step 3 新規
│   ├── InterventionWindow.xaml.cs # Step 3 新規
│   ├── SettingsWindow.xaml        # Step 5 新規
│   └── SettingsWindow.xaml.cs     # Step 5 新規
├── Models/
│   └── ApiModels.cs              # 既存 (変更なし)
├── MainWindow.xaml               # Step 1 変更 (ViewModel バインド)
├── MainWindow.xaml.cs            # Step 1 変更 (スリム化)
├── App.xaml.cs                   # Step 2 変更 (トレイ + 終了制御)
├── TastileDesktop.csproj         # Step 2 変更 (NuGet 追加)
└── Package.appxmanifest          # Step 6 変更 (アセット)
```

新規ファイル: 9
変更ファイル: 5
変更なし: 3

---

## 介入エスカレーションの全体像

```
時間軸 (作業中)
─────────────────────────────────────────────────
0 min   タイル開始
        │
15 min  トースト通知 📋
        │  「15 min on "タイル名"」
        │  [Continue] [Break] [Complete]
        │
        │  ← ユーザーがアクション → リセット
        │  ← 無視 →
        │
25 min  🚨 不可避ダイアログ (Topmost, フルスクリーン)
        │  「25 min 経過。続ける？」
        │  [Continue 25min] [Break 5min] [Complete]
        │
        │  ← ユーザーがアクション → リセット or 遷移
        │
30 min  🚨 再度不可避ダイアログ (5分ごと繰り返し)
        │
─────────────────────────────────────────────────

時間軸 (休憩中)
─────────────────────────────────────────────────
0 min   休憩開始 (5 min)
        │
5 min   トースト通知 📋
        │  「Break is over!」
        │
6 min   🚨 不可避ダイアログ (1分猶予)
        │  「何に取り組む？」
        │  [前のタイルを続ける] [Ready タイルリスト]
─────────────────────────────────────────────────

時間軸 (Idle)
─────────────────────────────────────────────────
0 min   前のタイル完了 / アプリ起動
        │
5 min   トースト通知 📋
        │  「What's next?」
        │
10 min  🚨 不可避ダイアログ
        │  「何に取り組む？」
        │  [Ready タイルリスト] [Create new]
─────────────────────────────────────────────────
```

---

## 検証チェックリスト

各ステップ完了時:
- [x] `dotnet build -r win-x64` 成功
- [x] 既存機能が壊れていないこと

最終検証:
- [x] daemon 自動起動
- [x] トレイアイコン常駐
- [x] ウィンドウ閉じても常駐
- [x] 15分でトースト通知
- [x] 25分で不可避ダイアログ
- [x] ダイアログを X / ESC で閉じられないこと
- [x] ダイアログのアクション → daemon API コール成功
- [x] 休憩終了通知 → ダイアログエスカレート
- [x] Idle 通知 → ダイアログエスカレート
- [x] 設定画面でタイミング変更 → 反映
- [ ] MSIX パッケージ生成
- [ ] アプリアイコン / アセット配置
- [ ] タイル右クリックメニュー
- [ ] ライフサイクルフィルター

---

## 実装済みステップの状況

| Step | 状態 | 備考 |
|------|------|------|
| 1 | ✅ 完了 | MVVM + PollingService + MainViewModel |
| 2 | ✅ 完了 | トレイ常駐 (H.NotifyIcon.WinUI) |
| 3 | ✅ 完了 | 介入ダイアログ (バグ修正済み) |
| 4 | ✅ 完了 | NotificationService + 2段階エスカレーション |
| 5 | ✅ 完了 | SettingsService + SettingsWindow (スタートアップはプレースホルダー) |
| 6 | ⚠️ 部分完了 | ダークテーマ OK、CLAUDE.md 更新済み。アセット/右クリック/フィルター未実装 |
| 7 | ✅ 完了 | 4件のバグ修正 (X ボタン, Ready タイル, ハッシュ比較, Quick Create) |

---

## Step 7: 既存実装のバグ修正

Step 1-3 の実装レビューで発見された問題を修正する。
**Step 4-6 の実装前にこのステップを完了すること。**

### 7.1 [BUG] InterventionWindow が X ボタンで閉じられる

**ファイル**: `Views/InterventionWindow.xaml.cs`
**問題**: `OnWindowClosed()` で `args.Handled = true` を設定していないため、X ボタンやタスクバーの「閉じる」でウィンドウが閉じられる。不可避ダイアログの核心が破綻する
**修正**:
```csharp
private bool _actionTaken = false;

private void OnWindowClosed(object sender, WindowEventArgs args)
{
    if (!_actionTaken)
    {
        args.Handled = true; // Block close unless action button was clicked
    }
}

// Each action button handler must set _actionTaken = true before Close()
private void OnContinue(object sender, RoutedEventArgs e)
{
    _actionTaken = true;
    // ... existing logic ...
    Close();
}
```

### 7.2 [BUG] Idle 介入の Ready タイルリストが仮データ

**ファイル**: `Views/InterventionWindow.xaml.cs`
**問題**: `ReadyTilesList.ItemsSource = new[] { "Sample Tile 1", "Sample Tile 2" }` がハードコードされている
**修正**: コンストラクタで PollingService の `CurrentTiles` から Ready タイルを取得して表示
```csharp
// InterventionWindow constructor or ShowIdleIntervention()
var readyTiles = pollingService.CurrentTiles?.Tiles?
    .Where(t => t.Lifecycle == "Ready")
    .ToList() ?? new List<TileView>();
ReadyTilesList.ItemsSource = readyTiles;
```
- タイルクリック時に `_api.StartTileAsync(tile.Id)` を呼び、介入を閉じる
- Ready タイルが 0 件の場合は "No ready tiles. Create one?" メッセージ + 作成フォーム

### 7.3 [BUG] PollingService.HasTilesChanged() が常に true を返す

**ファイル**: `Services/PollingService.cs`
**問題**: タイル一覧が変更されていなくても毎回 `TilesChanged` イベントが発火し、UI が無駄に更新される
**修正**: タイル ID + lifecycle のハッシュで比較
```csharp
private string? _lastTilesHash;

private bool HasTilesChanged(TilesResponse? tiles)
{
    if (tiles?.Tiles == null) return _lastTilesHash != null;
    var hash = string.Join(",", tiles.Tiles.Select(t => $"{t.Id}:{t.Lifecycle}"));
    if (hash == _lastTilesHash) return false;
    _lastTilesHash = hash;
    return true;
}
```

### 7.4 [BUG] Quick Create がただの Show

**ファイル**: `Services/TrayIconService.cs`
**問題**: トレイメニューの「Quick Create...」が `ShowMainWindow()` を呼ぶだけで、タイル作成できない
**修正**: ContentDialog でインライン入力ダイアログを表示
```csharp
private async void OnQuickCreate()
{
    // Show main window first (ContentDialog needs a XamlRoot)
    ShowMainWindow();

    var textBox = new TextBox { PlaceholderText = "Tile title..." };
    var dialog = new ContentDialog
    {
        Title = "Quick Create",
        Content = textBox,
        PrimaryButtonText = "Create",
        CloseButtonText = "Cancel",
        XamlRoot = _mainWindow.Content.XamlRoot,
    };

    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
    {
        var title = textBox.Text?.Trim();
        if (!string.IsNullOrEmpty(title))
            await _api.CreateTileAsync(title);
    }
}
```

### 検証
- [ ] InterventionWindow が X ボタン / ESC で閉じられないこと
- [ ] InterventionWindow がアクションボタンでのみ閉じること
- [ ] Idle 介入で実際の Ready タイルが表示されること
- [ ] タイル一覧が変更されない時に UI が再描画されないこと
- [ ] トレイの Quick Create で新規タイル作成できること

---

## Step 4: Windows トースト通知 (未実装 → 実装)

### 4.1 NuGet パッケージ追加

**ファイル**: `TastileDesktop.csproj`
```xml
<PackageReference Include="Microsoft.Toolkit.Uwp.Notifications" Version="7.1.3" />
```

### 4.2 `Services/NotificationService.cs` (新規)

責務:
- Windows トースト通知の表示
- トースト通知のアクションボタンハンドリング
- InterventionEngine からの通知要求を受け取る

```csharp
public class NotificationService : IDisposable
{
    private readonly CoreApiClient _api;

    public NotificationService(CoreApiClient api)
    {
        _api = api;
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;
    }

    // Work phase notification (15 min)
    public void ShowWorkReminder(string tileTitle, int elapsedMinutes)
    {
        new ToastContentBuilder()
            .AddText($"{elapsedMinutes} min on \"{tileTitle}\"")
            .AddText("Keep going, or take a break?")
            .AddButton(new ToastButton("Continue", "action=continue"))
            .AddButton(new ToastButton("Break", "action=break"))
            .AddButton(new ToastButton("Complete", "action=complete"))
            .Show();
    }

    // Break over notification
    public void ShowBreakOverReminder()
    {
        new ToastContentBuilder()
            .AddText("Break is over!")
            .AddText("Ready to get back to work?")
            .AddButton(new ToastButton("Continue", "action=endbreak"))
            .Show();
    }

    // Idle notification (5 min)
    public void ShowIdleReminder()
    {
        new ToastContentBuilder()
            .AddText("What's next?")
            .AddText("You've been idle. Pick a tile to work on.")
            .Show();
    }

    private async void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        var args = ToastArguments.Parse(e.Argument);
        var action = args["action"];
        switch (action)
        {
            case "continue": break; // Do nothing, user acknowledged
            case "break": await _api.StartBreakAsync(5); break;
            case "complete": await _api.CompleteTileAsync(); break;
            case "endbreak": await _api.EndBreakAsync(); break;
        }
    }

    public void Dispose()
    {
        ToastNotificationManagerCompat.Uninstall();
    }
}
```

### 4.3 InterventionEngine にトースト通知を統合

**ファイル**: `Services/InterventionEngine.cs` (変更)

現在の介入判定ロジックを拡張し、2段階にする:

```
判定ロジック (変更後):

Work phase:
  1. phase_started_at から ToastNotifyMinutes (デフォルト 15分) 経過
     → NotificationService.ShowWorkReminder() (トースト通知)
     → _lastToastShown を記録
  2. phase_started_at から InterventionMinutes (デフォルト 25分) 経過
     → InterventionWindow 表示 (不可避ダイアログ)
  3. 以降 InterventionRepeatMinutes (デフォルト 5分) ごとに再表示

Break phase:
  1. phase_ends_at を過ぎた
     → NotificationService.ShowBreakOverReminder() (トースト通知)
  2. phase_ends_at + 1分 経過
     → InterventionWindow 表示

Idle phase:
  1. 最後の完了から IdlePromptMinutes (デフォルト 5分) 経過
     → NotificationService.ShowIdleReminder() (トースト通知)
  2. 最後の完了から IdlePromptMinutes + 5分 経過
     → InterventionWindow 表示
```

新しいフィールド追加:
```csharp
private readonly NotificationService _notificationService;
private DateTimeOffset? _lastToastShown;
private bool _toastShownForCurrentPhase = false;
```

### 検証
- [ ] 15分でトースト通知が表示される
- [ ] トースト通知のボタンが動作する (Continue/Break/Complete)
- [ ] 25分で不可避ダイアログにエスカレートする
- [ ] 休憩終了でトースト → 1分後に不可避ダイアログ
- [ ] Idle 5分でトースト → 10分で不可避ダイアログ
- [ ] トーストで Continue を押すと介入タイマーがリセットされる

---

## Step 5: 設定画面 (未実装 → 実装)

### 5.1 `Services/SettingsService.cs` (新規)

```csharp
public class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tastile");
    private static readonly string SettingsFile =
        Path.Combine(SettingsDir, "settings.json");

    public TastileSettings Current { get; private set; } = new();

    public SettingsService()
    {
        Load();
    }

    public void Load()
    {
        if (!File.Exists(SettingsFile))
        {
            Current = new TastileSettings();
            return;
        }
        try
        {
            var json = File.ReadAllText(SettingsFile);
            Current = JsonSerializer.Deserialize<TastileSettings>(json) ?? new();
        }
        catch
        {
            Current = new TastileSettings();
        }
    }

    public void Save(TastileSettings settings)
    {
        Current = settings;
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json);
    }
}

public record TastileSettings
{
    public int ToastNotifyMinutes { get; init; } = 15;
    public int InterventionMinutes { get; init; } = 25;
    public int DefaultBreakMinutes { get; init; } = 5;
    public int IdlePromptMinutes { get; init; } = 5;
    public int InterventionRepeatMinutes { get; init; } = 5;
    public bool LaunchAtStartup { get; init; } = false;
}
```

### 5.2 `ViewModels/SettingsViewModel.cs` (新規)

- `ObservableObject` 継承
- 各設定値をプロパティとして公開
- `SaveCommand` で SettingsService.Save() + InterventionEngine に反映

### 5.3 `Views/SettingsWindow.xaml` + `.cs` (新規)

設定項目 UI:
```
┌─────────────────────────────────────┐
│  Settings                           │
│                                     │
│  Notifications                      │
│  ─────────────────────────────────  │
│  Toast reminder      [15] min       │
│  Force intervention  [25] min       │
│  Repeat intervention [5]  min       │
│                                     │
│  Breaks                             │
│  ─────────────────────────────────  │
│  Default break time  [5]  min       │
│  Idle prompt after   [5]  min       │
│                                     │
│  System                             │
│  ─────────────────────────────────  │
│  Launch at startup   [OFF]          │
│                                     │
│  [ Save ]  [ Cancel ]              │
└─────────────────────────────────────┘
```

- NumberBox (WinUI 3) を使用して数値入力
- ToggleSwitch でスタートアップ ON/OFF
- Save ボタンで JSON に永続化

### 5.4 InterventionEngine の設定値読み込み

**ファイル**: `Services/InterventionEngine.cs` (変更)
- コンストラクタで `SettingsService` を受け取る
- ハードコードされた `TimeSpan.FromMinutes(25)` 等を `_settingsService.Current.InterventionMinutes` に置換
- `UpdateSettings()` メソッドで設定変更をリアルタイム反映

### 5.5 設定画面への導線

- MainWindow のヘッダーバーに ⚙ ギアアイコンを追加
- トレイメニューに "Settings" 項目を追加
- クリックで SettingsWindow を開く

### 5.6 スタートアップ起動

- `Windows.ApplicationModel.StartupTask` API を使用
- MSIX パッケージの場合は `StartupTask.GetAsync()` → `RequestEnableAsync()`
- Unpackaged の場合はレジストリ `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` に登録

### 検証
- [ ] 設定画面が表示される
- [ ] 値を変更して Save → JSON に保存される
- [ ] アプリ再起動後に設定値が復元される
- [ ] 設定変更が介入タイミングに即座に反映される
- [ ] スタートアップ ON/OFF が動作する

---

## Step 6: UI 仕上げ + パッケージング (未実装 → 実装)

### 6.1 アプリアイコン / アセット

生成が必要なファイル:
- `Assets/Square44x44Logo.png` (44x44)
- `Assets/Square44x44Logo.targetsize-256.png` (256x256, アイコン用)
- `Assets/Square150x150Logo.png` (150x150)
- `Assets/Wide310x150Logo.png` (310x150)
- `Assets/StoreLogo.png` (50x50)
- `Assets/SplashScreen.png` (620x300)
- `Assets/tastile-tray.ico` (16/32/48/256 マルチサイズ)

デザイン: シンプルな "T" のアイコン、背景は Tastile ブランドカラー
トレイアイコン: 現在の生成アイコン "T" を .ico ファイルに置き換え

### 6.2 MainWindow UI 改善

**タイル右クリックメニュー**:
```xml
<MenuFlyout>
    <MenuFlyoutItem Text="Start" Icon="Play" Click="OnStartTile" />
    <MenuFlyoutItem Text="Complete" Icon="Accept" Click="OnCompleteTile" />
    <MenuFlyoutItem Text="Defer" Icon="Clock" Click="OnDeferTile" />
    <MenuFlyoutSeparator />
    <MenuFlyoutItem Text="Delete" Icon="Delete" Click="OnDeleteTile" />
</MenuFlyout>
```
- CoreApiClient に `DeleteTileAsync()` を追加 (daemon の `/commands/tile/delete` が存在するか確認が必要、なければ defer で代替)

**タイル詳細表示の改善**:
- next_action と done_definition を展開表示 (現在は ActiveTile のみ表示)
- TileListItem のテンプレートに2行目を追加

**フィルター**:
- SegmentedControl (WinUI 3 `RadioButtons` + 横並び) で All / Ready / Started / Done を切り替え
- MainViewModel に `SelectedFilter` プロパティ追加、フィルタリングロジック

### 6.3 ダークテーマ対応
- WinUI 3 は標準でシステムテーマ追従済み、確認のみ
- InterventionWindow の半透明背景がダークテーマでも視認性が良いか確認

### 6.4 MSIX パッケージ生成

ビルドコマンド:
```bash
dotnet publish -c Release -r win-x64
```

確認事項:
- [ ] tastile-daemon.exe がバンドルに含まれるか (Condition 付き Content Include)
- [ ] 署名設定 (開発時は `AppxPackageSigningEnabled=false` で OK)
- [ ] Package.appxmanifest のアセット参照が有効か

### 6.5 CLAUDE.md 更新

最新のアーキテクチャを反映:
```markdown
## Architecture
- Services/
  - CoreApiClient.cs — Daemon HTTP API client (localhost:3140)
  - DaemonManager.cs — Daemon process lifecycle
  - PollingService.cs — 2s polling + event dispatch
  - InterventionEngine.cs — Escalation logic (toast → intervention)
  - NotificationService.cs — Windows toast notifications
  - SettingsService.cs — JSON settings persistence
  - TrayIconService.cs — System tray icon + context menu
- ViewModels/
  - MainViewModel.cs — Main window state + commands
  - SettingsViewModel.cs — Settings form binding
- Views/
  - InterventionWindow.xaml — Unavoidable full-screen dialog
  - SettingsWindow.xaml — Settings panel
- Models/
  - ApiModels.cs — Daemon API DTOs
```

### 検証
- [ ] アセットが Package.appxmanifest で正しく参照される
- [ ] トレイアイコンが .ico ファイルで表示される
- [ ] タイル右クリックメニューの全項目が動作する
- [ ] フィルターでタイル一覧が切り替わる
- [ ] MSIX パッケージが生成される
- [ ] パッケージからインストール → 起動 → 全機能動作

---

## 更新された依存関係フロー

```
Step 1 (MVVM) ✅ → Step 2 (トレイ) ✅ → Step 3 (介入) ✅
                                          │
                                    Step 7 (バグ修正) ← 最優先
                                          │
                                    Step 4 (トースト通知)
                                          │
                                    Step 5 (設定)
                                          │
                                    Step 6 (仕上げ)
```

実行順序: **Step 7 → Step 4 → Step 5 → Step 6**

Step 7 のバグ修正が最優先。特に 7.1 (X ボタン無効化) は介入システムの核心
Step 4 (トースト) が Step 5 (設定) より先。設定画面では通知タイミングを設定するため、まず通知が動く必要がある

---

## 更新されたファイル作成/変更リスト

```
TastileDesktop/
├── Services/
│   ├── CoreApiClient.cs          # 既存 → Step 6 で DeleteTile 追加
│   ├── DaemonManager.cs          # 既存 (変更なし)
│   ├── PollingService.cs         # Step 1 済 → Step 7.3 で修正
│   ├── InterventionEngine.cs     # Step 3 済 → Step 4 で通知統合, Step 5 で設定統合
│   ├── NotificationService.cs    # Step 4 新規
│   ├── SettingsService.cs        # Step 5 新規
│   └── TrayIconService.cs        # Step 2 済 → Step 5 で Settings メニュー追加, Step 7.4 で QuickCreate修正
├── ViewModels/
│   ├── MainViewModel.cs          # Step 1 済 → Step 6 でフィルター追加
│   └── SettingsViewModel.cs      # Step 5 新規
├── Views/
│   ├── InterventionWindow.xaml    # Step 3 済 → Step 7.1, 7.2 で修正
│   ├── InterventionWindow.xaml.cs # Step 3 済 → Step 7.1, 7.2 で修正
│   ├── SettingsWindow.xaml        # Step 5 新規
│   └── SettingsWindow.xaml.cs     # Step 5 新規
├── Models/
│   └── ApiModels.cs              # 既存 (変更なし)
├── MainWindow.xaml               # Step 1 済 → Step 5 で ⚙ ボタン追加, Step 6 でフィルター追加
├── MainWindow.xaml.cs            # Step 1 済 (変更なし)
├── App.xaml.cs                   # Step 2 済 → Step 4 で NotificationService 初期化
├── TastileDesktop.csproj         # Step 2 済 → Step 4 で NuGet 追加
├── Package.appxmanifest          # Step 6 変更 (アセット)
├── Assets/                       # Step 6 新規 (アイコン群)
└── CLAUDE.md                     # Step 6 更新
```

新規ファイル: 4 (NotificationService, SettingsService, SettingsViewModel, SettingsWindow)
変更ファイル: 10
アセットファイル: 7

---

## Step 8: Step 6 残作業の修正

Step 4-5-7 は実装完了。Step 6 の残作業を完了させる

### 8.1 [MISSING] アプリアイコン / アセット配置

**問題**: `Assets/` フォルダが存在しない。`Package.appxmanifest` がアセットを参照しているが実ファイルがないため、MSIX パッケージ生成時にエラーになる

**対応**: 以下のファイルをプレースホルダーとして生成する。シンプルな "T" のアイコン、背景は `#0078D4` (Tastile ブランド青)

生成するファイル:
```
src/TastileDesktop/Assets/
├── Square44x44Logo.png              (44x44, タスクバー/スタートメニュー)
├── Square44x44Logo.targetsize-256.png (256x256, 高解像度アイコン)
├── Square150x150Logo.png            (150x150, スタートメニュータイル)
├── Wide310x150Logo.png              (310x150, ワイドタイル)
├── StoreLogo.png                    (50x50, ストア)
├── SplashScreen.png                 (620x300, スプラッシュ)
├── LockScreenLogo.png               (24x24, ロック画面)
└── tastile-tray.ico                 (マルチサイズ 16/32/48/256)
```

生成方法 (System.Drawing or ImageSharp):
- 各サイズの PNG を動的生成するヘルパースクリプトを作るか、手動で作成
- 最低限: 白い "T" を青背景に描画した単色アイコン
- TrayIconService で `.ico` ファイルを使うよう変更 (現在は生成アイコン "T" で代替中)

**TrayIconService.cs の変更**:
```csharp
// 現在: 動的生成アイコン "T"
// 変更後:
var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "tastile-tray.ico");
if (File.Exists(iconPath))
    _trayIcon.Icon = new System.Drawing.Icon(iconPath);
```

### 8.2 [MISSING] タイル右クリックメニュー

**問題**: タイルリストに右クリックメニューがなく、Ready タイルをクリックして Start するしかない。Complete/Defer/Delete 操作にはアクティブタイルのボタンを使う必要がある

**ファイル**: `MainWindow.xaml` (変更)

ListView の DataTemplate に MenuFlyout を追加:
```xml
<DataTemplate x:DataType="local:TileListItem">
    <Grid Padding="4,8" ColumnSpacing="12">
        <Grid.ContextFlyout>
            <MenuFlyout>
                <MenuFlyoutItem Text="Start" Click="{x:Bind StartCommand}"
                    IsEnabled="{x:Bind IsReady}" />
                <MenuFlyoutItem Text="Complete" Click="{x:Bind CompleteCommand}"
                    IsEnabled="{x:Bind IsStarted}" />
                <MenuFlyoutItem Text="Defer" Click="{x:Bind DeferCommand}" />
                <MenuFlyoutSeparator />
                <MenuFlyoutItem Text="Delete" Click="{x:Bind DeleteCommand}" />
            </MenuFlyout>
        </Grid.ContextFlyout>
        <!-- existing tile content -->
    </Grid>
</DataTemplate>
```

**TileListItem にプロパティ追加**:
```csharp
public bool IsReady => Lifecycle == "Ready";
public bool IsStarted => Lifecycle == "Started";
```

**MainViewModel にコマンド追加**:
```csharp
[RelayCommand]
private async Task DeferTile(string tileId)
{
    await _api.DeferTileAsync(tileId);
    await LoadTilesAsync();
}
```

**CoreApiClient の確認**:
- `DeferTileAsync()` は既に実装済み
- `DeleteTileAsync()` の追加が必要
  - daemon に `/commands/tile/delete` エンドポイントがあるか確認
  - なければ soft delete: `DeferTileAsync()` で代替する

### 8.3 [MISSING] ライフサイクルフィルター

**問題**: タイル一覧にフィルター機能がなく、全 lifecycle のタイルが混在して表示される

**ファイル**: `MainWindow.xaml` (変更)

ヘッダーバーとタイルリストの間にフィルターを追加:
```xml
<!-- Lifecycle Filter -->
<StackPanel Orientation="Horizontal" Spacing="4">
    <RadioButton Content="All" GroupName="Filter" IsChecked="{x:Bind ViewModel.IsFilterAll, Mode=TwoWay}" />
    <RadioButton Content="Ready" GroupName="Filter" IsChecked="{x:Bind ViewModel.IsFilterReady, Mode=TwoWay}" />
    <RadioButton Content="Started" GroupName="Filter" IsChecked="{x:Bind ViewModel.IsFilterStarted, Mode=TwoWay}" />
    <RadioButton Content="Done" GroupName="Filter" IsChecked="{x:Bind ViewModel.IsFilterDone, Mode=TwoWay}" />
</StackPanel>
```

**MainViewModel に追加**:
```csharp
[ObservableProperty]
private string _selectedFilter = "All";

// Bool properties for RadioButton binding
public bool IsFilterAll { get => SelectedFilter == "All"; set { if (value) SelectedFilter = "All"; } }
public bool IsFilterReady { get => SelectedFilter == "Ready"; set { if (value) SelectedFilter = "Ready"; } }
public bool IsFilterStarted { get => SelectedFilter == "Started"; set { if (value) SelectedFilter = "Started"; } }
public bool IsFilterDone { get => SelectedFilter == "Done"; set { if (value) SelectedFilter = "Done"; } }

partial void OnSelectedFilterChanged(string value)
{
    ApplyFilter();
}

private void ApplyFilter()
{
    var source = _allTiles; // full unfiltered list
    if (SelectedFilter != "All")
        source = source.Where(t => t.Lifecycle == SelectedFilter).ToList();

    Tiles.Clear();
    foreach (var tile in source)
        Tiles.Add(tile);
}
```

### 8.4 [ENHANCEMENT] タイルリストの詳細表示

**問題**: タイルリストでは Title と Lifecycle バッジのみ表示。next_action がある場合は2行目に表示すると操作判断しやすい

**ファイル**: `MainWindow.xaml` (変更)

DataTemplate に2行目を追加:
```xml
<StackPanel Grid.Column="0" VerticalAlignment="Center">
    <TextBlock Text="{x:Bind Title}" TextTrimming="CharacterEllipsis" />
    <TextBlock Text="{x:Bind NextActionText}" FontSize="12"
               Foreground="{ThemeResource TextFillColorSecondaryBrush}"
               Visibility="{x:Bind HasNextAction}" />
</StackPanel>
```

**TileListItem にプロパティ追加**:
```csharp
public required string? NextAction { get; init; }
public string NextActionText => !string.IsNullOrEmpty(NextAction) ? $"→ {NextAction}" : "";
public Visibility HasNextAction => !string.IsNullOrEmpty(NextAction) ? Visibility.Visible : Visibility.Collapsed;
```

### 8.5 [STUB] スタートアップ起動の実装

**問題**: `SettingsViewModel.UpdateStartupTaskAsync()` がプレースホルダーのみで、実際のレジストリ登録コードがない

**ファイル**: `ViewModels/SettingsViewModel.cs` (変更)

Unpackaged 実行時のレジストリベース実装:
```csharp
private void UpdateStartupTask(bool enable)
{
    const string keyName = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run";
    const string valueName = "Tastile";

    if (enable)
    {
        var exePath = Environment.ProcessPath;
        if (exePath != null)
            Microsoft.Win32.Registry.SetValue(keyName, valueName, $"\"{exePath}\" --minimized");
    }
    else
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
```

`--minimized` フラグ対応:
- `App.xaml.cs` の `OnLaunched()` でコマンドライン引数をチェック
- `--minimized` の場合はウィンドウを表示せずトレイのみで起動

### 検証
- [ ] Assets フォルダにすべてのアイコンが配置されている
- [ ] トレイアイコンが .ico ファイルで表示される
- [ ] タイル右クリックメニューの全項目が動作する (Start/Complete/Defer/Delete)
- [ ] フィルターで All/Ready/Started/Done の切り替えが動作する
- [ ] タイルリストに next_action が2行目に表示される
- [ ] スタートアップ起動 ON → 再起動後にトレイに常駐
- [ ] スタートアップ起動 OFF → 再起動後に自動起動しない
- [ ] `dotnet publish -c Release -r win-x64` でビルド成功
- [ ] MSIX パッケージが生成される (Assets 参照エラーなし)

---

## 最終的な依存関係フロー

```
Step 1 (MVVM) ✅
  → Step 2 (トレイ) ✅
    → Step 3 (介入) ✅
      → Step 7 (バグ修正) ✅
        → Step 4 (トースト通知) ✅
          → Step 5 (設定) ✅
            → Step 8 (残作業修正) ← NOW
```

実行順序: **Step 8 のみ残り**

### Step 8 内の推奨実行順序:
1. **8.1 アセット** → MSIX ビルドのブロッカー解消
2. **8.2 右クリックメニュー** → UX 改善の核
3. **8.3 フィルター** → 操作性向上
4. **8.4 タイル詳細表示** → 小さな改善
5. **8.5 スタートアップ起動** → 仕上げ
