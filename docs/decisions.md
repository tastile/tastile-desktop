# Decisions

`tastile-desktop` は親ワークスペース `tastile/` 直下の child repository である。
このファイルには、**この child repository 自身の agent toolchain・plugin・tool
選定に閉じた decision** だけを残す。認証 / API / schema など複数 child にまたがる
decision は親 `tastile/docs/decisions.md` を正本とする。

## 2026-08-23 — agent toolchain init / audit

### 背景

`tastile-desktop` は Codex 指示を主として運用してきた child repository で、
既に次の要素が揃っている。

- `AGENTS.md` (Codex 向け dispatcher、stack / architecture / env / commands)
- `CLAUDE.md` (Claude Code 向け薄い adapter)
- `.claude/settings.json` (PreToolUse hook)
- `.claude/hooks/git-guard.mjs` (破壊的コマンド抑止)
- `scripts/check.ps1` (canonical validation entry point)
- `.github/workflows/ci.yml` (unit tests + desktop build)
- `.github/workflows/release.yml` (tag-triggered release with version validation)
- `tests/TastileDesktop.Tests` (196 unit tests passing)

`/init` 監査の結果、**大幅な再生成は不要** であり、ギャップは次の三点に
とどまる。

1. `.gitignore` に `.tmp/` `.reference/` `.worktrees/` が無い (project policy
   §21 / §30 / §31)
2. agent-toolchain decision を残す ADR が無い (project policy §7)
3. `.claude/skills/` が child 側になく、親 workspace の Skills に walk-up で
   依存しているが、その事実を明文化していない

### 決定

#### 1. 親 workspace の Agent Skills を walk-up で継承する (新規 ADR)

`verify-tastile-change` / `cross-repo-contract-check` / `tastile-precommit-review`
は親 `tastile/.agents/skills/` を正本とし、child 側では複製しない。`CLAUDE.md`
adapter がこの walk-up を参照する形を維持する。

- 採用根拠: child repo が独自の Skills を持つと親との同期負債が発生し、
  desktop 固有の skill が必要になったらその時点で追加する方が context cost
  が小さい。
- 却下: `desktop-build-skill`, `desktop-release-skill` 等の child-local Skill
  を新設する案。`scripts/check.ps1` と `scripts/build-desktop-installer.ps1`
  が既に canonical entry point として機能しており、追加 Skill の context
  cost > 効果。

#### 2. PreToolUse git-guard hook は `.claude/hooks/git-guard.mjs` に置く

`git reset --hard` / `git clean -f` / `git push --force` 等を pattern-match で
block する。実装は素朴なコマンド文字列 scanner であり、shell AST 解析では
ない (header comment に limitation を明記)。`exit 2` で `PreToolUse` を
中断する。

- 採用根拠: 既存 hook は十分に小さく、project-local に置けて、CI からも独立
  に動作する。ログは `.claude/logs/git-guard.log` に audit 用に記録される。
- 却下: pre-commit hook として `git commit` を gate する案。CI と `.claude/`
  hook の二重実装になり、policy §33 (pre-commit hook 新設禁止) に抵触する。

#### 3. canonical validation は `scripts/check.ps1`

- 単体テストのみ: `.\scripts\check.ps1 -SkipDesktopBuild`
- 通常: `.\scripts\check.ps1` (test + default build + win-x64 build +
  TimelineWindow connector wiring check)

CI (`.github/workflows/ci.yml`) も同じ script を呼ぶ。local と CI の
validation logic を統一する。

- 却下: `Makefile` / `justfile` の導入。PowerShell で十分。OS 依存を 1 言語
  に閉じる方が再現性が高い (Windows 必須の desktop app)。

#### 4. coverage 80% threshold は導入しない (現状維持)

`tests/TastileDesktop.Tests` には `coverlet.collector` が入っているが、
threshold は未設定。UI / Window 依存が大きい desktop の特性上、unit test
カバレッジの数値よりも contract test / resolver test の意味的網羅性を優先
する。`TimelineWindowLayoutTests` / `SettingsLayoutTests` / `*ContractsTests`
が layout / contract の非退化性を担保している。

- 再評価条件: contract test だけでは regression を catch できない実例が 2 件
  以上発生した場合、`coverlet.collector` の threshold 設定を再度検討する。

