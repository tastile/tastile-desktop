# Tastile Desktop — UI Design-System Pass Design Spec

- **Status**: Draft for user review
- **Date**: 2026-08-25
- **Target release**: v0.5.0 (minor) — phased delivery, Phase 1 ships in v0.4.1 patch
- **Companion ADR**: `docs/adr/0007-ui-design-system-pass.md` (to be created in Phase 1)

## Context

`tastile-desktop` is a WinUI 3 (Windows App SDK 1.7) native Windows client for the Tastile execution control system. The app has 9 windows (Main = QuickPanel / Auth / Settings / Execute / Tiles / Timeline / Integrations / CreateTile / Intervention) and a CreateTile component split that already follows component-per-file conventions (`b86e663 refactor(ui): extract CreateTile into per-component Bodies/Sections/Header files`).

The user requested a thorough UI improvement. After brainstorming (5 clarifying questions + 3 approaches + 7 design sections, all approved), the agreed direction is:

| Axis | Decision |
|---|---|
| Scope | **Full design-system pass** (9 windows + Style + Asset) |
| Brand | **Native Fluent First** (Mica backdrop, system accent, standard tokens) |
| Assets | **Segoe Fluent Icons + status glyphs only** (no new icon procurement) |
| Accessibility | **Keyboard navigation full pass** (AutomationProperties + Tab order + shortcuts + mnemonics) |
| QuickPanel priority | **Lead role refresh** (responsive sizing, ProgressRing, InfoBadge, Ctrl+1–5) |
| Approach | **Layered** (Token → Control → Window → A11y) |

This spec is the binding contract for that direction.

## Goals

1. Bring every window into compliance with AGENTS.md hard rules:
   - No `#RRGGBB` literals in XAML — ThemeResource only.
   - Use `SelectorBar` instead of hand-rolled segmented controls.
   - No `ControlTemplate` reimplementations of standard controls.
2. Establish a reusable Control library (`TitleBar`, `SettingRow`, `TileListRow`, `SelectorBarWrap`, `IconButton`, `StatusGlyphBadge`).
3. Make `MainWindow` (QuickPanel) the headline experience: Mica backdrop, responsive width, keyboard shortcuts, status glyphs.
4. Ship consistent Light/Dark/HighContrast support without bespoke `ThemeDictionary` duplication.
5. Cover all controls with `AutomationProperties.Name`, Tab order, `KeyboardAccelerator`, and `AccessKey` mnemonics.
6. Preserve all existing behavior — Intervention flow, QuickPanelPlacementResolver, prompt-toast stack, EventDrivenPoller, Auth/Token lifecycle.

## Non-goals

- Business-logic changes (InterventionEngine, EventDrivenPoller, CoreApiClient).
- Data-model changes (ApiModels, AuthSession).
- Setting-item semantic changes (ToastNotifyMinutes, InterventionMinutes meaning unchanged).
- Backend (tastile-core) API contract changes.
- Installer / update-manifest format changes.
- TrayIconService behavior changes.
- Full multi-display / DPI-aware redesign (Phase 3 covers QuickPanel responsive sizing only).
- Manual translation execution (request flow defined, not executed by this spec).

## Architecture (Layered)

```
┌─────────────────────────────────────────────────────────┐
│ Layer 5: Accessibility (AutomationProperties + Tab order│
│          + AccessKey + FocusVisual)                       │
├─────────────────────────────────────────────────────────┤
│ Layer 4: Window Views (9 XAML files, thin shells)        │
│          Auth / Settings / Execute / Tiles / Timeline /  │
│          Integrations / CreateTile / Intervention / Main │
├─────────────────────────────────────────────────────────┤
│ Layer 3: Control Library (UserControls, themed once)      │
│          TitleBar / SettingRow / TileListRow /           │
│          SelectorBarWrap / IconButton / StatusGlyphBadge │
├─────────────────────────────────────────────────────────┤
│ Layer 2: Composition Patterns (XAML helpers + templates)  │
│          SettingRowSection / TileListSection /           │
│          ToolbarSegmented / WindowCard                   │
├─────────────────────────────────────────────────────────┤
│ Layer 1: Design Tokens (App.xaml ThemeDictionary)        │
│          System token pass-through + Mica backdrop +     │
│          Emergency color only (SystemFillColorCritical)  │
├─────────────────────────────────────────────────────────┤
│ Layer 0: AppShell (BackdropMaterial / MicaController /   │
│          WindowChrome extents / Window.AppWindow)        │
└─────────────────────────────────────────────────────────┘
```

