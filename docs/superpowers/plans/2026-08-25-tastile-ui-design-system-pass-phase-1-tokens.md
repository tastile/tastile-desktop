# UI Design-System Pass — Phase 1 (Tokens) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace 17 hand-rolled brushes with Win11 System tokens, add the override-accent layer that lets `ThemeManager` keep dynamic Windows-accent support, fix the two remaining `#RRGGBB` literals in Views, and ship Mica backdrop to the four primary windows. Ship as **v0.4.1** patch.

**Architecture:** Layered (Layer 1 = tokens, Layer 0 = Mica). Static brushes point at Win11 system resources; dynamic accent overrides write to a single named pair (`OverrideAccentFillBrush`, `OverrideAccentFillSecondaryBrush`) that `ThemeManager` mutates at runtime. Two literal hex strings (InterventionWindow scrim, IntegrationsWindow error text) are replaced by named resources. Mica is applied via `Window.SystemBackdrop` from `Settings.xaml.cs`-style code-behind in each of the four target windows.

**Tech Stack:** C# / WinUI 3 (Windows App SDK 1.7), `net9.0-windows10.0.26100.0`, CommunityToolkit.Mvvm, xUnit. PowerShell 7 for `scripts/*`. New test project additions only (no new project).

**Spec:** `docs/superpowers/specs/2026-08-25-tastile-ui-design-system-pass-design.md` (sections Tokens, Architecture, Data Flow & Error Handling, Testing, Risks R3/R10/R11).

## Global Constraints

- WinUI 3 / Windows App SDK 1.7. Target framework `net9.0-windows10.0.26100.0`, SDK pinned via `global.json` (`rollForward: latestFeature`).
- Colors must use `ThemeResource` references. No `#RRGGBB` literals in XAML (AGENTS.md hard rule).
- No new `NavigationView` for full-screen layouts (AGENTS.md hard rule).
- No `ControlTemplate` reimplementation of standard controls (AGENTS.md hard rule).
- `Microsoft Learn MCP` is **not** reachable from Claude Code in this environment. Use the `microsoft-docs` plugin (`microsoft_docs_search` / `microsoft_code_sample_search` / `microsoft_docs_fetch`) for Microsoft API lookups. (Risk R10.)
- `dotnet format --verify-no-changes` must remain green (`TreatWarningsAsErrors` enforced).
- i18n: no new `x:Uid` keys are introduced by Phase 1. Existing 5-language gate (`scripts/check-i18n.ps1`) is unaffected.
- All commits end with trailer `Co-Authored-By: Claude <noreply@anthropic.com>`.
- Branch convention: `feat/ui-tokens-phase-1`. Rebase-friendly, no merge commits.

---

## File Structure

| File | Phase | Action | Responsibility |
|---|---|---|---|
| `docs/adr/0007-ui-design-system-pass.md` | 0 | Create | ADR recording Native Fluent First + override-accent decision |
| `docs/adr/0007-ui-design-system-pass.md` | 0 | Create | Re-interpretation of "hand-rolled segmented" hard rule (R12) |
| `src/TastileDesktop/Services/FloatingWindowHelper.cs` | 1 | Modify (Task 2a) | Rename 8 brush-key lookups to System tokens |
| `src/TastileDesktop/Services/PromptAttentionOverlayWindow.cs` | 1 | Modify (Task 2a) | Rename 1 brush-key lookup |
| `src/TastileDesktop/Services/PromptToastWindow.cs` | 1 | Modify (Task 2a) | Rename 5 brush-key lookups |
| `src/TastileDesktop/Services/QuickPanelIconStyleResolver.cs` | 1 | Modify (Task 2a) | Rename 2 brush-key lookups |
| `src/TastileDesktop/ViewModels/MainViewModel.cs` | 1 | Modify (Task 2a) | Rename 5 brush-key lookups |
| `src/TastileDesktop/Views/AuthWindow.xaml.cs` | 1 | Modify (Task 2a, R5) | Rename 1 brush-key lookup (line 70, AppPrimaryBrush) |
| `src/TastileDesktop/MainWindow.xaml.cs` | 1 | Modify (Task 2a, R5) | Rename 1 brush-key lookup (line 86, PrimaryForegroundBrush) |
| `src/TastileDesktop/App.xaml` | 1 | Modify | Drop 14 custom brushes; keep 3 override brushes; add 6 layout tokens |
| `src/TastileDesktop/Services/ThemeManager.cs` | 1 | Modify | Write to `OverrideAccentFill*Brush` instead of `AccentBrush`/`AppPrimaryBrush` |
| `src/TastileDesktop/Views/InterventionWindow.xaml` | 1 | Modify | Replace `Background="#66000000"` with `InterventionScrimBrush` |
| `src/TastileDesktop/Views/IntegrationsWindow.xaml` | 1 | Modify | Replace `Foreground="#FFB00020"` with `SystemFillColorCriticalBrush` |
| `src/TastileDesktop/Styles/Backdrop.xaml` | 1 | Create | ResourceDictionary exposing `DefaultMicaBackdrop` style |
| `src/TastileDesktop/MainWindow.xaml.cs` | 1 | Modify | Set `SystemBackdrop = new MicaBackdrop()` |
| `src/TastileDesktop/Views/SettingsWindow.xaml.cs` | 1 | Modify | Same Mica wiring |
| `src/TastileDesktop/Views/CreateTileWindow.xaml.cs` | 1 | Modify | Same Mica wiring |
| `src/TastileDesktop/Views/TimelineWindow.xaml.cs` | 1 | Modify | Same Mica wiring |
| `scripts/check-ui-tokens.ps1` | 1 | Create | Hex literal grep + brush duplication grep |
| `scripts/check.ps1` | 1 | Modify | Invoke `check-ui-tokens.ps1` from the tail |
| `tests/TastileDesktop.Tests/UiTokensTests.cs` | 1 | Create | xUnit assertions that App.xaml defines override brushes + layout tokens |
| `tests/TastileDesktop.Tests/ThemeManagerOverrideTests.cs` | 1 | Create | xUnit assertions for ThemeManager override target names |