#### 5. test project を `TastileDesktop.slnx` へ加えない (現状維持)

slnx は `src/TastileDesktop/TastileDesktop.csproj` のみ参照する。test
project は `scripts/check.ps1` が `dotnet test` で個別に起動する。

- 採用根拠: Visual Studio の「slnx に無い project を build しない」挙動と
  `dotnet run` の両立。slnx は「GUI で primary project を開くためのもの」
  として割り切る。
- 再評価条件: IDE (VS / Rider) で「test が見えない」問題が新規 contributor
  から 2 件以上報告されたら見直す。

### 影響範囲

- コード変更: 無し
- `.gitignore`: 補強 (`.tmp/` `.reference/` `.worktrees/` `.agent-loop/`
  `.env.local` 等の OS / local dotenv / agent cache を追加)
- 新規 file: `docs/decisions.md` (本ファイル)
- AGENTS.md / CLAUDE.md: 既存で正しいため変更なし

### Rollback

- `.gitignore` の変更は個別行の削除で巻き戻し可能
- `docs/decisions.md` の削除は単体で巻き戻し可能
- `.claude/` 配下の変更を伴わないため、agent 環境への影響なし

## 2026-08-23 — project-local LSP / MCP additions

### 背景

`tastile-desktop` は C# / WinUI 3 / `net9.0-windows10.0.26100.0` + .NET 10 SDK
(`global.json` pin 10.0.202) という Microsoft プラットフォーム中心の child
repository であり、agent が vendor API の正確なシグネチャやバージョン固有の
挙動を問う機会が多い。`.NET SDK` は host にインストール済みだが、code
intelligence は手付かず、Microsoft 公式 docs / GitHub Actions への外部
参照経路も手動 (web fetch) だった。

### 決定

#### 1. LSP は opencode 組み込み `csharp` を使う (custom LSP を追加しない)

`opencode.json` に `"lsp": {}` を置いて組み込み LSP を全有効化する。
組み込み `csharp` LSP は `.cs` / `.csx` を自動検出し、`.NET SDK` 検出時に
自動起動する (opencode 公式 docs §LSP Servers)。

- 採用根拠: `global.json` で .NET 10 SDK が pin されており、要件を完全に満た
  す。`policy §8` 「native LSP で十分な場合は重複させない」に従い、`csharp-ls`
  (`razzmatazz/csharp-language-server`) や Microsoft 公式 Roslyn Language
  Server を別途立てることは行わない。
- 再評価条件: 組み込み LSP の diagnostics が誤って stale / 遅いなど業務に
  支障が出る具体的問題が 2 件以上発生した場合、`csharp-ls@0.26.0` の
  override を opencode.json に追加して評価する。

#### 2. MCP: `microsoft-learn` を default-on で導入

`https://learn.microsoft.com/api/mcp` を `type: "remote"` で登録する。Microsoft
公式 (CC-BY-4.0 + MIT, repo `microsoftdocs/mcp`)、no-auth / no-key の
Streamable HTTP endpoint。

- 採用根拠: この child repo は WindowsAppSDK 1.7 / WinUI 3 control API /
  `Microsoft.Toolkit.Uwp.Notifications` といった Microsoft 1st-party API を
  主に扱う。LLM の training data は 1-2 version 遅れる傾向があり、vendor
  公式 docs への直接経路を持つことが hallucination の抑制に直結する。
- 却下: `@microsoft/learn-cli@0.1.0` を local stdio で起動する案。実装を
  見て判明 — 同パッケージはターミナル向け CLI (search / fetch / doctor
  subcommand) であり、stdio MCP サーバとしては動かない。MCP 経路は
  remote endpoint のみが公式サポート。
- 却下: `@upstash/context7-mcp@4.0.3`。Microsoft 1st-party docs 経路として
  は `microsoft-learn` が正確で curated であり、Context7 の generic な
  scraping を併用する利点が薄い。`policy §8` 「局所的な project knowledge が
  重要なら generic docs service を優先しない」に該当。
- 再評価条件: `microsoft-learn` が 7 日以上連続して reachability error を
  返すか、Microsoft 公式が deprecate を告知した場合は Context7 を fallback
  として追加検討。

#### 3. MCP: `github` を opt-in (`enabled: false`) で導入