### Invariants

- All brushes defined in Layer 1 only; Layers 2+ reference them via `ThemeResource`. Hex literals are mechanically forbidden by `scripts/check-ui-tokens.ps1`.
- Mica backdrop applied to `MainWindow` / `SettingsWindow` / `CreateTileWindow` / `TimelineWindow`. `InterventionWindow` uses a custom `InterventionScrimBrush` (full-screen dialog).
- `SelectorBarWrap` is a thin `DependencyProperty` wrapper over the Win11 standard `SelectorBar` — not a `ControlTemplate` redefinition, satisfying the AGENTS.md hard rule.
- Layer 3 Controls are dependency-free (no ViewModel references); all bindings via `x:Bind` on `DependencyProperty` only.
- `InterventionWindow` and `MainWindow` are exempt from `Controls.TitleBar` (Intervention: existing fixed chrome; MainWindow: QuickPanel chrome is bespoke).

## Tokens (Layer 1)

### Removed (replaced by System tokens)

| Current (custom) | Replacement (System) | Use |
|---|---|---|
| `AppBackgroundBrush` | `SolidBackgroundFillColorBaseBrush` | Window root background |
| `AppSurface0Brush` | `LayerFillColorDefaultBrush` | Layer 0 (background) |
| `AppSurface1Brush` | `LayerFillColorAltBrush` | Card / Toolbar |
| `AppSurface2Brush` | `LayerOnAccentAcrylicFillColorDefaultBrush` | Elevated surface |
| `AppSurfaceElevatedBrush` | `SolidBackgroundFillColorSecondaryBrush` | Dialog / Popup |
| `AppForegroundBrush` | `TextFillColorPrimaryBrush` | Body text |
| `AppForegroundMutedBrush` | `TextFillColorSecondaryBrush` | Caption |
| `AppForegroundSubtleBrush` | `TextFillColorTertiaryBrush` | Hint / disabled |
| `AppBorderBrush` | `ControlStrokeColorDefaultBrush` | 1px border |
| `AppBorderStrongBrush` | `ControlStrokeColorSecondaryBrush` | Emphasis border |
| `AccentBrush` / `AppPrimaryBrush` | `AccentFillColorDefaultBrush` | Brand accent |
| `AppPrimaryForegroundBrush` | `TextOnAccentFillColorPrimaryBrush` | On-accent text |
| `AppPrimaryHoverBrush` | `AccentFillColorSecondaryBrush` | Hover |
| `PrimaryForegroundBrush` ≡ `AppForegroundBrush` | (consolidated) | — |
| `SecondaryForegroundBrush` ≡ `AppForegroundMutedBrush` | (consolidated) | — |
| `TertiaryForegroundBrush` ≡ `AppForegroundSubtleBrush` | (consolidated) | — |
| `QuickPanelBackgroundBrush` | `ControlFillColorTertiaryBrush` + alpha | QuickPanel scrim |
| `QuickPanelBorderBrush` | `ControlStrokeColorDefaultBrush` | QuickPanel border |

### Retained (custom — emergency-only)

| Token | Value (Dark / Light) | Use | Rationale |
|---|---|---|---|
| `InterventionScrimBrush` | `#A6000000` / `#66000000` | Full-screen dialog scrim | No standard WinUI scrim brush exists |
| `InterventionEmergencyBrush` | `SystemFillColorCriticalBrush` (default) | Intervention accent icon | Wraps a System token; not a literal |
| `OverlayAccentBrush` | `AccentFillColorDefaultBrush` | Prompt edge overlay | System accent |

### Added (Layout only)