---

## Task 1: ADR 0007 — Record Native Fluent First

**Files:**
- Create: `docs/adr/0007-ui-design-system-pass.md`
- Read: `docs/adr/template.md` (existing)

**Step 1: Read the ADR template**

Run: `cat docs/adr/template.md`
Expected: A markdown template with sections `## Status`, `## Context`, `## Decision`, `## Consequences`.

**Step 2: Write the ADR**

Create `docs/adr/0007-ui-design-system-pass.md`:

```markdown
# ADR 0007: UI Design-System Pass (Native Fluent First)

Status: Accepted (2026-08-25)
Deciders: Desktop maintainers
Spec: docs/superpowers/specs/2026-08-25-tastile-ui-design-system-pass-design.md

## Context

tastile-desktop has accumulated 27 custom brushes in `App.xaml` ThemeDictionaries
and 2 `#RRGGBB` literals in view XAML. The system theme already relies on
custom colors and a hand-rolled `ThemeManager` that overwrites `AccentBrush` /
`AppPrimaryBrush` / `AppPrimaryHoverBrush` at runtime. AGENTS.md forbids
literal hex colors and hand-rolled segmented controls.

## Decision

1. **Native Fluent First.** Static brushes map to Win11 system tokens
   (`TextFillColorPrimaryBrush`, `LayerFillColorDefaultBrush`,
   `AccentFillColorDefaultBrush`, etc.). Dynamic accent uses a single
   named override layer.
2. **Override accent layer.** Keep two custom brushes —
   `OverrideAccentFillBrush` and `OverrideAccentFillSecondaryBrush` — in
   both Dark and Light theme dictionaries. Default their `Color` to the
   current System accent at startup. `ThemeManager` writes only to these
   two keys.
3. **Hard-rule re-interpretation.** "Hand-rolled segmented control" in
   AGENTS.md means `ControlTemplate` redefinition. A
   `DependencyProperty` wrap over the standard Win11 `SelectorBar` is
   acceptable. (Resolves Risk R12 in the spec.)
4. **Layered delivery.** Phases 1-5 ship as separate releases
   (v0.4.1 / v0.4.2 / v0.5.0). Phase 1 covers token replacement, override
   layer, hex fixes, and Mica backdrop for the four primary windows.
5. **i18n flow.** New `x:Uid` keys are inventoried at the start of each
   phase. `scripts/skeleton-translations.ps1` bulk-stubs English, then a
   translator review covers 8 languages before merge. Phase 1 adds zero
   new keys.

## Consequences

- Removing the 14 obsolete brushes shrinks `App.xaml` substantially and
  removes the duplication that risked Light/Dark drift.
- `ThemeManager` semantics are unchanged from the user's perspective:
  the runtime accent override still works.
- Future `SelectorBarWrap` (Phase 2) is sanctioned by this ADR.
- The override-accent contract (key names) is locked. Adding more
  override keys requires a new ADR.
```

**Step 3: Commit**

```bash
git add docs/adr/0007-ui-design-system-pass.md
git commit -m "docs(desktop): ADR 0007 UI design-system pass (Native Fluent First)

Records the override-accent layer decision, the hand-rolled-segmented
hard-rule re-interpretation, and the Phase 0-5 release plan.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 2: Token-name collision inventory

`AppSettings.cs` may define enum/constant members whose names overlap with the
new token keys. The plan removes 14 brushes; we must confirm no collision before
editing.

**Files:**
- Read: `src/TastileDesktop/Services/AppSettings.cs`

**Step 1: Grep AppSettings for any of the brush keys being removed**

Run:
```bash
grep -nE 'AppBackgroundBrush|AppSurface0Brush|AppSurface1Brush|AppSurface2Brush|AppSurfaceElevatedBrush|AppForegroundBrush|AppForegroundMutedBrush|AppForegroundSubtleBrush|AppBorderBrush|AppBorderStrongBrush|AppPrimaryBrush|AppPrimaryForegroundBrush|AppPrimaryHoverBrush|QuickPanelBackgroundBrush|QuickPanelBorderBrush|PrimaryForegroundBrush|SecondaryForegroundBrush|TertiaryForegroundBrush|AccentBrush' src/TastileDesktop/Services/AppSettings.cs
```

Expected: **no matches** (Risk R11 mitigation — AppSettings.cs uses different identifiers).

**Step 2: Grep Services/ and ViewModels/ for the same keys**

Run:
```bash
grep -rnE 'AppBackgroundBrush|AppSurface0Brush|AppSurface1Brush|AppSurface2Brush|AppSurfaceElevatedBrush|AppForegroundBrush|AppForegroundMutedBrush|AppForegroundSubtleBrush|AppBorderBrush|AppBorderStrongBrush|AppPrimaryBrush|AppPrimaryForegroundBrush|AppPrimaryHoverBrush|QuickPanelBackgroundBrush|QuickPanelBorderBrush|PrimaryForegroundBrush|SecondaryForegroundBrush|TertiaryForegroundBrush|AccentBrush' src/TastileDesktop/Services/ src/TastileDesktop/ViewModels/
```

Expected: **no matches** for these tokens in non-XAML files. (Tokens are XAML-only.)

If matches are found, **stop** and surface them in the PR description; do not
proceed with Task 3 until collisions are resolved.

**Step 3: No commit** — this is a verification task.

---

## Task 2a: Rename 21 C# brush-key lookups to System tokens

