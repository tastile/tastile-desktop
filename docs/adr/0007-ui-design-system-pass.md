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