| Token | Value | Use |
|---|---|---|
| `WindowTitleBarHeight` | `32` | All `Controls.TitleBar` heights |
| `WindowCardCornerRadius` | `8` (default), `4` (compact) | Card / Toolbar corners |
| `ControlSpacingTight` | `8` | IconButton / SettingRow interior |
| `ControlSpacingNormal` | `12` | Card padding |
| `ControlSpacingLoose` | `20` | Section spacing |
| `SettingRowHeight` | `48` | SettingRow standard height |

### Typography

Use system `TextBlockStyle` only (`DisplayTextBlockStyle`, `TitleTextBlockStyle`, `TitleLargeTextBlockStyle`, `BodyStrongTextBlockStyle`, `BodyTextBlockStyle`, `CaptionTextBlockStyle`). No custom typography ramp.

### Mica backdrop

`Styles/Backdrop.xaml` (new) hosts `MicaBackdrop`. `Settings.xaml.cs` applies `Window.SystemBackdrop = new MicaBackdrop()` to MainWindow / SettingsWindow / CreateTileWindow / TimelineWindow.

### Hard-rule violation cleanup

| Violation | Phase |
|---|---|
| `InterventionWindow.xaml:8` `#66000000` | Phase 1 → `InterventionScrimBrush` |
| `IntegrationsWindow.xaml:76` `#FFB00020` | Phase 1 → `SystemFillColorCriticalBrush` (via `InterventionEmergencyBrush`) |
| App.xaml brush duplication (4 pairs) | Phase 1 |
| TitleBar 32px duplication (6 windows) | Phase 2 → `Controls.TitleBar` |
| TilesWindow hand-rolled segmented (4 buttons) | Phase 4 → `SelectorBarWrap` |

## Controls (Layer 3)

All controls live in `src/TastileDesktop/Controls/`. Dependency-free (no ViewModel references). `x:Bind` consumes `DependencyProperty` only.

### `Controls/TitleBar.xaml`

- **Responsibility**: 32px title bar common to 7 windows (Auth, Settings, Execute, Tiles, Timeline, Integrations, CreateTile).
- **Props**: `string Title`, `string Subtitle` (optional), `bool ShowSubtitle`.
- **Internal**: `<Grid Height="32" Padding="16,0,12,0"><TextBlock Style={BodyStrong} Foreground={TextFillColorPrimary}/></Grid>`.
- **Note**: Window must set `ExtendsContentIntoTitleBar=true` (except `MainWindow` / `InterventionWindow`).

### `Controls/SettingRow.xaml`

- **Responsibility**: One settings row (label + caption + control slot + help text).
- **Props**: `string Label`, `string LabelCaption` (optional), `string HelpText` (optional), `object ControlContent` (`ContentPresenter`).
- **Internal**: 2-column Grid (`*` + `240`), left `StackPanel(Label, Caption)`, right `ContentPresenter` (vertical-centered).
- **Applies to**: SettingsWindow (30+ rows), IntegrationsWindow form rows.

### `Controls/TileListRow.xaml`

- **Responsibility**: One tile's display (status icon, title, duration, edit button).
- **Props**: `string StatusGlyph`, `Brush StatusForeground`, `string Title`, `string TargetDurationText`, `string ScheduledTimeDisplay`, `object TileId`.
- **Internal**: 4-column Grid (Auto + * + Auto + Auto) + Border (Surface).
- **Applies to**: TilesWindow (Ready / Running / Done — de-duplicated in Phase 4).
- **Note**: `StatusForeground` is a `DependencyProperty`; passes through `TileListItem.StatusBadgeForeground`.

### `Controls/SelectorBarWrap.xaml`

- **Responsibility**: Thin wrap over Win11 standard `SelectorBar`. Generates `SelectorBarItem` from `ItemsSource`.
- **Props**: `IEnumerable<SelectorBarItemViewModel> Items`, `object SelectedItemId`, `event SelectionChanged`.
- **Internal**: `<SelectorBar SelectionChanged="OnSelectorSelectionChanged">` hosted as template.
- **Applies to**: TilesWindow view filter (state / group / project / tag).
- **Hard-rule satisfaction**: `SelectorBarWrap` is a `DependencyProperty` wrapper, not a `ControlTemplate` redefinition. ADR 0007 documents this interpretation.