> **Origin:** Task 2 surfaced 21 hard-runtime collisions: 21 string-literal
> `Application.Current.Resources["…"]` / `ThemeManager.GetColor("…")` /
> `TryGetResourceBrush(app, "…")` calls across 7 files that reference brush
> keys Task 4 is about to remove. Without this task, Task 4 leaves a window
> where the XAML resources are gone but the C# consumers still request them
> by old name → `KeyNotFoundException` / null brushes / silent fallback on
> first run. This task lands the consumer rename in the same logical commit
> scope as Task 4 (one reviewer, one logical unit) but in a separate commit so
> `git log --reverse` shows the rename before the removal.

**Files (read each before editing; preserve indentation and string literal quotes):**

- Modify: `src/TastileDesktop/Services/FloatingWindowHelper.cs` (8 lookups, lines 320, 322, 325, 326, 327, 328, 329, 330)
- Modify: `src/TastileDesktop/Services/PromptAttentionOverlayWindow.cs` (1 lookup, line 53)
- Modify: `src/TastileDesktop/Services/PromptToastWindow.cs` (5 lookups, lines 46, 55, 62, 146, 147)
- Modify: `src/TastileDesktop/Services/QuickPanelIconStyleResolver.cs` (2 lookups, lines 17, 18)
- Modify: `src/TastileDesktop/ViewModels/MainViewModel.cs` (5 lookups, lines 1342, 1343, 1344, 1345, 1346)

**Step 1: Apply the rename map**

Use the mapping below (derived from the Spec "Tokens (Layer 1) — Removed" table, with the override-layer adjustment for `AppPrimaryBrush` per ADR 0007). Edit each file in place; preserve everything except the string literal inside the bracket/argument.

| Old token key | New token key | Rationale |
|---|---|---|
| `"AppForegroundBrush"` | `"TextFillColorPrimaryBrush"` | Body text |
| `"AppForegroundMutedBrush"` | `"TextFillColorSecondaryBrush"` | Caption |
| `"AppSurface1Brush"` | `"LayerFillColorAltBrush"` | Card / Toolbar |
| `"AppSurface2Brush"` | `"LayerOnAccentAcrylicFillColorDefaultBrush"` | Elevated surface |
| `"AppPrimaryBrush"` | `"OverrideAccentFillBrush"` | Runtime accent (override layer), NOT the static `AccentFillColorDefaultBrush` |
| `"PrimaryForegroundBrush"` | `"TextFillColorPrimaryBrush"` | Consolidated (Spec row 103) |
| `"SecondaryForegroundBrush"` | `"TextFillColorSecondaryBrush"` | Consolidated (Spec row 104) |
| `"AppBorderBrush"` | `"ControlStrokeColorDefaultBrush"` | 1px border |
| `"AppSurfaceElevatedBrush"` | `"SolidBackgroundFillColorSecondaryBrush"` | Dialog / Popup |
| `"TertiaryForegroundBrush"` | `"TextFillColorTertiaryBrush"` | Consolidated (Spec row 105) |

**Step 2: Verify no remaining references**

Run:
```bash
grep -rnE '"AppBackgroundBrush"|"AppSurface0Brush"|"AppSurface1Brush"|"AppSurface2Brush"|"AppSurfaceElevatedBrush"|"AppForegroundBrush"|"AppForegroundMutedBrush"|"AppForegroundSubtleBrush"|"AppBorderBrush"|"AppBorderStrongBrush"|"AppPrimaryBrush"|"AppPrimaryForegroundBrush"|"AppPrimaryHoverBrush"|"QuickPanelBackgroundBrush"|"QuickPanelBorderBrush"|"PrimaryForegroundBrush"|"SecondaryForegroundBrush"|"TertiaryForegroundBrush"|"AccentBrush"' src/TastileDesktop/Services/ src/TastileDesktop/ViewModels/ src/TastileDesktop/Controls/ src/TastileDesktop/Views/ src/TastileDesktop/MainWindow.xaml.cs 2>/dev/null
```

Expected: **no matches** in any C# file. (Note: the search above adds `Controls/`, `Views/`, and `MainWindow.xaml.cs` because the original Task 2 only covered Services/ and ViewModels/ — be thorough.)

Note: the substring matches in `ViewModels/SettingsViewModel.cs` (`AccentBrush`, `WindowsAccentBrush`, `UiAccentBrush` — CLR properties), `ViewModels/MainViewModel.cs:54,1373,1496,1706` (`SecondaryForegroundBrush` CLR property assignments), and any other C# identifier that *contains* the brush-name substring are **not** collisions — they are CLR property/field names, not XAML resource lookups. Leave them untouched. The grep pattern uses double-quoted literal strings (`"AppForegroundBrush"` etc.) to exclude those.

**Step 3: Run format + build**

```bash
dotnet format src/TastileDesktop/TastileDesktop.csproj --verify-no-changes --no-restore --verbosity minimal
dotnet build src/TastileDesktop/TastileDesktop.csproj
```

Expected: clean build. The renamed string literals are still XAML resource keys, but `App.xaml` still defines both the old and new keys at this stage, so the consumers' runtime lookups continue to resolve. Task 4 strips the old keys.

**Step 4: Commit**

