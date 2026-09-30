# Claude Code adapter

この repository の canonical contract は `AGENTS.md` である。作業前に全文を読むこと。

親 workspace 共通の設定 — `.claude/settings.json` の command guard、
`.agents/skills/` の Skill (`verify-tastile-change` ほか)、
`.agent-loop/` の durable checkpoint schema — は親 `tastile/` 直下のものが、この repository へ
walk-up で適用される。secret 値は Infisical の `/tastile/desktop` path から取得し、local `.env` を使わない。

旧 per-commit reviewer loop は root ADR-0021 により廃止済み。commit 前は
`../.agents/skills/verify-tastile-change/SKILL.md` に従い binding verification を行う。
ADR-0008 の checkpoint と通常の独立 review は維持する。

project-wide の規則を本ファイルへ複製しない。検証入口は `scripts/check.ps1`、
project knowledge / command / architecture / env-var key list は `AGENTS.md` を参照
すること。
