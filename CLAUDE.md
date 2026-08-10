# Claude Code adapter

この repository の canonical contract は `AGENTS.md` である。作業前に全文を読むこと。

親 workspace 共通の設定 — `.claude/settings.json` の PreToolUse hook (commit review /
command guard)、`.agents/skills/` の Skill (`verify-tastile-change` ほか)、
`.agent-loop/` の独立 review gate — は親 `tastile/` 直下のものが、この repository へ
walk-up で適用される。project-local の `.env` は本 repository のものをそのまま使う。

project-wide の規則を本ファイルへ複製しない。検証入口は `scripts/check.ps1`、
project knowledge / command / architecture / env-var key list は `AGENTS.md` を参照
すること。