```bash
git add src/TastileDesktop/Services/FloatingWindowHelper.cs \
        src/TastileDesktop/Services/PromptAttentionOverlayWindow.cs \
        src/TastileDesktop/Services/PromptToastWindow.cs \
        src/TastileDesktop/Services/QuickPanelIconStyleResolver.cs \
        src/TastileDesktop/ViewModels/MainViewModel.cs
git commit -m "refactor(desktop): rename 21 C# brush-key lookups to System tokens

Surfaces Task 2 collision inventory. 21 hard-runtime string-lookup
call sites across 5 files (FloatingWindowHelper, PromptAttentionOverlay,
PromptToast, QuickPanelIconStyleResolver, MainViewModel) referenced
brush keys Task 4 will remove. Renamed to the System token keys per
the Spec 'Tokens (Layer 1) — Removed' table, with AppPrimaryBrush
mapped to OverrideAccentFillBrush (runtime override) per ADR 0007.

App.xaml is unchanged; both old and new keys still resolve until
Task 4 strips the old set.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 3: xUnit test that App.xaml defines the override-accent contract

TDD step: lock the override-accent contract in a test before editing `App.xaml`.

**Files:**
- Create: `tests/TastileDesktop.Tests/UiTokensTests.cs`

**Step 1: Write the failing test**

Create `tests/TastileDesktop.Tests/UiTokensTests.cs`:

```csharp
using System.Xml.Linq;
using Xunit;

namespace TastileDesktop.Tests;

public class UiTokensTests
{
    // WPF / WinUI XAML uses the x: prefix for resource keys, which is bound to
    // the http://schemas.microsoft.com/winfx/2006/xaml namespace. A namespace-
    // aware lookup is required — e.Attribute("Key") returns null for every key.
    private static readonly XName KeyAttr = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");

    private static readonly string AppXamlPath =
        Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TastileDesktop", "App.xaml");

    [Fact]
    public void AppXaml_DefinesOverrideAccentFillBrush_BothThemes()
    {
        var doc = XDocument.Load(AppXamlPath);
        var keys = doc.Descendants()
            .Where(e => e.Name.LocalName == "SolidColorBrush")
            .Select(e => (string?)e.Attribute(KeyAttr) ?? "")
            .ToHashSet();

        Assert.Contains("OverrideAccentFillBrush", keys);
        Assert.Contains("OverrideAccentFillSecondaryBrush", keys);
    }

    [Fact]
    public void AppXaml_RemovedBrushes_AreNotPresent()
    {
        var doc = XDocument.Load(AppXamlPath);
        var keys = doc.Descendants()
            .Where(e => e.Name.LocalName == "SolidColorBrush")
            .Select(e => (string?)e.Attribute(KeyAttr) ?? "")
            .ToHashSet();

        var removed = new[]
        {
            "AppBackgroundBrush", "AppSurface0Brush", "AppSurface1Brush",
            "AppSurface2Brush", "AppSurfaceElevatedBrush", "AppForegroundBrush",
            "AppForegroundMutedBrush", "AppForegroundSubtleBrush",
            "AppBorderBrush", "AppBorderStrongBrush", "AppPrimaryBrush",
            "AppPrimaryForegroundBrush", "AppPrimaryHoverBrush",
            "QuickPanelBackgroundBrush", "QuickPanelBorderBrush",
            "PrimaryForegroundBrush", "SecondaryForegroundBrush",
            "TertiaryForegroundBrush", "AccentBrush",
        };

        foreach (var key in removed)
            Assert.DoesNotContain(key, keys);
    }

    [Fact]
    public void AppXaml_DefinesLayoutTokens()
    {
        var doc = XDocument.Load(AppXamlPath);
        var tokens = doc.Descendants()
            .Select(e => (string?)e.Attribute(KeyAttr) ?? "")
            .Where(k => !string.IsNullOrEmpty(k))
            .ToHashSet();

        Assert.Contains("WindowTitleBarHeight", tokens);
        Assert.Contains("WindowCardCornerRadius", tokens);
        Assert.Contains("ControlSpacingTight", tokens);
        Assert.Contains("ControlSpacingNormal", tokens);
        Assert.Contains("ControlSpacingLoose", tokens);
        Assert.Contains("SettingRowHeight", tokens);
        Assert.Contains("InterventionScrimBrush", tokens);
        Assert.Contains("InterventionEmergencyBrush", tokens);
    }
}
```

**Step 2: Run test to verify it fails**

Run:
```bash
dotnet test tests/TastileDesktop.Tests/TastileDesktop.Tests.csproj --filter "FullyQualifiedName~UiTokensTests" --nologo
```

Expected: 3 tests fail. (`AppXaml_DefinesOverrideAccentFillBrush_BothThemes` fails because override brushes are missing; `AppXaml_RemovedBrushes_AreNotPresent` fails because the old brushes are still present; `AppXaml_DefinesLayoutTokens` fails because layout tokens are not yet defined.)

**Step 3: No commit yet** — Task 4 and 5 will satisfy these tests.

---

## Task 4: Edit `App.xaml` — drop 14 brushes, add override + layout + emergency tokens

**Files:**
- Modify: `src/TastileDesktop/App.xaml`

**Step 1: Open `App.xaml`**

The file currently defines a `ResourceDictionary.ThemeDictionaries` block with both `Dark` and `Light` sub-dictionaries containing the 19 custom brushes. Replace that block with the version below.

**Step 2: Replace the ThemeDictionaries block**

Inside `src/TastileDesktop/App.xaml`, locate the `<ResourceDictionary.ThemeDictionaries>` opening tag and the matching `</ResourceDictionary.ThemeDictionaries>` closing tag. Replace the entire block (including both Dark and Light children) with:

```xaml
<ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Dark">
        <!-- Override-accent layer: ThemeManager writes here at runtime -->
        <SolidColorBrush x:Key="OverrideAccentFillBrush" Color="#FF0078D4" />
        <SolidColorBrush x:Key="OverrideAccentFillSecondaryBrush" Color="#FF1A88DE" />
        <!-- Intervention-only emergency resources -->
        <SolidColorBrush x:Key="InterventionScrimBrush" Color="#A6000000" />
        <SolidColorBrush x:Key="InterventionEmergencyBrush" Color="{ThemeResource SystemFillColorCriticalBrush}" />
    </ResourceDictionary>

    <ResourceDictionary x:Key="Light">
        <SolidColorBrush x:Key="OverrideAccentFillBrush" Color="#FF0078D4" />
        <SolidColorBrush x:Key="OverrideAccentFillSecondaryBrush" Color="#FF1A88DE" />
        <SolidColorBrush x:Key="InterventionScrimBrush" Color="#66000000" />
        <SolidColorBrush x:Key="InterventionEmergencyBrush" Color="{ThemeResource SystemFillColorCriticalBrush}" />
    </ResourceDictionary>