`@modelcontextprotocol/server-github@2025.4.8` を `type: "local"` で登録する
が default disabled。`GITHUB_PERSONAL_ACCESS_TOKEN` env var を required と
する。

- 採用根拠: この child repo は GitHub Actions による unit-test / dual build /
  release workflow を持っており、PR / CI / Release への agent 参照は確かに
  価値がある。一方 opencode 公式 docs が「GitHub MCP は token 消費が
  大きい」と明示警告しているため、default-on にすると全 session で
  context を浪費する。
- 運用: 必要な session / subagent 起動前に `opencode.json` で該当行を
  `enabled: true` に切り替え、終わったら戻す。または `opencode mcp list` で
  状態を確認しながら使う。
- 再評価条件: GitHub MCP の token 消費が実測で常時 5k token 未満である
  など low-cost が確認できた場合のみ default-on を再検討。

#### 4. Plugins は追加しない (現状維持)

opencode plugin system (`.opencode/plugins/`) は、project-specific な tool /
hook / integration を足す仕組み。今回は LSP / MCP で要件が満たせるため
plugin を追加しない。

- 採用根拠: `policy §6` 「重複した実装を理由なく導入しない」に従う。
- 再評価条件: LSP / MCP では表現できない host-specific な hook (例: dotnet
  build artifact からの symbol 抽出) が必要になった時点で再評価。

### 影響範囲

- 新規 file: `opencode.json` (project-local opencode config、LSP + MCP)
- 既存 file への変更: 無し
- AGENTS.md / CLAUDE.md / `.claude/` / `.gitignore` / `scripts/`: 既存で正し
  いため変更なし
- host 環境への副作用: 無し (`.NET SDK` / `bun` は既に必要要件として存在)

### 検証

- `opencode mcp list` → `microsoft-learn connected (https://learn.microsoft.com/api/mcp)`,
  `github disabled (bunx -y @modelcontextprotocol/server-github@2025.4.8)`
- `opencode mcp debug microsoft-learn` → HTTP 200, no-auth 経路が応答
- `opencode debug config` → 期待通り `lsp: {}` + 2 MCP entry (1 remote, 1 local)
- `git check-ignore` → `opencode.json` は tracked 扱い、副作用なし
- `.\scripts\check.ps1 -SkipDesktopBuild` → 既存 unit test 196/196 PASS

### Rollback

- `opencode.json` を削除するだけで組み込み LSP + 既存状態に戻る
- `microsoft-learn` のみ個別に切りたければ該当 block を削除 (built-in LSP と
  `github` は残る)

## 2026-08-23 — `check.ps1` に format / vulnerability gate を追加 + 死んだ test project を削除

### 背景

audit 後の follow-up として、canonical validation (`scripts/check.ps1`) に
二つの deterministic gate を足し、また AWS 移行後に空殻となった test project
を削除する。

- `dotnet format --verify-no-changes` を desktop / test 両 csproj に適用した
  ところ、24 + 5 = 29 ファイルに違反が検出された (import 順序違反、final
  newline 欠落、`.gitattributes` 違反の CRLF 改行)。`.editorconfig` と
  `.gitattributes` で規定されたスタイルと実コードが乖離していた状態。
- `dotnet list package --vulnerable --include-transitive` を desktop / test
  両 csproj に適用した結果、現時点で vulnerable な NuGet パッケージはゼロ。
  ただし CI に永続 gate として入れておかないと、新規 transitive vuln
  (例: `Microsoft.Web.WebView2` の advisory update) を取り逃す。
- `tests/TastileDesktop.Task2.Tests/` は csproj のみで `.cs` ファイルを持た
  ない空プロジェクト。`git log` で確認すると commit `06a8035` (`fix(tests):
  prune dead polling-service tests orphaned by AWS migration`) で中身が
  prune された残骸。`check.ps1` 側で「project があれば実行、なければ
  skip」という分岐を抱えていたが、これは将来の再発防止に役立たない。

### 決定

#### 1. `dotnet format --verify-no-changes` を `check.ps1` の先頭に追加

unit test より前に走らせ、format 違反があれば即 fail で developer に通知
する (fail-fast)。`--no-restore` で network を発生させない。