### `Controls/IconButton.xaml`

- **Responsibility**: Icon-only button (32×32 / 28×28 / 26×26 sizes, tooltip required).
- **Props**: `string Glyph`, `string GlyphFontFamily` (default Segoe Fluent Icons), `string TooltipText`, `ICommand Command`, `object CommandParameter`, `IconButtonSize Size`.
- **Internal**: `<Button Width="{Size}" Height="{Size}" Padding="0" Background="Transparent" BorderThickness="0" ToolTipService="{TooltipText}">`.
- **Applies to**: MainWindow 8 buttons, TimelineWindow +/-/←/→, TilesWindow status / edit.
- **Note**: `AutomationProperties.Name = TooltipText` is set via `x:Bind` template — Phase 5 a11y is mechanically trivial.

### `Controls/StatusGlyphBadge.xaml`

- **Responsibility**: Color-coded glyph for tile status (Running, Ready, Done, Break).
- **Props**: `TileStatus Status` (enum).
- **Internal**: `VisualStateManager` with 4 states, Foreground from `SystemFillColor*`.
- **Applies to**: MainWindow quick tile, `TileListRow` interior, `InterventionWindow` next-action hint.

### Common conventions

- **Dependency direction**: Controls ← Views only. Reverse forbidden.
- **x:Bind Mode**: `OneWay` default; explicit `TwoWay` only where needed.
- **Nullability**: string props require `FallbackValue=""`.
- **AutomationProperties**: every Control exposes a bindable `AutomationName` DependencyProperty.
- **Testability**: every Control instantiable via parameterless ctor + DependencyProperty defaults (covered by `winui-ui-testing`).

## Window-by-window changes (Layer 4)

### Phase 3: `MainWindow` (QuickPanel lead refresh)

| Aspect | Before | After |
|---|---|---|
| Window sizing | Fixed 892×88 | `MinWidth=720 MaxWidth=1080`, content-driven Auto size |
| Backdrop | `QuickPanelBackgroundBrush` (custom) | Mica + `BackdropMaterial`; frame uses `LayerOnMicaBaseAltFillColorDefaultBrush` |
| 8 buttons | Hand-rolled `<Button Background="Transparent">` | `Controls.IconButton`; `AutomationProperties.Name` auto-bound |
| Status (running tile) | `ProgressBar Height="3"` + TextBlock | `Controls.StatusGlyphBadge` + `InfoBadge` (count) + `ProgressRing IsActive=True` |
| Layout | Fixed 4 columns px | `Grid` with `*` + min/max constraints; card padding = `ControlSpacingNormal` |
| Keyboard | None | `KeyboardAccelerator Key="Number1..5"` bound to buttons; `AccessKey` on title |
| Next / Running tile | Hand-rolled `Button` | `Controls.TileListRow` |
| Top-right quick actions | `IconButton` group | `SelectorBarWrap` toggling 4 modes (Run / Plan / Sync / Settings) |
| VisualStateManager | None | `PointerOver` / `Pressed` / `Running` / `Idle` / `Break` / `Intervention` StateGroups |
| Tooltip | English fixed | `x:Uid` linked to i18n (5+3 languages) |

**Out of Phase 3 scope** (Phase 4):

- Pin / Hide behavior logic
- QuickPanel placement (top/bottom/display) — `QuickPanelPlacementResolver` recent fix preserved
- Setting for QuickPanel width (optional `QuickPanelWidth` key, default `null` = auto)

### Phase 4: Remaining 8 windows