</ResourceDictionary.ThemeDictionaries>
```

**Step 3: Add Layout tokens after `</ResourceDictionary.ThemeDictionaries>`**

Insert this block immediately after the closing `</ResourceDictionary.ThemeDictionaries>` tag, before the existing `<converters:BoolToVisibilityConverter>` line:

```xaml
<!-- Layout Tokens -->
<x:Double x:Key="WindowTitleBarHeight">32</x:Double>
<CornerRadius x:Key="WindowCardCornerRadius">8</CornerRadius>
<Thickness x:Key="CardBorderThickness">1</Thickness>
<x:Double x:Key="SettingRowHeight">48</x:Double>
<x:Double x:Key="ControlSpacingTight">8</x:Double>
<x:Double x:Key="ControlSpacingNormal">12</x:Double>
<x:Double x:Key="ControlSpacingLoose">20</x:Double>
```

**Step 4: Run `UiTokensTests` to verify pass**

Run:
```bash
dotnet test tests/TastileDesktop.Tests/TastileDesktop.Tests.csproj --filter "FullyQualifiedName~UiTokensTests" --nologo
```

Expected: all 3 `UiTokensTests` pass.

**Step 5: Run `dotnet format`**

Run:
```bash
dotnet format src/TastileDesktop/TastileDesktop.csproj --no-restore --verbosity minimal
```

Expected: no diff (App.xaml edit does not affect C# formatting).

**Step 6: Commit**

```bash
git add src/TastileDesktop/App.xaml tests/TastileDesktop.Tests/UiTokensTests.cs
git commit -m "feat(desktop): introduce override-accent layer + layout tokens

Drops 14 hand-rolled brushes in favor of Win11 System tokens
(TextFillColorPrimaryBrush, LayerFillColorDefaultBrush, etc.,
referenced directly by Phase 2 Controls). Keeps two named override
brushes that ThemeManager mutates at runtime. Adds six layout tokens
(WindowTitleBarHeight, ControlSpacing*, SettingRowHeight,
WindowCardCornerRadius) consumed by Phase 2.

UiTokensTests lock the contract via xUnit assertions against App.xaml.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 5: Update `ThemeManager.cs` to write to the override layer

**Files:**
- Modify: `src/TastileDesktop/Services/ThemeManager.cs:54-62`

**Step 1: Add the failing test first**

Create `tests/TastileDesktop.Tests/ThemeManagerOverrideTests.cs`:

```csharp
using System.Reflection;
using Xunit;

namespace TastileDesktop.Tests;

public class ThemeManagerOverrideTests
{
    [Fact]
    public void ThemeManager_NoLongerReferences_ObsoleteAccentKeys()
    {
        var asm = typeof(TastileDesktop.Services.ThemeManager).Assembly;
        var themeManager = asm.GetType("TastileDesktop.Services.ThemeManager")!;
        var source = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "TastileDesktop", "Services", "ThemeManager.cs"));

        Assert.DoesNotContain("\"AccentBrush\"", source);
        Assert.DoesNotContain("\"AppPrimaryBrush\"", source);
        Assert.DoesNotContain("\"AppPrimaryHoverBrush\"", source);
        Assert.Contains("\"OverrideAccentFillBrush\"", source);
        Assert.Contains("\"OverrideAccentFillSecondaryBrush\"", source);
    }
}
```

**Step 2: Run to verify failure**

Run:
```bash
dotnet test tests/TastileDesktop.Tests/TastileDesktop.Tests.csproj --filter "FullyQualifiedName~ThemeManagerOverrideTests" --nologo
```

Expected: FAIL (`"AccentBrush"` and friends are still in `ThemeManager.cs`).

**Step 3: Edit `ThemeManager.UpdateAccentInThemeDictionary`**

In `src/TastileDesktop/Services/ThemeManager.cs`, replace the method body (lines 54-62):

```csharp
private static void UpdateAccentInThemeDictionary(ResourceDictionary resources, string themeKey, string accentHex, string accentHoverHex)
{
    if (!resources.ThemeDictionaries.TryGetValue(themeKey, out var themeObj) || themeObj is not ResourceDictionary themeDict)
        return;

    SetBrush(themeDict, "AccentBrush", accentHex);
    SetBrush(themeDict, "AppPrimaryBrush", accentHex);
    SetBrush(themeDict, "AppPrimaryHoverBrush", accentHoverHex);
}
```

with:

```csharp
private static void UpdateAccentInThemeDictionary(ResourceDictionary resources, string themeKey, string accentHex, string accentHoverHex)
{
    if (!resources.ThemeDictionaries.TryGetValue(themeKey, out var themeObj) || themeObj is not ResourceDictionary themeDict)
        return;

    SetBrush(themeDict, "OverrideAccentFillBrush", accentHex);
    SetBrush(themeDict, "OverrideAccentFillSecondaryBrush", accentHoverHex);
}
```

**Step 4: Run test to verify pass**

Run:
```bash
dotnet test tests/TastileDesktop.Tests/TastileDesktop.Tests.csproj --filter "FullyQualifiedName~ThemeManagerOverrideTests" --nologo
```

Expected: PASS.

**Step 5: Run all desktop unit tests**

Run:
```bash
dotnet test tests/TastileDesktop.Tests/TastileDesktop.Tests.csproj --nologo
```