- 採用根拠: `.editorconfig` に C# style rule が既に定義されているのに、
  CI で誰も enforce していなかった。`policy §28` 「error / warning を本当の
  意味で 0 にする」と `policy §40` 「機械的に判定できる事実は決定論的 tool で
  検証する」に直接該当。
- 自動 fix: 29 ファイルの import ordering / final newline / EOL を機械的に
  修正した (この ADR に伴って含まれる commit 内に含まれる)。ロジック・
  コメント・identifier への変更はゼロ。`git diff --stat` で +186 / -160 行
  (機械的 reorder のみ)。
- 却下: `dotnet format whitespace` のみに限定する案。whitespace 違反だけ
  catch して import 違反は放置すると、policy §28 と矛盾する。`policy §28`
  「blanket ignore / warning suppression で green に見せかけない」に該当。

#### 2. `dotnet list package --vulnerable --include-transitive` を gate 化

desktop / test 両 csproj で走らせる。`policy §34` 「local と CI で別々の
validation logic を重複実装せず、可能な限り同じ project scripts を呼び出す」
に従い、`scripts/check.ps1` に集約。

- 採用根拠: CI には `.github/workflows/ci.yml` が既に存在し、unit test と
  build を走らせている。vulnerability scan を **CI と同じ script 経由で**
  入れれば、local と CI が同じ結果を返す。`policy §6`「再現性」と `policy
  §34` に該当。
- 却下: GitHub Dependabot / Renovate 等の自動 PR 生成ツールを別導入する案。
  現状 transitive vuln の追跡は GitHub の Dependabot alerts で十分機能してお
  り、CI gate を入れた上で Dependabot を後付けする余地はある (再評価条件)。
- 再評価条件: dependabot version update PR が蓄積して merge conflict が
  頻発するようになったら Renovate 等の aggregator を評価する。

#### 3. `tests/TastileDesktop.Task2.Tests/` を削除

空 csproj 1 ファイル (`.cs` 0 件) のみだった project を `git rm -r` で
削除。

- 採用根拠: `git log --all -- tests/TastileDesktop.Task2.Tests` で確認した
  結果、commit `06a8035` で polling-service tests が prune された残骸であり、
  新規 owner も新規 test も今後追加されない。空 project を残すと CI で
  「使用できるテストはありません」警告を出し続ける。`policy §16`「初期開発
  段階の互換性」- 死んだ legacy は保持せず削除。
- `check.ps1` から `if (Test-Path $task2TestProject) { ... }` 分岐を削除。
- `AGENTS.md` の「Two test projects exist ...」文を「One test project exists」
  に書き換え。
- 再評価条件: Task2 を冠する新しい test target が具体的に立ち上がる場合に
  は csproj を再作成する (deferred ではなく dispose する)。

#### 4. `TastileDesktop.slnx` への test project 追加は見送り (前回 ADR の再評価)

前回の ADR §5 で「IDE contributor から test が見えない問題が 2 件以上
報告されたら見直す」と再評価条件を設定したが、現時点で該当 feedback は
ない。`dotnet test` 直接起動で十分機能しており、slnx は primary project
を開くためのもの、という割り切りを維持する。

### 影響範囲

- `scripts/check.ps1`: format / vulnerability gate 追加、Task2 分岐削除
- `AGENTS.md`: Task2 言及を削除、Commands 冒頭のコメントを更新
- `docs/decisions.md`: 本 ADR 追記
- 削除: `tests/TastileDesktop.Task2.Tests/` (csproj 1 ファイル、bin/obj は
  working tree 上のみで未 track)
- 大規模 import reorder: 24 desktop + 5 test = 29 file, +186 / -160 行
  (純機械的: import ordering / final newline / EOL fix)

### 検証

- `.\scripts\check.ps1 -SkipDesktopBuild`:
  - `dotnet format --verify-no-changes` (desktop + test) → silent (no violations)
  - `dotnet list package --vulnerable --include-transitive` → "プロジェクト
    には vulnerabilities パッケージはありません" (×2)
  - `dotnet test -warnaserror` → 196 passed / 0 failed
- `git check-ignore` → 既存 tracked file はすべて維持、新規 ignore なし

### Rollback

- format gate を一時外したい場合: `check.ps1` の該当 2 行をコメントアウト。
  revert したくなったら git revert。
- Task2 project を復活させたい場合: `git revert` で本 commit を取り消す。
  別 ADR を起こして再導入の理由を記述する。