| Window | Primary changes | Size |
|---|---|---|
| `SettingsWindow` | StackPanel 30+ rows → `Controls.SettingRow`; padding = `ControlSpacingLoose`; Language selector: keep as `ComboBox` for now (Phase 4 design-time decision — see Open Questions §4) | M (~547 → ~250 lines) |
| `CreateTileWindow` | Honor existing split; internal spacing via tokens; `CreateTileHeader` gains `InfoBadge` for validation error count | S |
| `ExecuteWindow` | Status block unified into `InfoBar` (Idle / Working / Break); Quick memo gets `TeachingTip` first-time guide | S |
| `TilesWindow` | 4-button hand-rolled segmented → `SelectorBarWrap` (hard-rule fix); 3 sections unified via `Controls.TileListRow` | M |
| `TimelineWindow` | Toolbar → `SelectorBarWrap` (Day/Week/Month/Year) + `IconButton` (+/-/←/→); month/week/year cells recolored via `LayerFillColor*Brush` | L |
| `IntegrationsWindow` | Connection state → `InfoBar` (Success/Warning/Error); `#FFB00020` removed; custom brush count drops to 1 (`InterventionEmergencyBrush`) | S |
| `AuthWindow` | Central card `MinHeight 320 → 360`; description promoted to `BodyStrong`; background Mica | S |
| `InterventionWindow` | Scrim unified to `InterventionScrimBrush`; emergency icon → `SystemFillColorCriticalBrush`; `ListView` → `ItemsRepeater` + `TileListRow` | M |

### Phase 5: Accessibility (mechanical)

| Item | Application |
|---|---|
| `AutomationProperties.Name` | Via `Controls.TitleBar.Subtitle` and bindable `AutomationName` on every Control |
| Tab order | `TabIndex` 0..N in semantic order (top-left → bottom-right) |
| Keyboard shortcuts | `Ctrl+1..5` (MainWindow modes), `Ctrl+S` (Settings save), `Ctrl+,` (Settings open), `Esc` (close dialog), `?` (help TeachingTip) |
| Mnemonics (AccessKey) | `_Title`, `_Settings`, etc. added to resx (8 languages) |
| `FocusVisualPrimaryBrush` | Inherits `SystemControlFocusVisualPrimaryBrush`; override only where accent is needed |
| `HighContrastAdjustment="Auto"` | Applied to major controls |

**MainWindow scope rule**: `KeyboardAccelerator.ScopeOwner = MainWindow` — only active when MainWindow focused. Other windows use `Ctrl+Shift+1` etc.

**Intervention escape**: Esc → fires `EndBreak` button click (existing behavior preserved).

## Data flow & error handling

### Data flow

No changes from existing pattern:

- `x:Bind ViewModel.Prop, Mode=OneWay` (unchanged)
- `x:Bind ViewModel.Prop, Mode=TwoWay` (Settings only)
- `x:Bind ViewModel.Command` (RelayCommand)
- `CommunityToolkit.Mvvm` `ObservableProperty` / `RelayCommand`
- ViewModel ↔ Service layer untouched

New addition: `DependencyProperty` on Controls, bound from Views via `x:Bind`.

### Error handling

| Surface | Before | After |
|---|---|---|
| `IntegrationsWindow` | `<TextBlock Foreground="#FFB00020" Text="…"/>` | `<InfoBar Severity="Error" IsOpen="{x:Bind ViewModel.HasError, Mode=OneWay}"/>` |
| `CreateTileWindow` | `sections/PanelErrorBanner` | Preserved; internal refactor to consume `InfoBarSeverity` |
| `InterventionWindow` | `ListView` direct error | Preserved (Phase 5 a11y only) |
| `SettingsWindow` save | `OnSaveClick` status text | Status text + `Snackbar` / `TeachingTip` success notification (new) |
| `MainWindow` running tile | TextBlock direct binding | Preserved; `Controls.StatusGlyphBadge` handles state internally |

**InfoBar severity → token mapping**:

| Severity | Token |
|---|---|
| Error | `SystemFillColorCriticalBrush` |
| Warning | `SystemFillColorCautionBrush` |
| Success | `SystemFillColorSuccessBrush` |
| Informational | `AccentFillColorDefaultBrush` |

### Settings backward compatibility

`%APPDATA%\Tastile\settings.json` schema unchanged. New optional keys (default behavior if missing, via `??=` pattern):