Expected: all tests pass (existing `ThemeManager`-related tests still green; the override-accent contract test passes; no regressions).

**Step 6: Commit**

```bash
git add src/TastileDesktop/Services/ThemeManager.cs tests/TastileDesktop.Tests/ThemeManagerOverrideTests.cs
git commit -m "refactor(desktop): point ThemeManager at override-accent layer

ThemeManager.UpdateAccentInThemeDictionary now writes only to
OverrideAccentFillBrush and OverrideAccentFillSecondaryBrush. The
previous custom keys (AccentBrush / AppPrimaryBrush / AppPrimaryHoverBrush)
have been removed in the prior commit; runtime Windows-accent override
behavior is preserved by this name change.

ThemeManagerOverrideTests xUnit-locks the contract.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 6: Replace `InterventionWindow.xaml` literal hex with `InterventionScrimBrush`

**Files:**
- Modify: `src/TastileDesktop/Views/InterventionWindow.xaml:8`

**Step 1: Replace the line**

In `src/TastileDesktop/Views/InterventionWindow.xaml`, replace line 8:

```xaml
    <Grid Background="#66000000">
```

with:

```xaml
    <Grid Background="{ThemeResource InterventionScrimBrush}">
```

**Step 2: Run all tests + format**

```bash
dotnet format src/TastileDesktop/TastileDesktop.csproj --verify-no-changes --no-restore --verbosity minimal
dotnet test tests/TastileDesktop.Tests/TastileDesktop.Tests.csproj --nologo
```

Expected: format clean; tests pass.

**Step 3: Commit**

```bash
git add src/TastileDesktop/Views/InterventionWindow.xaml
git commit -m "fix(desktop): replace InterventionWindow scrim literal with token

Background='#66000000' violated the AGENTS.md no-literal-hex rule.
Routes through the new InterventionScrimBrush token (Light '#66000000',
Dark '#A6000000').

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 7: Replace `IntegrationsWindow.xaml` error-color literal

**Files:**
- Modify: `src/TastileDesktop/Views/IntegrationsWindow.xaml:76`

**Step 1: Replace the line**

In `src/TastileDesktop/Views/IntegrationsWindow.xaml`, replace line 76:

```xaml
                <TextBlock x:Name="ErrorTextBlock" Text="" Foreground="#FFB00020" TextWrapping="Wrap" />
```

with:

```xaml
                <TextBlock x:Name="ErrorTextBlock" Text="" Foreground="{ThemeResource InterventionEmergencyBrush}" TextWrapping="Wrap" />
```

**Step 2: Run all tests + format**

```bash
dotnet format src/TastileDesktop/TastileDesktop.csproj --verify-no-changes --no-restore --verbosity minimal
dotnet test tests/TastileDesktop.Tests/TastileDesktop.Tests.csproj --nologo
```

Expected: clean.

**Step 3: Commit**

```bash
git add src/TastileDesktop/Views/IntegrationsWindow.xaml
git commit -m "fix(desktop): replace IntegrationsWindow error-color literal with token

Foreground='#FFB00020' violated AGENTS.md. Now routes through
InterventionEmergencyBrush (SystemFillColorCriticalBrush in both
themes). The Phase 4 InfoBar refactor will replace this TextBlock
entirely; this commit only fixes the hard-rule violation.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 8: Reserve `Styles/Backdrop.xaml` placeholder (comment-only)

**Files:**
- Create: `src/TastileDesktop/Styles/Backdrop.xaml`

**Step 1: Create Backdrop.xaml as a comment-only placeholder**

Create `src/TastileDesktop/Styles/Backdrop.xaml`:

```xaml
<?xml version="1.0" encoding="utf-8" ?>
<!--
    Backdrop resources for tastile-desktop.

    Phase 1 applies Mica via Window.SystemBackdrop in code-behind (see
    Tasks 9-10). Phase 2+ may introduce shared backdrop Styles here
    (e.g. DefaultMicaBackdrop, DefaultAcrylicBackdrop) that windows
    reference via Style="{StaticResource ...}".

    This file is intentionally empty so the merged-dictionaries slot is
    reserved; do not add resources here without a consumer.
-->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />
```

**Step 2: Run format + build (no tests)**

```bash
dotnet format src/TastileDesktop/TastileDesktop.csproj --verify-no-changes --no-restore --verbosity minimal
dotnet build src/TastileDesktop/TastileDesktop.csproj
```

Expected: clean build. (No consumer in Phase 1 — Tasks 9-10 set Mica via `SystemBackdrop` in code-behind. `App.xaml` `MergedDictionaries` is **not** modified here.)

**Step 3: Commit**

```bash
git add src/TastileDesktop/Styles/Backdrop.xaml
git commit -m "feat(desktop): reserve Styles/Backdrop.xaml placeholder

Empty ResourceDictionary with a comment header explaining that
Phase 1 applies Mica via Window.SystemBackdrop in code-behind
(Tasks 9-10). Phase 2+ may populate this file with shared Mica /
Acrylic Styles that windows reference via Style.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 9: Apply Mica to MainWindow

**Files:**
- Modify: `src/TastileDesktop/MainWindow.xaml.cs`

**Step 1: Open the file**

`MainWindow.xaml.cs` constructs the MainWindow. The constructor (or a method called immediately after construction) must set `SystemBackdrop`.

**Step 2: Add the SystemBackdrop assignment**

At the end of the `MainWindow` constructor (or, if the constructor body is non-trivial, immediately after `InitializeComponent();`), add:

```csharp
SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
```

The required `using` directives at the top of the file should already cover `Microsoft.UI.Xaml`; if not, add:

```csharp
using Microsoft.UI.Composition.SystemBackdrops;
```

**Step 3: Run format + build**

```bash
dotnet format src/TastileDesktop/TastileDesktop.csproj --verify-no-changes --no-restore --verbosity minimal
dotnet build src/TastileDesktop/TastileDesktop.csproj
```