## 2026-08-23 — repo-wide build hardening + CI hardening + Dependabot

### 背景

`scripts/check.ps1` に format / vulnerability gate を入れた直後、次の 3 系統
に独立した改善余地が残っていることを確認した。

1. **MSBuild 設定が csproj ごとに重複している**: `<Nullable>enable</Nullable>`
   `<ImplicitUsings>enable</ImplicitUsings>` を desktop / test 両 csproj が
   個別に宣言している。`TreatWarningsAsErrors` は **誰も宣言していない** が、
   policy §28 として本来あるべき。SDK 暗黙の analyzer / code-style rule
   (IDE0080, IDE0290 等) は現状 warning 止まりで、build は通過してしまう。
2. **GitHub Actions に権限・並行制御・キャッシュの標準が未設定**:
   - `ci.yml` に `permissions:` 宣言なし (default は repository 単位で
     write 権限を含む広すぎるスコープ)
   - `concurrency:` なし — 同一 ref への連続 push で不要な runner を
     浪費する
   - NuGet キャッシュなし — `setup-dotnet@v4` には `cache: true` 引数が
     存在するのに未使用
   - `actions/checkout@v4` の `@v4` 形式は major-version までしか pin せず
     supply chain 観点で弱い
3. **Dependabot config 未設置**: 親 `tastile/` workspace の他 child には
   `.github/dependabot.yml` があり、Microsoft.WindowsAppSDK の MAJOR 上げや
   `actions/setup-dotnet` のマイナー上げを PR 化してくれている。desktop
   は手動追従になっている。

### 決定

#### 1. リポジトリ root に `Directory.Build.props` を新設

`src/TastileDesktop/Directory.Build.props` (XamlCompiler workaround 用) と
並列に、**repo root** に `Directory.Build.props` を置く。MSBuild は
project ディレクトリから祖先に向かって `Directory.Build.props` を自動
import するので、root に置けば `src/` と `tests/` 両方の csproj に一律に
適用される。

設定内容:
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — `policy §28` 直接
  該当
- `<AnalysisLevel>latest</AnalysisLevel>` — 最新 analyzer rule を有効化
- `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` — code-style
  violation を build warning に格上げ

- 採用根拠: 既存 desktop / test 両 build は 0 warning / 0 error で通過して
  おり、有効化しても安全。`policy §28`「error / warning を本当の意味で 0
  にする」と `policy §40`「機械的に判定できる事実は決定論的 tool で検証
  する」を build time gate として具体化する。
- 却下: `<TreatWarningsAsErrors>` を **個別 csproj** に置く案。3 csproj に
  同じ設定を書く重複が発生し、`policy §25`「dependency / 重複 library を
  理由なく併用しない」精神に反する。`Directory.Build.props` で集約する
  方が合理的。
- 検証: 一時的に `using System.*; using Microsoft.UI.Xaml;` の逆順 import
  を混入して build → 即 `error CS8955` で fail。復元後 0/0 で通過。Gate
  が実際に効くことを確認済み。
- 再評価条件: transitive dependency の upgrade で抑制不可能な warning が出
  た場合、`<NoWarn>` ではなく dependency 側 / コード側で修正する。`policy
  §28`「blanket ignore で green に見せかけない」。

#### 2. `ci.yml` の GitHub Actions ハードニング

3 系統の改善を 1 ファイルに集約:

- **Top-level `permissions: contents: read`**: GitHub Actions の default は
  `GITHUB_TOKEN` 経由で多くのスコープが暗黙付与される。`contents: read`
  を明示することで、unit-test / desktop-build 両 job が **必要最小限** の
  スコープに収まる。release 系 (`contents: write` / `id-token: write`)
  は release.yml 側で既に宣言済み。
- **`concurrency:` グループ**: 同一 ref への連続 push で古い SHA の run を
  即 cancel し runner を節約。`cancel-in-progress: true` で wait-in-line
  ではなく cancel。
- **`actions/setup-dotnet@v4` に `cache: true` + `cache-dependency-path`**:
  csproj から NuGet dependency graph を抽出し、`.nuget/packages` キャッシュ
  を job 間で再利用。`src/TastileDesktop/TastileDesktop.csproj` と
  `tests/TastileDesktop.Tests/TastileDesktop.Tests.csproj` の両方を指定
  (test project が別 dependency を持つため)。