| New key | Purpose | Default |
|---|---|---|
| `QuickPanelWidth` | 720–1080 range | `null` (auto) |
| `QuickPanelKeyboardShortcuts` | `Ctrl+1..5` enable | `true` |
| `PromptEdgeOverlayAlpha` | Future overlay alpha | `0.6` |

### i18n impact (5+3 languages)

Approx 40 new strings × 8 languages = **320 resx keys**.

| Source | Keys |
|---|---|
| MainWindow shortcut labels (`x:Uid`) | ~10 |
| AccessKey mnemonics (Phase 5) | ~25 |
| Snackbar / TeachingTip messages | ~5 |

Workflow:

1. Phase 1: list all new `x:Uid` keys (English master).
2. `scripts/skeleton-translations.ps1` (existing) bulk-stubs into 8 resx files.
3. Translator review before Phase 4 commit.
4. `scripts/check-i18n.ps1` (existing) gates CI on full coverage.

## Testing

### Phase gates

| Phase | Automated | Manual |
|---|---|---|
| 1 Tokens | `scripts/check.ps1` green; `grep -rE '#[0-9A-Fa-f]{6,8}'` returns 0 hex literals in Views/MainWindow/App.xaml/Styles | Light / Dark / HighContrast eyeball on all windows |
| 2 Controls | `winui-ui-testing` instantiation + default DependencyProperty assertions; `grep -r 'Height="32"' src/TastileDesktop/Views` returns 0 TitleBar duplicates | One-off test window hosting every Control; Visual Inspector |
| 3 QuickPanel | xUnit layout test (`MinWidth=720 MaxWidth=1080`, AutomationProperties present); `Ctrl+1..5` accelerator binding test | QuickPanel at 720 / 892 / 1080 widths; 4 states (Running / Idle / Break / Intervention) |
| 4 8 windows | `scripts/check.ps1` per-window; `grep SelectorBar` confirms TilesWindow hand-rolled segmented removed | Each window in Light / Dark / HighContrast; CreateTile 4 bodies with validation errors |
| 5 A11y | `grep -rE 'AutomationProperties\.Name='` ≥ 90% coverage; Tab-order unit tests | Accessibility Insights scan (MainWindow / SettingsWindow); Narrator reading check |

### Canonical local validation: `scripts/check.ps1`

Existing entries (format / vuln / tests / dual desktop build / TimelineWindow connector) preserved. New additions:

- `scripts/check-ui-tokens.ps1` (new): hex grep + TitleBar duplication grep + SelectorBar adoption grep + AutomationProperties coverage.
- Called from the tail of `scripts/check.ps1`.

### UI testing (new project: `tests/TastileDesktop.UiTests/`)

CommunityToolkit.WinUI + Appium WinAppDriver. Initial 5 cases:

1. `MainWindow_Loads`
2. `MainWindow_RespondsToShortcut`
3. `TilesWindow_FilterSelection`
4. `CreateTileWindow_ValidationFlow`
5. `SettingsWindow_SavePersists`

### Visual regression

PerceptualDiff at 5% threshold, screenshots at:

| Resolution | Theme |
|---|---|
| 1920×1080 | Light |
| 1920×1080 | Dark |
| 2560×1440 | Light |
| 1366×768 | Dark |

Stored at `tests/screenshots/<phase>/<window>-<theme>-<res>.png`.

### 5-language gate

`scripts/check-i18n.ps1` (existing, content unchanged; hook verified in Phase 1):

- en / ja / zh-CN / ko / es (5 required)
- de / fr / pt-BR (3 tier-2)

Untranslated keys fail CI.

## Risks & mitigations