Expected: clean build.

**Step 4: Manual smoke**

Run unpackaged:

```bash
dotnet run --project src/TastileDesktop
```

Confirm QuickPanel renders with the Mica backdrop on Windows 11 22H2+ (no behavior change to layout, placement, or content). Capture a screenshot for the PR.

**Step 5: Commit**

```bash
git add src/TastileDesktop/MainWindow.xaml.cs
git commit -m "feat(desktop): apply Mica backdrop to MainWindow

Sets SystemBackdrop = MicaBackdrop { Kind = Base } after InitializeComponent.
Layout, content, and placement (QuickPanelPlacementResolver) are untouched.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 10: Apply Mica to SettingsWindow, CreateTileWindow, TimelineWindow

**Files:**
- Modify: `src/TastileDesktop/Views/SettingsWindow.xaml.cs`
- Modify: `src/TastileDesktop/Views/CreateTileWindow.xaml.cs`
- Modify: `src/TastileDesktop/Views/TimelineWindow.xaml.cs`

**Step 1: Add `SystemBackdrop` to SettingsWindow**

In `src/TastileDesktop/Views/SettingsWindow.xaml.cs`, immediately after `InitializeComponent();`, add:

```csharp
SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
```

Ensure `using Microsoft.UI.Composition.SystemBackdrops;` is present (add if missing).

**Step 2: Same for CreateTileWindow**

In `src/TastileDesktop/Views/CreateTileWindow.xaml.cs`, immediately after `InitializeComponent();`, add:

```csharp
SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
```

Ensure the `using` directive is present.

**Step 3: Same for TimelineWindow**

In `src/TastileDesktop/Views/TimelineWindow.xaml.cs`, immediately after `InitializeComponent();`, add:

```csharp
SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
```

Ensure the `using` directive is present.

**Step 4: Format + build + tests**

```bash
dotnet format src/TastileDesktop/TastileDesktop.csproj --verify-no-changes --no-restore --verbosity minimal
dotnet test tests/TastileDesktop.Tests/TastileDesktop.Tests.csproj --nologo
dotnet build src/TastileDesktop/TastileDesktop.csproj
```

Expected: all green.

**Step 5: Commit**

```bash
git add src/TastileDesktop/Views/SettingsWindow.xaml.cs \
        src/TastileDesktop/Views/CreateTileWindow.xaml.cs \
        src/TastileDesktop/Views/TimelineWindow.xaml.cs
git commit -m "feat(desktop): apply Mica backdrop to Settings, CreateTile, Timeline

Extends Phase 1's Mica rollout. AuthWindow, ExecuteWindow,
IntegrationsWindow, InterventionWindow remain unchanged (Auth is a
single-step dialog; Execute/Integrations/Intervention have their own
chrome considerations deferred to Phase 4).

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 11: Create `scripts/check-ui-tokens.ps1`

This script enforces the hex-literal rule mechanically at CI time.

**Files:**
- Create: `scripts/check-ui-tokens.ps1`

**Step 1: Write the script**

Create `scripts/check-ui-tokens.ps1`:

```powershell
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$searchPaths = @(
    Join-Path $repoRoot "src/TastileDesktop/MainWindow.xaml"
    Join-Path $repoRoot "src/TastileDesktop/App.xaml"
    Join-Path $repoRoot "src/TastileDesktop/Styles"
    Join-Path $repoRoot "src/TastileDesktop/Views"
)
$excludeRegex = '\\(bin|obj)\'

$hexPattern = '#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?'
$removedBrushKeys = @(
    'AppBackgroundBrush', 'AppSurface0Brush', 'AppSurface1Brush',
    'AppSurface2Brush', 'AppSurfaceElevatedBrush', 'AppForegroundBrush',
    'AppForegroundMutedBrush', 'AppForegroundSubtleBrush',
    'AppBorderBrush', 'AppBorderStrongBrush', 'AppPrimaryBrush',
    'AppPrimaryForegroundBrush', 'AppPrimaryHoverBrush',
    'QuickPanelBackgroundBrush', 'QuickPanelBorderBrush',
    'PrimaryForegroundBrush', 'SecondaryForegroundBrush',
    'TertiaryForegroundBrush', 'AccentBrush'
)

$failures = New-Object System.Collections.Generic.List[string]

function Test-File {
    param([string]$Path)
    $content = Get-Content -Raw -Path $Path
    $rel = $Path.Substring($repoRoot.Length).TrimStart('\', '/')
    $isAppXaml = $rel -ieq "src\TastileDesktop\App.xaml"

    if (-not $isAppXaml) {
        $hexMatches = [regex]::Matches($content, $hexPattern)
        foreach ($m in $hexMatches) {
            $lineNumber = ($content.Substring(0, $m.Index) -split "`n").Count
            $failures.Add("$rel`:$lineNumber`: literal hex '$($m.Value)' (use ThemeResource instead).") | Out-Null
        }
    }

    foreach ($key in $removedBrushKeys) {
        if ($content -match "\{\s*(Static|Theme)Resource\s+$key\s*\}") {
            $lineNumber = ($content.Substring(0, $content.IndexOf("{$key}")) -split "`n").Count
            $failures.Add("$rel`:$lineNumber`: removed brush key '$key' is still referenced.") | Out-Null
        }
    }
}

function Search-Files {
    param([string[]]$Paths)
    foreach ($p in $Paths) {
        if (-not (Test-Path -LiteralPath $p)) { continue }
        if ((Get-Item -LiteralPath $p).PSIsContainer) {
            Get-ChildItem -LiteralPath $p -Recurse -Filter "*.xaml" -File |
                Where-Object { $_.FullName -notmatch $excludeRegex } |
                ForEach-Object { Test-File -Path $_.FullName }
        } else {
            if ($p -notmatch $excludeRegex) {
                Test-File -Path $p
            }
        }
    }
}