- **`actions/checkout@v4` に `persist-credentials: false`**: 旧 default
  は checkout 時に git config に token を書き込む。post-job でも credential
  が残り得る。off に統一。

#### 3. `release.yml` に `concurrency:` 追加

release は **失敗時のみ retry すべき** workflow。同一 tag への複数 run が
並走すると `gh release create` が race condition を起こす。
`cancel-in-progress: false` で「前の run が終わるまで次の run は待つ」と
いうセマンティクスに。

#### 4. `.github/dependabot.yml` を新設

親 workspace の他 child と同形式。ただし:

- **Microsoft.WindowsAppSDK 系** を 1 つの group に集約 — MAJOR bump の
  breaking change が同時に大量 PR にならないように
- **xUnit / coverlet 系** を 1 つの group に集約
- **GitHub Actions** を `actions/*` で group
- weekly + 月曜 09:00 JST + PR 上限 5 (NuGet) / 3 (Actions)
- `MAJOR` 上げは自動生成 PR されるが、**この repo では auto-merge しない**
  (human review 必須)

- 採用根拠: 既に `dotnet list package --vulnerable` を gate 化しているの
  で Dependabot は補完。Dependabot が **先に上げてくれる** ことで、
  security advisory が出る前に更新 PR が来る可能性が上がる。
- 却下: Renovate 等の aggregator 導入。Dependabot が GitHub-native で
  credential 不要・UI 統合済み・permissions 不要で、`policy §6`「保守性:
  official / first-party 優先」に従う。

#### 5. `.gitignore` に `**/TestResults/` と coverage artifact を追加

`dotnet test --collect:"XPlat Code Coverage"` 実行時に生成される
`TestResults/{guid}/coverage.cobertura.xml` などが誤って stage される
経路を塞ぐ。`*.trx` `*.coverage` `coverage.opencover.xml` 等の関連形式も
まとめて ignore。

### 影響範囲

- 新規 file: `Directory.Build.props` (repo root), `.github/dependabot.yml`
- 変更 file:
  - `.github/workflows/ci.yml`: `permissions:` `concurrency:` `cache: true`
    `persist-credentials: false` を追加 (合計 +10 行程度)
  - `.github/workflows/release.yml`: `concurrency:` を追加 (+5 行)
  - `.gitignore`: TestResults / coverage artifact ignore を追加 (+7 行)
- AGENTS.md / `scripts/check.ps1` / 既存コード: 変更なし

### 検証

- `dotnet build src/TastileDesktop/TastileDesktop.csproj --no-restore`
  → 0 warning / 0 error (TreatWarningsAsErrors 有効)
- `dotnet test tests/TastileDesktop.Tests/...` → 196/0
- `.\scripts\check.ps1 -SkipDesktopBuild` → 全 3 gate PASS
- `node -e` で `.github/dependabot.yml` の基本構造チェック (version: 2,
  updates: key 存在)
- `git check-ignore -v tests/TastileDesktop.Tests/TestResults/...`
  → `.gitignore` 行 40 で catch
- 一時的逆順 import 混入 → `error CS8955` で即 fail (gate 動作確認) →
  復元後 0/0

### Rollback

- `Directory.Build.props` を削除 → 旧 csproj 個別設定に戻る
- `ci.yml` / `release.yml` の追加行を削除 → 元の状態
- `.github/dependabot.yml` を削除 → Dependabot PR 自動生成停止
- `.gitignore` の TestResults 行を削除 → 個別除外が必要な file だけ復活

### 既知の残存事項 (再評価条件付き)

- Actions SHA pin (immutable ref): `@v4` は major-version pin までしか
  効かず supply chain 観点で `@08eba0b27e820071cde6df949e0b9ba4906955`
  形式が推奨されるが、各 action の SHA を維持する運用負荷 > リスク低減
  効果と現時点で判断。`policy §6`「保守性」軸で一旦 deferred とし、
  dependabot が v5 を上げるタイミングで再評価する。
- `dtolnay/rust-toolchain@stable`: float tag。同上の理由で一旦 deferred。
- Dependabot が生成する MAJOR bump PR の review / merge rotation は
  別途運用設計が必要。本 ADR は file 配置のみ。