| ID | Risk | Phase | Mitigation |
|---|---|---|---|
| R1 | Mica backdrop paint cost on HiDPI multi-monitor | 3 | Profile; fallback to `SolidBackgroundFillColorBaseBrush` if frame > 16ms |
| R2 | `SelectorBarItem` truncates long i18n labels | 4 | `TextTrimming="CharacterEllipsis"`, `MinWidth` sized to longest label |
| R3 | Responsive QuickPanel breaks `QuickPanelPlacementResolver` edge cases | 3 | Add unit tests for Resolver pre-Phase 3; separate placement calc from window size |
| R4 | 320 i18n keys × 8 languages manual translation delay | 4-5 | Bulk-stub via `scripts/skeleton-translations.ps1`; translator review 1 sprint ahead; ADR documents request flow |
| R5 | 5-language gate fails on mixed existing/new translations | 1, 4, 5 | Inventory all new `x:Uid` at Phase 1 kickoff; complete translations before Phase 4 |
| R6 | WinAppDriver Win11 24H2 compatibility | 2-5 | Verify in Phase 2; fallback to xUnit + manual WinAppDriver calls (Plan B) |
| R7 | `ExtendsContentIntoTitleBar=true` conflicts with MainWindow QuickPanel chrome | 3 | MainWindow exempt (Layer 0 exception); `Controls.TitleBar` only adjusts interior |
| R8 | `Ctrl+1..5` scope collision across windows | 3, 5 | `KeyboardAccelerator.ScopeOwner = MainWindow`; other windows use `Ctrl+Shift+1` |
| R9 | New Controls break Intervention "X 無効" enforcement | 4 | Intervention exempt from `Controls.TitleBar`; preserves existing fixed chrome |
| R10 | Microsoft Learn MCP only reachable via `opencode.json` (opencode CLI), not Claude Code | All | Use `microsoft-docs` plugin (AGENTS.md pinned): `microsoft_docs_search` / `microsoft_code_sample_search` / `microsoft_docs_fetch` |
| R11 | Token-name collision with `AppSettings` enum/constant | 1 | Pre-Phase-1 grep audit for collisions |
| R12 | `SelectorBarWrap` violates "no hand-rolled segmented" hard rule | 2 | Wrap is `DependencyProperty` over Win11 standard `SelectorBar`, not `ControlTemplate` redefinition. ADR 0007 documents the interpretation |

## Companion ADR

`docs/adr/0007-ui-design-system-pass.md` (Phase 1) records:

1. Native Fluent First — AGENTS.md hard-rule alignment + Win11 consistency
2. Hard-rule re-interpretation: "hand-rolled segmented" = `ControlTemplate` redefinition (not `DependencyProperty` wrap) — R12 mitigation
3. Layered approach — QuickPanel lead refresh + 8-window coherence (vs. Window-by-window inconsistent mid-stream)
4. i18n translation flow — R4 mitigation
5. Responsive QuickPanel vs. `QuickPanelPlacementResolver` separation of concerns — R3 mitigation

Template: `docs/adr/template.md` (existing). Aligns with `5f08013 docs(desktop): add ADR foundation for init/LSP-MCP/gates/build/CI changes`.

## Release plan

| Phase | Target | Gate |
|---|---|---|
| 1 Tokens | v0.4.1 (patch) | `scripts/check-ui-tokens.ps1`; 0 hex literals; 0 brush dupes |
| 2 Controls | v0.4.2 (patch) | `winui-ui-testing` Controls cases; 0 TitleBar duplicates |
| 3 QuickPanel | v0.5.0 (minor) | Responsive + Mica + Ctrl+1..5 + screenshot review |
| 4 Remaining windows | v0.5.0 (minor, continued) | SelectorBar adoption + TileListRow unification + screenshot review |
| 5 A11y | v0.5.0 (minor, finalized) | AutomationProperties ≥ 90% + Tab order tests + Accessibility Insights clean |

Release notes (English / Japanese) updated per phase. Translation gate respected at each PR.

## Open questions deferred to Phase 1 kickoff

1. Confirm `QuickPanelWidth` setting key is opt-in vs. forced (recommend opt-in).
2. Decide if Snackbar / TeachingTip success notification is added to Settings save (recommend yes, ~5 keys).
3. Confirm whether `SelectorBarWrap` is the right name vs. `SelectorBarSegmented` (recommend `SelectorBarWrap`).
4. Decide visual regression threshold (recommend 5%, PerceptualDiff).