Search-Files -Paths $searchPaths

if ($failures.Count -gt 0) {
    Write-Host "==> UI token checks FAILED"
    $failures | ForEach-Object { Write-Host "    $_" }
    exit 1
}

Write-Host "==> UI token checks passed (0 hex literals, 0 removed brush references)"
exit 0
```

**Step 2: Run the script**

```bash
pwsh scripts/check-ui-tokens.ps1
```

Expected: `UI token checks passed (0 hex literals, 0 removed brush references)`.

(Note: the script intentionally **skips** `App.xaml` for hex-literal scans. `App.xaml` is the single source of truth for token definitions; the two `InterventionScrimBrush` literals there are intentional and reviewed per PR. Removing that exemption would force token definitions into a separate file, deferred to Phase 4.)

**Step 3: Commit**

```bash
git add scripts/check-ui-tokens.ps1
git commit -m "build(desktop): add check-ui-tokens.ps1 for hex-literal + removed-brush gates

Greps MainWindow.xaml, Styles/, Views/ for:
- hex literal colors (App.xaml is exempted as the token source of truth)
- StaticResource/ThemeResource references to the 19 removed brush keys

Phase 1 ships with the script active and green.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 12: Wire `check-ui-tokens.ps1` into `check.ps1`

**Files:**
- Modify: `scripts/check.ps1`

**Step 1: Add the new step**

In `scripts/check.ps1`, immediately after the `Assert-NoTimelineToolbarConnectorWiring` call (and before the closing `}` of the `try` block), add:

```powershell
    Write-Host "==> Verifying UI tokens (no hex literals, no removed brush references)"
    $checkUiTokensScript = Join-Path $PSScriptRoot "check-ui-tokens.ps1"
    Invoke-Step -Action { & pwsh -NoProfile -File $checkUiTokensScript } -FailureMessage "UI token checks failed. See output above."
```

**Step 2: Run the full `check.ps1`**

```bash
./scripts/check.ps1
```

Expected: every step green. (`check-ui-tokens.ps1` runs as part of the canonical local validation.)

**Step 3: Commit**

```bash
git add scripts/check.ps1
git commit -m "build(desktop): wire check-ui-tokens.ps1 into canonical check.ps1

Adds the UI-token gate to the existing format + vuln + tests + build +
TimelineWindow wiring chain. Phase 1 ships with this gate active.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 13: Bump version + final verification

**Files:**
- Modify: `src/TastileDesktop/Package.appxmanifest` (version attribute)
- Modify: `src/TastileDesktop/TastileDesktop.csproj` (if version is tracked there)

**Step 1: Locate the current version**

Run:
```bash
grep -nE 'Version=|<Version>' src/TastileDesktop/Package.appxmanifest src/TastileDesktop/TastileDesktop.csproj
```

The recent commit `e7001c2 chore(release): bump version to 0.4.0.0` shows the prior version is `0.4.0.0`. Phase 1 ships as **v0.4.1** patch.

**Step 2: Update to `0.4.1.0`**

In both files (where found), change every occurrence of `0.4.0.0` to `0.4.1.0`. Be precise: only the version literal, not surrounding text.

**Step 3: Run the full `check.ps1`**

```bash
./scripts/check.ps1
```

Expected: all green, including:
- `dotnet format --verify-no-changes` (clean)
- NuGet vulnerability scan (clean)
- Desktop unit tests (all pass — UiTokensTests, ThemeManagerOverrideTests, and all ~196 existing tests)
- Dual desktop build (Debug + Release / win-x64)
- TimelineWindow connector safety check
- UI token checks (Phase 1)

**Step 4: Commit**

```bash
git add src/TastileDesktop/Package.appxmanifest src/TastileDesktop/TastileDesktop.csproj
git commit -m "chore(release): bump version to 0.4.1.0

Phase 1 (Tokens) of the UI design-system pass ships as v0.4.1.
Spec: docs/superpowers/specs/2026-08-25-tastile-ui-design-system-pass-design.md
ADR: docs/adr/0007-ui-design-system-pass.md

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Self-review (run by planner before handoff)

1. **Spec coverage:**
   - §Tokens (Layer 1) — covered by Tasks 3, 4, 5.
   - §Architecture (Layer 0-5) — Layer 0 (Mica) covered by Tasks 8-10; Layer 1 by Task 4.
   - §Data Flow & Error Handling (InfoBar → token) — deferred to Phase 4.
   - §Testing (Phase gates) — Phase 1 gate is `check-ui-tokens.ps1` + xUnit (Tasks 11, 12).
   - §Risks R3, R10, R11 — R10 (Microsoft Learn MCP) addressed in Global Constraints; R11 (token-name collision) addressed in Task 2; R3 (QuickPanelPlacementResolver) deferred to Phase 3.
   - §Companion ADR — Task 1.
   - §Open Questions — `QuickPanelWidth` deferred to Phase 3; Snackbar / TeachingTip deferred to Phase 4; `SelectorBarWrap` naming deferred to Phase 2; PerceptualDiff threshold deferred to Phase 3+.

2. **Placeholder scan:** No "TBD", "TODO", "fill in", "implement later" strings present.

3. **Type consistency:** Override brush key names (`OverrideAccentFillBrush`, `OverrideAccentFillSecondaryBrush`) appear identically in Task 4 (App.xaml definition), Task 5 (ThemeManager.cs writer), Task 1 (ADR), and Task 11 (script exclusion list).

4. **Scope:** Single sprint (~2-5 days). 13 tasks, each independently shippable. Out-of-scope items (Controls, QuickPanel, remaining windows, A11y) are deferred to Phase 2-5 plans.