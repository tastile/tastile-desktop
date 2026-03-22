# Tastile Desktop PowerToys Design System Migration Plan

## Overview
完全にPowerToysのデザインシステムと構造を採用し、WindowsネイティブなUIエクスペリエンスを実現する。

## Architecture Principles (from tastile_docs_bundle)
- **Thin UI Layer**: UIは表示と入力の窓口のみ
- **Core Processing**: すべての処理はRust Core + SQLiteで実行
- **Windows Native**: WinUI 3で自然なWindows体験を提供
- **Local First**: データはローカルに保持

## Directory Structure (PowerToys方式)

```
tastile-desktop/src/TastileDesktop/
├── App.xaml                          # 統合エントリポイント
├── MainWindow.xaml                   # Quick Panel (メインUI)
├── SettingsXAML/                     # PowerToys方式の設定UI
│   ├── Themes/
│   │   └── Colors.xaml              # Dark/Light/HighContrast定義
│   ├── Styles/
│   │   ├── TextBlock.xaml           # タイポグラフィ
│   │   ├── Button.xaml              # ボタンスタイル
│   │   └── Card.xaml                # カード/サーフェススタイル
│   └── Controls/                    # カスタムコントロール
├── Views/                           # ウィンドウ/ページ
│   ├── AuthWindow.xaml
│   ├── SettingsWindow.xaml
│   ├── TilesWindow.xaml
│   └── TimelineWindow.xaml
├── ViewModels/                      # MVVM ViewModels
├── Services/                        # ビジネスロジック
│   ├── CoreApiClient.cs            # Coreとの通信
│   ├── ThemeManager.cs             # テーマ管理
│   └── WindowManager.cs            # ウィンドウ管理
└── Models/                          # データモデル
```

## Design Tokens (PowerToys方式)

### Colors.xaml Structure
```xml
<ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Dark">
        <SolidColorBrush x:Key="PrimaryBackgroundBrush" Color="#FF202020"/>
        <SolidColorBrush x:Key="SecondaryBackgroundBrush" Color="#FF242424"/>
        <SolidColorBrush x:Key="TertiaryBackgroundBrush" Color="#FF2D2D2D"/>
        <SolidColorBrush x:Key="PrimaryForegroundBrush" Color="#FFFFFFFF"/>
        <SolidColorBrush x:Key="SecondaryForegroundBrush" Color="#FF9A9A9A"/>
        <SolidColorBrush x:Key="PrimaryBorderBrush" Color="#FF3A3A3A"/>
        <SolidColorBrush x:Key="AccentBrush" Color="{ThemeResource SystemAccentColor}"/>
    </ResourceDictionary>
    <ResourceDictionary x:Key="Light">...</ResourceDictionary>
    <ResourceDictionary x:Key="HighContrast">...</ResourceDictionary>
</ResourceDictionary.ThemeDictionaries>
```

### Layout Tokens
- `QuickPanelCornerRadius`: 8px (Snap Assistと同じ)
- `ControlCornerRadius`: 4px (標準)
- `SettingsCardSpacing`: 4
- `PageMaxWidth`: 1000

## Migration Steps

### Phase 1: 基盤構築
1. SettingsXAML/Themes/Colors.xaml 作成
2. SettingsXAML/Styles/ 作成
3. App.xaml でMergedDictionaries設定

### Phase 2: MainWindow更新
1. BorderのBackground/Brushを新トークンに変更
2. DWM角丸無効化 (FloatingWindowHelper)
3. ボタンスタイル適用

### Phase 3: 他ウィンドウ更新
1. 各Window.xamlを新トークンに移行
2. スタイル参照を更新

### Phase 4: クリーンアップ
1. 旧ブラシ定義の削除
2. 重複スタイルの統合
3. テスト実行

## Reference: PowerToys Code Structure

### Key Files in PowerToys Reference
- `src/settings-ui/Settings.UI/SettingsXAML/Themes/Colors.xaml`
- `src/modules/fancyzones/editor/FancyZonesEditor/Themes/Dark.xaml`
- `src/modules/Workspaces/WorkspacesEditor/Themes/Dark.xaml`
- `src/modules/cmdpal/Microsoft.CmdPal.UI/Styles/`

## Implementation Notes

### DWM Corner Radius
```csharp
// FloatingWindowHelper.cs
var cornerPreference = DwmWindowCornerPreferenceDoNotRound;
DwmSetWindowAttribute(hwnd, DwmWindowCornerPreferenceAttribute, 
    ref cornerPreference, sizeof(uint));
```

### Theme Switching
- System設定に連動
- Dark/Light/HighContrast対応
- アクセントカラーはSystemAccentColorを使用

## Acceptance Criteria
- [ ] MainWindowがPowerToys方式のトークンを使用
- [ ] 2重角丸が解消されている
- [ ] ボーダーが適切に表示される
- [ ] Dark/Light/HighContrastテーマが動作
- [ ] アクセントカラーがシステム設定に追従
