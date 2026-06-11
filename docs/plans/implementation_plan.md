# tastile-desktop: ローカルデーモン廃止 → AWS リモート API 接続への移行

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** `tastile-desktop` (C# WinUI 3) を、ローカル `tastile-daemon.exe` (127.0.0.1:3140) への直接接続から、`https://beta.tastile.app` 上の AWS リモート API への **Cognito Bearer JWT 認証** 接続に移行する。tastile-web の `lib/cognito/*` + `lib/daemon/client.ts` と同じパターンを踏襲。

**Architecture:**

```
TastileDesktop (WinUI 3)  ──HTTPS + Bearer JWT──>  beta.tastile.app  ──>  tastile-core (EC2:3140)
   ├─ CognitoAuthService (PKCE + refresh)
   ├─ CoreApiClient (Bearer header, 401→refresh→retry)
   ├─ EventDrivenPoller (no background timer; user-action / focus / 60s-idle)
   └─ SecureTokenStore (DPAPI protected, tokens-only)
```

**Tech Stack:** C# / .NET 10 / WinUI 3 / CommunityToolkit.Mvvm / HttpClient + `System.Security.Cryptography.ProtectedData` (DPAPI) / `Windows.Security.Credentials.PasswordVault` (代替可) / WebAuthn-OAuth (Cognito Hosted UI 経由)

---

## Context — なぜこの変更か

tastile-core は新しいアーキテクチャ (fact-interpretation engine, 2026-05-28 改訂) へ移行し、tastile-web はすでにこの新アーキテクチャの AWS リモート API へ直接接続している (2026-06-07 設計)。しかし tastile-desktop は旧来の **「localhost で子プロセス `tastile-daemon.exe` を起動して、1秒 tick + 4エンドポイント並列ポーリング + SSE で同期する」モデル** のまま停滞している。

この旧モデルは以下に問題を抱える:
- **常時プロセスが複数走る** (デスクトッププロセス + 子デーモンプロセス) — タスクマネージャに常駐
- **ローカルにタイルデータを SQLite/JSON で持つ** — 同期競合・破損・古いデータの原因
- **OS レベルの介入ウィンドウがデーモン駆動** — 挙動が追跡困難
- **新しい fact-based API とは別系統** — 修正が両方に必要

ユーザー指示 (2026-06-09):
- Cognito Client ID = tastile-web と同一 (`2b9fkkb4u5di8veelnmjkmnldj`)
- **Tick完全廃止** (常時プロセス禁止)
- **dev mode のみ docker ローカル API 接続可**、メインはリモート API
- **ローカルに本データは絶対残さない** (auth token のみ保持可)
- **要らなくなった箇所は思いきり削除** (deprecation comment ではなくファイルごと削除)
- **前提から作り変える**

意図する成果: tastile-desktop を tastile-web と同じ AWS リモート API 接続モデルに揃え、コードベースを新アーキテクチャに揃える。バックグラウンドの常駐プロセスを 0 にする。

---

## 0. 非ゴール (Hard Requirements)

1. **常時プロセス禁止**: 1 秒 tick (`/commands/tick`)、DispatcherTimer ベース wall-clock loop、SSE 接続はデフォルト OFF。`tastile-daemon.exe` 子プロセスは起動しない。
2. **ローカルデータ最小**: 認証トークン (id_token / refresh_token) のみ DPAPI で保護して保存可。タイルデータ・ログ・JSON 状態・SQLite は **一切** 作成しない。
3. **docker ローカル API は開発専用**: `TASTILE_API_BASE_URL` 環境変数で `http://127.0.0.1:3140` へ切替可能。プロダクションビルドのデフォルトは `https://beta.tastile.app`。
4. **デッドコード削除**: 新モデルに合わないファイルは `// removed` ではなくファイルごと削除。
5. **Cognito Client ID**: `2b9fkkb4u5di8veelnmjkmnldj` (tastile-web と同一、env var で上書き可)。

---

## 1. アーキテクチャ詳細

### 1.1 リアルタイム更新戦略 (3 つのイベント源)

「常時プロセス禁止」と「UI が古い情報を表示しない」の両立:

| イベント源 | 発火タイミング | ポーリング先 |
|---|---|---|
| ユーザー操作 | タイル作成/開始/完了/中断/Extend/メモ等のコマンド送信直後 | `EventDrivenPoller.RefreshAsync()` |
| ウィンドウフォーカス | `MainWindow.Activated` / `TilesWindow.Activated` | `EventDrivenPoller.RefreshAsync()` (debounce 1s) |
| アイドル (長時間操作なし) | 最後の `RefreshAsync` から 60 秒経過 | `EventDrivenPoller.RefreshAsync()` (DispatcherTimer 1 個のみ) |

- `DispatcherTimer` は **最大 1 個** しか立ち上がらない (PollingService の 4 並列とは根本的に異なる)
- タイマーはユーザー操作のたびに `Reset()` される (連続発火しない)
- SSE は `TASTILE_ENABLE_SSE=1` を明示した時だけ別タスクで開く。デフォルト OFF。
- ポーリング先は `/read/execution-view`, `/read/tiles`, `/views/pending-prompt`, `/views/timeline/today` の 4 つを 1 セット (PollingService と同じ) — ただし「常時」走らない点が異なる

### 1.2 認証フロー (Cognito Hosted UI + PKCE)

1. 起動 → `SecureTokenStore.LoadAsync()` (DPAPI 復号) → トークンあれば復号 → `CognitoAuthService` に格納
2. 未認証なら `AuthWindow` 表示 → "Sign in" ボタン → `CognitoAuthService.StartHostedUiAsync()`
   - PKCE pair 生成 (verifier / S256 challenge)
   - state 生成 (CSRF 対策)
   - URL: `https://tastile-beta.auth.ap-northeast-1.amazoncognito.com/oauth2/authorize?client_id=...&response_type=code&scope=openid+email+profile&redirect_uri=tastile%3A%2F%2Fauth%2Fcallback&code_challenge=...&code_challenge_method=S256&state=...`
   - システム既定ブラウザで開く (`Launcher.LaunchUriAsync`)
   - pending state を `CognitoAuthService.RegisterPendingState(state, verifier, redirect)` に登録
   - `TaskCompletionSource` を立ててコールバックを待つ
3. ブラウザで Cognito 認証 → リダイレクト `tastile://auth/callback?code=...&state=...` → Windows が `tastile://` プロトコル経由でデスクトップを再起動 (または既に起動中なら `ProtocolHandler` のハンドラ着火)
4. `App.xaml.cs.HandleOAuthCallbackAsync` → state 検証 → `CognitoAuthService.HandleAuthorizationCodeAsync(code, state, verifier)` → `POST https://tastile-beta.auth.ap-northeast-1.amazoncognito.com/oauth2/token` (grant_type=authorization_code, code, redirect_uri, client_id, code_verifier) → トークン取得 → `SecureTokenStore.SaveAsync()` → TCS 完了 → `AuthWindow` 閉じる → `MainWindow` 表示
5. 以降、`CoreApiClient` の全リクエストに `Authorization: Bearer <id_token>` を付与
6. 401 受信 → `CognitoAuthService.RefreshAsync()` (grant_type=refresh_token) → 新しい id_token → 1 回リトライ

### 1.3 設定ソース (env 変数中心)

| 環境変数 | デフォルト | 用途 |
|---|---|---|
| `TASTILE_API_BASE_URL` | `https://beta.tastile.app` | API ベース URL (docker dev は `http://127.0.0.1:3140`) |
| `TASTILE_COGNITO_CLIENT_ID` | `2b9fkkb4u5di8veelnmjkmnldj` | Cognito App Client ID |
| `TASTILE_COGNITO_USER_POOL_ID` | `ap-northeast-1_pwYcPWOyR` | User Pool ID |
| `TASTILE_COGNITO_HOSTED_UI_DOMAIN` | `tastile-beta` | Hosted UI ドメインプレフィックス |
| `TASTILE_COGNITO_REGION` | `ap-northeast-1` | AWS リージョン |
| `TASTILE_COGNITO_CALLBACK_URL` | `tastile://auth/callback` | カスタムスキーム (Cognito User Pool App Client に登録) |
| `TASTILE_POLL_IDLE_SECONDS` | `60` | アイドル時ポーリング間隔 (0 で完全無効) |
| `TASTILE_ENABLE_SSE` | 未設定 (= OFF) | `1` で SSE 有効化 (プロダクション初期は OFF 推奨) |

`settings.json` (`SettingsService`) には書かない。`%APPDATA%\Tastile\settings.json` には UI 設定 (テーマ・配色・トースト等) のみ残す。

---

## 2. ファイル変更リスト

### 2.1 削除 (Delete) — ファイルごと

| ファイル | 削除理由 |
|---|---|
| `src/TastileDesktop/Services/DaemonManager.cs` | 子プロセス `tastile-daemon.exe` 起動ロジック全体 |
| `src/TastileDesktop/Services/DaemonCompatibility.cs` | ローカルデーモンバイナリの SHA256 検証 |
| `src/TastileDesktop/Services/PollingService.cs` | 1 秒 tick + 4 エンドポイント並列 + SSE loop + 200ms throttle の寄せ集め |
| `src/TastileDesktop/Services/PollingHealthCoordinator.cs` | PollingService と運命共同体、空実装 |
| `src/TastileDesktop/Services/LocalOAuthServer.cs` | 既に `[Obsolete(error: true)]` |
| `src/TastileDesktop/Services/OAuthCallbackHandoff.cs` | ファイルベース state 受け渡し (プロセス内 TCS で完結) |
| `src/TastileDesktop/Services/PkceHelper.cs` | `CognitoAuthService` 内に PKCE 統合、別ファイル不要 |
| `src/TastileDesktop/Services/ITilesChangedSource.cs` | PollingService 専用 interface |
| `src/TastileDesktop/Services/RuntimeProfile.cs` | `127.0.0.1:3140/3141` ハードコード + profile 分岐 (env 変数で十分) |
| `TastileDesktop.csproj`: `BuildBundledDaemon`, `BundleDaemonBinary`, `VerifyBundledDaemon` Target | MSBuild で daemon バイナリを bundle する処理 |
| `TastileDesktop.csproj`: `DaemonBinaryPath`, `DaemonRustTarget` プロパティ | 同上 |

### 2.2 新規作成 (Create)

| パス | 役割 |
|---|---|
| `src/TastileDesktop/Services/CognitoAuthService.cs` | PKCE 生成、Hosted UI URL 構築、トークン交換、リフレッシュ、サインアウト、JWT claims 解析、pending state 管理 |
| `src/TastileDesktop/Services/SecureTokenStore.cs` | DPAPI でトークン保護保存。`%LOCALAPPDATA%\Tastile\Auth\credentials.bin` 1 ファイル。`ITokenStore` interface 経由でテスタブル |
| `src/TastileDesktop/Services/EventDrivenPoller.cs` | ユーザー操作 / ウィンドウフォーカス / アイドル (60s) で `RefreshAsync` を発火。`PollingService` の `OnUIUpdateTick` 200ms throttle 機構は残す。SSE はフラグで切替 |
| `src/TastileDesktop/Services/AppSettings.cs` | 環境変数からの設定読込静的クラス。`ApiBaseUrl`, `Cognito`, `PollIdleSeconds`, `EnableSse` |
| `src/TastileDesktop/Models/CognitoConfig.cs` | `TryFromEnv()` 静的メソッド (web の `lib/cognito/env.ts` の C# 移植) |
| `src/TastileDesktop/Models/AuthSession.cs` | 新規セッション record (id_token / access_token / refresh_token / sub / email / exp)。旧 `Services/AuthService.cs` 内の同名 record とは別物なので `TastileDesktop.Models` 名前空間に隔離 |
| `src/TastileDesktop/Services/JwtClaims.cs` | `ParseIdToken(string idToken)` で `sub` / `email` / `exp` 抽出 (検証はしない、claim 抽出のみ) |
| `src/TastileDesktop/Services/Pkce.cs` | `GenerateVerifier()`, `ComputeS256Challenge(verifier)` 静的ヘルパ |
| `src/TastileDesktop/Services/ITokenStore.cs` | テスタビリティ用の interface。`SecureTokenStore` が実装 |

### 2.3 修正 (Modify)

| ファイル | 変更点 |
|---|---|
| `src/TastileDesktop/Services/CoreApiClient.cs` | ① base URL を `AppSettings.ApiBaseUrl` から取得。② コンストラクタで `Func<Task<string?>>? getAccessToken` を受け取り、各リクエストに `Authorization: Bearer` ヘッダ付与。③ 401 受信時 → `CognitoAuthService` 経由で refresh → 1 回リトライ。④ `HttpClient` デフォルト Timeout を 10s に。⑤ `TriggerTickAsync` / `TriggerSyncAsync` / `ResetLocalSyncDataAsync` / `RedownloadRemoteSyncDataAsync` / `GetSyncStatusAsync` / `GetRuntimePathsAsync` / `StartOAuth*` / `SignInWithOAuth*` / `GetSessionAsync` / `RestoreSessionAsync` / `IsAuthenticatedAsync` を **削除** (Cognito 責務移譲、ローカルデータなし)。⑥ `StreamStateEventsAsync` は残す (`TASTILE_ENABLE_SSE=1` 用)。⑦ `DebugLogPath` (デーモン由来ログパス) 削除 |
| `src/TastileDesktop/Services/AuthService.cs` | 全面書き換え。`CognitoAuthService.Instance` への **ファサード** に降格。`IsAuthenticated`, `CurrentSession`, `UserEmail`, `UserId`, `AuthStateChanged`, `GetAccessTokenAsync`, `SignOutAsync` のみ残す。`InitializeAsync` / `SaveSessionToFile` / `LoadSessionFromFile` / `DeleteSessionFile` / `RefreshSessionFromDaemonAsync` 削除 |
| `src/TastileDesktop/App.xaml.cs` | ① `DaemonManager` 関連全削除。② `OnLaunched` の `EnsureRunningAsync` 削除。③ 起動フロー再設計: `CognitoAuthService.TryLoadFromStoreAsync()` → 未認証なら `AuthWindow` → 認証後 `MainWindow`。④ `_api = new CoreApiClient(AppSettings.ApiBaseUrl, AuthService.Instance.GetAccessTokenAsync, CognitoAuthService.Instance)` で初期化。⑤ `await apiClient.TriggerTickAsync()` 削除。⑥ `HandleOAuthCallbackAsync` を `CognitoAuthService.HandleAuthorizationCodeAsync` 委譲 |
| `src/TastileDesktop/MainWindow.xaml.cs` | `this.Activated += OnWindowActivated;` 追加 → `MainViewModel.RefreshAsync()` 呼ぶ |
| `src/TastileDesktop/Views/AuthWindow.xaml.cs` | ① `_api.StartBrowserAuthFlowAsync` → `CognitoAuthService.StartHostedUiAsync()` に置換。② ポーリングループ (2s 間隔で daemon の flow status 覗く) 削除。③ `AuthWindowCompletionSource` (static `TaskCompletionSource`) を待って完了判定 |
| `src/TastileDesktop/Views/TilesWindow.xaml.cs` | `Activated` イベントで `RefreshAsync` トリガ |
| `src/TastileDesktop/ViewModels/MainViewModel.cs` | ① `_pollingService` フィールド・参照を全削除、`EventDrivenPoller` に置換。② 既存の `ITilesChangedSource` 参照を `EventDrivenPoller` の同名 event に書き換え。③ コマンド後の `await _pollingService.PollAsync()` を `await _poller.RefreshAsync()` に。④ `InitializeAsync` で `await _poller.StartAsync()` 呼ぶ |
| `src/TastileDesktop/Views/SettingsWindow.xaml(.cs)` | (任意) 上級者向けに `ApiBaseUrl` テキストボックスを追加 (env 変数優先) |
| `src/TastileDesktop/Services/SettingsService.cs` | `TastileSettings` から daemon 由来の設定が残っていないか確認。`UpdateManifestUrl` (リモート URL) は残す |
| `src/TastileDesktop/Services/TrayIconService.cs` | `SignInWithGoogleAsync` を `CognitoAuthService.StartHostedUiAsync()` のみ呼ぶシンプル版に。daemon ポーリング削除 |
| `src/TastileDesktop/Services/InterventionEngine.cs` | `PollingService` 参照 → `EventDrivenPoller` 参照に置換 (他は変更なし) |
| `src/TastileDesktop/ProtocolHandler.cs` | `tastile://auth/callback?code=...&state=...` を `App.HandleOAuthCallbackAsync` にディスパッチするだけの最小実装に簡素化 |
| `src/TastileDesktop/AuthResult.cs` | レコード (Success, ErrorCode) は維持。実装は Cognito 用に |
| `src/TastileDesktop/Services/NotificationService.cs` | 変更なし (CoreApiClient 経由のコマンド呼び出しは継続) |
| `src/TastileDesktop/Services/AppUpdateService.cs` | 変更なし (既に `https://tastile.app/api/version` を叩いている) |
| `TastileDesktop.csproj` | ① `DaemonBinaryPath` / `DaemonRustTarget` 削除、② `BuildBundledDaemon` / `BundleDaemonBinary` / `VerifyBundledDaemon` Target 削除、③ `StopRunningTastileProcesses` から `tastile-daemon` kill 部分削除 |
| `TastileDesktop/Package.appxmanifest` | `tastile://` プロトコル宣言は **維持** (Cognito カスタムスキーム) |
| `src/TastileDesktop/app.manifest` | 変更なし |

---

## 3. 新規ファイル詳細

### 3.1 `Services/AppSettings.cs`

```csharp
namespace TastileDesktop.Services;

public static class AppSettings
{
    public static string ApiBaseUrl =>
        Environment.GetEnvironmentVariable("TASTILE_API_BASE_URL")?.Trim() is { Length: > 0 } url
            ? url.TrimEnd('/')
            : "https://beta.tastile.app";

    public static CognitoConfig Cognito => CognitoConfig.TryFromEnv()
        ?? throw new InvalidOperationException("Cognito config not set. TASTILE_COGNITO_CLIENT_ID is required.");

    public static int PollIdleSeconds =>
        int.TryParse(Environment.GetEnvironmentVariable("TASTILE_POLL_IDLE_SECONDS"), out var s) && s >= 0
            ? s
            : 60;

    public static bool EnableSse =>
        Environment.GetEnvironmentVariable("TASTILE_ENABLE_SSE") == "1";
}
```

### 3.2 `Models/CognitoConfig.cs`

```csharp
namespace TastileDesktop.Models;

public sealed record CognitoConfig(
    string UserPoolId,
    string ClientId,
    string HostedUiDomain,
    string Region,
    string CallbackUrl)
{
    public string Issuer => $"https://cognito-idp.{Region}.amazonaws.com/{UserPoolId}";
    public string HostedUiBaseUrl => $"https://{HostedUiDomain}.auth.{Region}.amazoncognito.com";

    public static CognitoConfig? TryFromEnv()
    {
        var clientId = Environment.GetEnvironmentVariable("TASTILE_COGNITO_CLIENT_ID")?.Trim();
        if (string.IsNullOrEmpty(clientId)) return null;
        return new CognitoConfig(
            UserPoolId: Environment.GetEnvironmentVariable("TASTILE_COGNITO_USER_POOL_ID")?.Trim() ?? "ap-northeast-1_pwYcPWOyR",
            ClientId: clientId,
            HostedUiDomain: Environment.GetEnvironmentVariable("TASTILE_COGNITO_HOSTED_UI_DOMAIN")?.Trim() ?? "tastile-beta",
            Region: Environment.GetEnvironmentVariable("TASTILE_COGNITO_REGION")?.Trim() ?? "ap-northeast-1",
            CallbackUrl: Environment.GetEnvironmentVariable("TASTILE_COGNITO_CALLBACK_URL")?.Trim() ?? "tastile://auth/callback");
    }
}
```

### 3.3 `Models/AuthSession.cs`

```csharp
namespace TastileDesktop.Models;

public sealed record AuthSession(
    string IdToken,
    string AccessToken,
    string RefreshToken,
    string Sub,
    string? Email,
    DateTimeOffset ExpiresAt);
```

### 3.4 `Services/Pkce.cs`

```csharp
namespace TastileDesktop.Services;

public static class Pkce
{
    public static string GenerateVerifier(int length = 64)
    {
        // RFC 7636: 43-128 chars, [A-Z][a-z][0-9]\-._~
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=')
            [..length];
    }

    public static string ComputeS256Challenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
```

### 3.5 `Services/JwtClaims.cs`

```csharp
namespace TastileDesktop.Services;

public static class JwtClaims
{
    public static (string Sub, string? Email, long Exp) ParseIdToken(string idToken)
    {
        var parts = idToken.Split('.');
        if (parts.Length < 2) throw new FormatException("Invalid JWT");
        var payloadJson = Base64UrlDecode(parts[1]);
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;
        return (
            Sub: root.GetProperty("sub").GetString() ?? throw new FormatException("Missing sub"),
            Email: root.TryGetProperty("email", out var e) ? e.GetString() : null,
            Exp: root.GetProperty("exp").GetInt64()
        );
    }

    private static string Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4) { case 2: padded += "=="; break; case 3: padded += "="; break; }
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
```

### 3.6 `Services/ITokenStore.cs` + `Services/SecureTokenStore.cs`

```csharp
// ITokenStore.cs
public interface ITokenStore
{
    Task<AuthSession?> LoadAsync();
    Task SaveAsync(AuthSession session);
    Task ClearAsync();
}

// SecureTokenStore.cs
public sealed class SecureTokenStore : ITokenStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tastile", "Auth", "credentials.bin");

    public async Task<AuthSession?> LoadAsync()
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            var encrypted = await File.ReadAllBytesAsync(FilePath);
            var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<AuthSession>(plain);
        }
        catch { return null; }  // DPAPI 復号失敗 = ユーザー切り替え or 破損
    }

    public async Task SaveAsync(AuthSession session)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var plain = JsonSerializer.SerializeToUtf8Bytes(session);
        var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(FilePath, encrypted);
    }

    public Task ClearAsync()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
        return Task.CompletedTask;
    }
}
```

### 3.7 `Services/CognitoAuthService.cs`

```csharp
namespace TastileDesktop.Services;

public sealed class CognitoAuthService
{
    public static CognitoAuthService Instance { get; } = new(new SecureTokenStore());

    private readonly ITokenStore _store;
    private AuthSession? _current;
    private (string State, string CodeVerifier, string RedirectUri, TaskCompletionSource<AuthResult> Tcs)? _pending;

    public AuthSession? CurrentSession => _current;
    public bool IsAuthenticated => _current is { } s && s.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(-30);
    public event EventHandler? AuthStateChanged;

    public CognitoAuthService(ITokenStore store) => _store = store;

    public async Task<AuthSession?> TryLoadFromStoreAsync()
    {
        _current = await _store.LoadAsync();
        if (_current != null) AuthStateChanged?.Invoke(this, EventArgs.Empty);
        return _current;
    }

    public Task<HostedUiStart> StartHostedUiAsync()
    {
        var cfg = AppSettings.Cognito;
        var verifier = Pkce.GenerateVerifier();
        var challenge = Pkce.ComputeS256Challenge(verifier);
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

        var authUrl = $"{cfg.HostedUiBaseUrl}/oauth2/authorize?" +
            $"client_id={Uri.EscapeDataString(cfg.ClientId)}" +
            $"&response_type=code" +
            $"&scope=openid+email+profile" +
            $"&redirect_uri={Uri.EscapeDataString(cfg.CallbackUrl)}" +
            $"&code_challenge={challenge}" +
            $"&code_challenge_method=S256" +
            $"&state={state}";

        var tcs = new TaskCompletionSource<AuthResult>();
        _pending = (state, verifier, cfg.CallbackUrl, tcs);

        // ブラウザ起動 (WinUI 3)
        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(authUrl));
        return Task.FromResult(new HostedUiStart(authUrl, state, cfg.CallbackUrl));
    }

    public async Task<AuthResult> HandleAuthorizationCodeAsync(string code, string state)
    {
        if (_pending is not { } p) return new AuthResult(false, "no_pending_flow");
        if (p.State != state) return new AuthResult(false, "state_mismatch");

        var cfg = AppSettings.Cognito;
        var tokenUrl = $"{cfg.HostedUiBaseUrl}/oauth2/token";
        using var client = new HttpClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = cfg.ClientId,
            ["code"] = code,
            ["redirect_uri"] = p.RedirectUri,
            ["code_verifier"] = p.CodeVerifier,
        });
        var resp = await client.PostAsync(tokenUrl, form);
        var json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) return new AuthResult(false, $"token_exchange_failed: {json}");

        var token = JsonSerializer.Deserialize<TokenResponse>(json)!;
        var (sub, email, exp) = JwtClaims.ParseIdToken(token.IdToken);
        var session = new AuthSession(
            IdToken: token.IdToken,
            AccessToken: token.AccessToken,
            RefreshToken: token.RefreshToken,
            Sub: sub,
            Email: email,
            ExpiresAt: DateTimeOffset.FromUnixTimeSeconds(exp));

        await _store.SaveAsync(session);
        _current = session;
        _pending = null;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
        p.Tcs.TrySetResult(new AuthResult(true));
        return new AuthResult(true);
    }

    public async Task<AuthSession?> RefreshAsync()
    {
        if (_current is not { RefreshToken: { Length: > 0 } rt }) return null;
        var cfg = AppSettings.Cognito;
        using var client = new HttpClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = cfg.ClientId,
            ["refresh_token"] = rt,
        });
        var resp = await client.PostAsync($"{cfg.HostedUiBaseUrl}/oauth2/token", form);
        if (!resp.IsSuccessStatusCode) return null;
        var token = JsonSerializer.Deserialize<TokenResponse>(await resp.Content.ReadAsStringAsync())!;
        var (sub, email, exp) = JwtClaims.ParseIdToken(token.IdToken);
        _current = new AuthSession(
            IdToken: token.IdToken,
            AccessToken: token.AccessToken,
            RefreshToken: rt,  // refresh_token は rotate されない限り維持
            Sub: sub, Email: email,
            ExpiresAt: DateTimeOffset.FromUnixTimeSeconds(exp));
        await _store.SaveAsync(_current);
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
        return _current;
    }

    public async Task SignOutAsync()
    {
        var cfg = AppSettings.Cognito;
        await _store.ClearAsync();
        _current = null;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
        // Cognito Hosted UI ログアウト (ブラウザ)
        var logoutUrl = $"{cfg.HostedUiBaseUrl}/logout?client_id={Uri.EscapeDataString(cfg.ClientId)}&logout_uri={Uri.EscapeDataString(cfg.CallbackUrl)}";
        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(logoutUrl));
    }

    public void RegisterPendingFlow(TaskCompletionSource<AuthResult> tcs, string state, string verifier, string redirectUri)
        => _pending = (state, verifier, redirectUri, tcs);

    private sealed record TokenResponse(
        [property: JsonPropertyName("id_token")] string IdToken,
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);
}

public sealed record HostedUiStart(string AuthUrl, string State, string RedirectUri);
public sealed record AuthResult(bool Success, string? ErrorCode = null);
```

### 3.8 `Services/EventDrivenPoller.cs`

```csharp
namespace TastileDesktop.Services;

public sealed class EventDrivenPoller : IDisposable
{
    private readonly CoreApiClient _api;
    private readonly DispatcherQueue _dispatcher;
    private readonly int _idleSeconds;
    private DispatcherQueueTimer? _idleTimer;
    private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;
    private DateTimeOffset _lastUserInitiated = DateTimeOffset.UtcNow;

    public event EventHandler<ExecutionView?>? ExecutionViewChanged;
    public event EventHandler<TilesResponse?>? TilesChanged;
    public event EventHandler<PendingPromptResponse?>? PendingPromptChanged;
    public event EventHandler<TimelineTodayResponse?>? TimelineChanged;
    public event EventHandler<bool>? ConnectionStatusChanged;

    public EventDrivenPoller(CoreApiClient api, DispatcherQueue dispatcher)
    {
        _api = api;
        _dispatcher = dispatcher;
        _idleSeconds = AppSettings.PollIdleSeconds;
    }

    public void Start()
    {
        if (_idleSeconds > 0 && _idleTimer == null)
        {
            _idleTimer = _dispatcher.CreateTimer();
            _idleTimer.Interval = TimeSpan.FromSeconds(_idleSeconds);
            _idleTimer.Tick += (_, _) => _ = RefreshAsync(userInitiated: false);
            _idleTimer.Start();
        }
    }

    public async Task RefreshAsync(bool userInitiated = true)
    {
        if (userInitiated) _lastUserInitiated = DateTimeOffset.UtcNow;
        try
        {
            // 4 エンドポイントを並列 fetch
            var evTask = _api.GetExecutionViewAsync();
            var tilesTask = _api.GetTilesAsync();
            var promptTask = _api.GetPendingPromptAsync();
            var timelineTask = _api.GetTodayTimelineAsync();
            await Task.WhenAll(evTask, tilesTask, promptTask, timelineTask);

            _dispatcher.TryEnqueue(() =>
            {
                ExecutionViewChanged?.Invoke(this, evTask.Result);
                TilesChanged?.Invoke(this, tilesTask.Result);
                PendingPromptChanged?.Invoke(this, promptTask.Result);
                TimelineChanged?.Invoke(this, timelineTask.Result);
                ConnectionStatusChanged?.Invoke(this, true);
            });
            _lastRefresh = DateTimeOffset.UtcNow;
        }
        catch
        {
            _dispatcher.TryEnqueue(() => ConnectionStatusChanged?.Invoke(this, false));
        }
    }

    public void Dispose() => _idleTimer?.Stop();
}
```

### 3.9 `Services/CoreApiClient.cs` (新コンストラクタ)

```csharp
public CoreApiClient(
    string? baseUrl = null,
    Func<Task<string?>>? getAccessToken = null,
    CognitoAuthService? cognito = null)
{
    _baseAddress = new Uri((baseUrl ?? AppSettings.ApiBaseUrl).TrimEnd('/') + "/");
    _getAccessToken = getAccessToken;
    _cognito = cognito;
    _httpClient = new HttpClient { BaseAddress = _baseAddress, Timeout = TimeSpan.FromSeconds(10) };
    _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TastileDesktop/0.3");
}

private async Task<HttpResponseMessage> SendWithAuthAsync(HttpRequestMessage req, CancellationToken ct)
{
    var token = _getAccessToken?.Invoke() is { } t1 ? await t1 : null;
    if (!string.IsNullOrEmpty(token))
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var resp = await _httpClient.SendAsync(req, ct);
    if (resp.StatusCode != HttpStatusCode.Unauthorized) return resp;

    // 401 → refresh → retry once
    if (_cognito is not null && await _cognito.RefreshAsync() is { } newSession)
    {
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newSession.IdToken);
        resp = await _httpClient.SendAsync(req, ct);
    }
    return resp;
}
```

---

## 4. 修正ファイルの詳細

### 4.1 `Services/AuthService.cs` (ファサード化)

```csharp
public sealed class AuthService
{
    public static AuthService Instance { get; } = new();
    private CognitoAuthService Inner => CognitoAuthService.Instance;

    public bool IsAuthenticated => Inner.IsAuthenticated;
    public AuthSession? CurrentSession => Inner.CurrentSession;
    public string? UserEmail => Inner.CurrentSession?.Email;
    public string? UserId => Inner.CurrentSession?.Sub;

    public event EventHandler? AuthStateChanged
    {
        add => Inner.AuthStateChanged += value;
        remove => Inner.AuthStateChanged -= value;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (Inner.CurrentSession is { } s && s.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(60))
            return s.IdToken;
        var refreshed = await Inner.RefreshAsync();
        return refreshed?.IdToken;
    }

    public Task SignOutAsync() => Inner.SignOutAsync();
}
```

### 4.2 `App.xaml.cs` 起動フロー

```csharp
protected override async void OnLaunched(LaunchActivatedEventArgs args)
{
    try
    {
        if (!ProtocolHandler.IsProtocolRegistered()) ProtocolHandler.RegisterProtocol();

        var cmdArgs = Environment.GetCommandLineArgs();
        var oauthCallback = cmdArgs.FirstOrDefault(a => a.StartsWith("tastile://", StringComparison.OrdinalIgnoreCase));
        if (oauthCallback != null) await HandleOAuthCallbackAsync(oauthCallback);

        await CognitoAuthService.Instance.TryLoadFromStoreAsync();
        if (!CognitoAuthService.Instance.IsAuthenticated)
        {
            var authWindow = new AuthWindow();
            authWindow.Activate();
            var tcs = new TaskCompletionSource<AuthResult>();
            CognitoAuthService.Instance.AuthStateChanged += (_, _) =>
            {
                if (CognitoAuthService.Instance.IsAuthenticated) tcs.TrySetResult(new AuthResult(true));
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            cts.Token.Register(() => tcs.TrySetResult(new AuthResult(false, "timeout")));
            var result = await tcs.Task;
            authWindow.Close();
            if (!result.Success) { Shutdown(); return; }
        }

        _api = new CoreApiClient(
            AppSettings.ApiBaseUrl,
            AuthService.Instance.GetAccessTokenAsync,
            CognitoAuthService.Instance);

        _mainWindow = new MainWindow();
        await _mainWindow.InitializeAsync(_api);

        _trayIconService = new TrayIconService(_mainWindow.ViewModel, _api, () => Shutdown(), _settingsService);
        _trayIconService.Initialize(_mainWindow);

        if (!cmdArgs.Contains("--minimized")) _mainWindow.ShowPanel();
    }
    catch (Exception ex) { Log.Fatal(ex, "OnLaunched failed"); throw; }
}

private async Task HandleOAuthCallbackAsync(string url)
{
    if (ProtocolHandler.ParseOAuthCallback(url) is not { } parsed) return;
    await CognitoAuthService.Instance.HandleAuthorizationCodeAsync(parsed.Code, parsed.State);
}
```

### 4.3 `MainWindow.xaml.cs` フォーカス時 refresh

```csharp
public MainWindow()
{
    this.InitializeComponent();
    this.Activated += OnWindowActivated;
}

private DateTimeOffset _lastFocusPoll = DateTimeOffset.MinValue;
private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
{
    if (e.WindowActivationState is not (WindowActivationState.PointerActivated or WindowActivationState.CodeActivated))
        return;
    if (DateTimeOffset.UtcNow - _lastFocusPoll < TimeSpan.FromSeconds(1)) return;  // debounce
    _lastFocusPoll = DateTimeOffset.UtcNow;
    _ = _viewModel?.RefreshAsync();
}
```

### 4.4 `ViewModels/MainViewModel.cs` の置換

**Before:**
```csharp
private readonly PollingService _polling;
// ...
_polling.TilesChanged += OnTilesChanged;
await _polling.PollAsync();
```

**After:**
```csharp
private readonly EventDrivenPoller _poller;
// ...
_poller.TilesChanged += OnTilesChanged;
await _poller.RefreshAsync();
```

コマンド後の処理:
```csharp
public async Task StartTileAsync(string? tileId)
{
    var resp = await _api.StartTileAsync(tileId!);
    if (resp?.Ok == true) await _poller.RefreshAsync();
}
```

### 4.5 `Views/AuthWindow.xaml.cs`

```csharp
public sealed partial class AuthWindow : Window
{
    public AuthWindow()
    {
        this.InitializeComponent();
        SignInButton.Click += async (_, _) =>
        {
            SignInButton.IsEnabled = false;
            StatusText.Text = "ブラウザでサインインを完了してください…";
            await CognitoAuthService.Instance.StartHostedUiAsync();
        };
    }
}
```

daemon flow ポーリング (`_api.GetOAuthFlowStatusAsync` ループ) を完全削除。

### 4.6 `TastileDesktop.csproj` 整理

**削除**:
```xml
<DaemonBinaryPath>..\..\..\tastile-core\target\$(DaemonRustTarget)\release\tastile-daemon.exe</DaemonBinaryPath>
<DaemonRustTarget>x86_64-pc-windows-msvc</DaemonRustTarget>

<Target Name="BuildBundledDaemon" ... />
<Target Name="BundleDaemonBinary" ... />
<Target Name="VerifyBundledDaemon" ... />
```

**残す**:
```xml
<TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
<UseWinUI>true</UseWinUI>
```

---

## 5. フェーズ別実装順序 (Migration Sequencing)

| Phase | 内容 | 検証 |
|---|---|---|
| **0: 土台** | `AppSettings.cs`, `CognitoConfig.cs`, `JwtClaims.cs`, `Pkce.cs`, `AuthSession.cs` 作成。`CoreApiClient.cs` に Bearer ヘッダ + 401 refresh コード追加 (既存メソッドは温存、URL はまだ `RuntimeProfile` fallback 読み) | 既存ビルドが通る (まだ旧 daemon 接続のまま) |
| **1: Cognito 認証** | `SecureTokenStore` / `ITokenStore` 作成 → `CognitoAuthService` 作成 → `AuthService` ファサード化 → `App.xaml.cs` 起動フロー再設計 → `AuthWindow.xaml.cs` 書き換え → `ProtocolHandler.cs` 簡素化 → `TrayIconService.SignInWithGoogleAsync` 簡素化 | **E2E**: prod Cognito で sign-in → token 保存 → アプリ再起動 → 自動ログイン |
| **2: 常時プロセス廃止** | `EventDrivenPoller.cs` 作成 → `MainViewModel` の `PollingService` 参照を全置換 → `PollingService.cs` / `PollingHealthCoordinator.cs` / `ITilesChangedSource.cs` を **ファイルごと削除** → `App.xaml.cs` から `TriggerTickAsync` 削除 → `MainWindow.xaml.cs` に `Activated` ハンドラ追加 | タスクマネージャで CPU 0% 維持。タイル作成 → 即座にリスト反映。1 分放置でタイマーが 1 回だけ発火 |
| **3: デーモン子プロセス廃止 + MSBuild 整理** | `DaemonManager.cs` / `DaemonCompatibility.cs` ファイル削除 → `App.xaml.cs` から `_daemonManager` 参照全削除 → `TastileDesktop.csproj` から daemon bundle Target 削除 | `tastile-daemon.exe` が成果物ディレクトリに存在しない |
| **4: 旧 API / ローカルデータ削除** | `CoreApiClient.cs` から `TriggerTickAsync` / `TriggerSyncAsync` / `ResetLocalSyncDataAsync` / `RedownloadRemoteSyncDataAsync` / `GetSyncStatusAsync` / `GetRuntimePathsAsync` / `StartOAuth*` / `SignInWithOAuth*` / `GetSessionAsync` / `RestoreSessionAsync` / `IsAuthenticatedAsync` を削除 → `RuntimeProfile.cs` 削除 → `PkceHelper.cs` / `OAuthCallbackHandoff.cs` / `LocalOAuthServer.cs` 削除 (Phase 1 で未参照になっているはず) | ビルド成功、未使用シンボルなし |
| **5: 統合テスト + リリース** | dev (docker) + prod 両方で E2E 検証 → ドキュメント更新 | 全フロー通過、受け入れ基準達成 |

各 Phase 完了時に **ビルド & 既存機能が壊れていないこと** を確認。revert 可能性を最大化。

---

## 6. 検証 (Verification)

### 6.1 ビルド検証 (各 Phase)
```bash
cd C:\Users\rebui\Desktop\tastile\tastile-desktop
dotnet build .\src\TastileDesktop\TastileDesktop.csproj -r win-x64 -c Debug
```
期待: 0 error、warning は既存分のみ。

### 6.2 E2E 検証

**Dev mode** (`TASTILE_API_BASE_URL=http://127.0.0.1:3140` + `TASTILE_BYPASS_AUTH=1` on daemon):
```bash
cd C:\Users\rebui\Desktop\tastile\tastile-core
docker-compose up -d tastile-core
cd C:\Users\rebui\Desktop\tastile\tastile-desktop
$env:TASTILE_API_BASE_URL="http://127.0.0.1:3140"
$env:TASTILE_BYPASS_AUTH="1"
dotnet run --project .\src\TastileDesktop\TastileDesktop.csproj
```

**Prod** (`TASTILE_API_BASE_URL` 未設定 → デフォルト `https://beta.tastile.app`):
```bash
dotnet run --project .\src\TastileDesktop\TastileDesktop.csproj
```

### 6.3 受け入れ基準

- [ ] すべての API 呼び出しが `Authorization: Bearer <id_token>` 付きで `https://beta.tastile.app` に行く
- [ ] `tastile-daemon.exe` が成果物ディレクトリに存在しない
- [ ] 1 秒 tick を送るコードがリポジトリ内に存在しない (`grep -r "TriggerTick\|/commands/tick"`)
- [ ] アイドル時 CPU 0%
- [ ] `DispatcherTimer` ベースの wall-clock loop が存在しない (新 `EventDrivenPoller` のアイドルタイマーは 1 個のみ)
- [ ] `%LOCALAPPDATA%\Tastile\` には `Auth\credentials.bin` のみ。タイル・状態・ログ JSON なし
- [ ] サインアウトで `credentials.bin` 削除
- [ ] `TastileDesktop.csproj` に `cargo build -p tastile-daemon` / SHA256 verify target が存在しない
- [ ] Cognito Hosted UI サインインが E2E で通る
- [ ] 1 時間アイドル後の再起動で自動ログイン

### 6.4 デバッグ手順

**API トレースを見たい時**:
```bash
# Fiddler / Charles などで https://beta.tastile.app への HTTPS 通信をプロキシ
# デスクトップから `HTTP_PROXY=http://localhost:8888` 設定で起動 (System.Net.Http は既定で proxy env を尊重)
```

**トークンの中身を見たい時**:
```bash
# id_token の claims をデコード
$idToken = "<your_id_token>"
$payload = $idToken.Split('.')[1]
[Text.Encoding]::Utf8.GetString([Convert]::FromBase64String($payload.Replace('-','+').Replace('_','/').PadRight(($payload.Length % 4),'='))) | ConvertFrom-Json | Format-List
```

**DPAPI 復号エラーの調査**:
```bash
# 別ユーザーで起動すると復号不可。Windows ユーザー切り替え検知には `SecureTokenStore.LoadAsync` が null を返す挙動でカバー。
```

---

## 7. リスクとオープン question

### 7.1 [HIGH] Cognito JWT 検証が daemon 側でデプロイ済みか
`tastile-core/docs/plans/2026-06-07-web-to-aws-daemon-real-path-design.md` には「Bearer JWT を受け入れる設計」と書かれているが、**実装がデプロイ済みかは要確認**。未デプロイなら全 API 呼び出しが 401 を返し refresh loop に陥る。

**対応**:
- Phase 0 で `curl https://beta.tastile.app/read/tiles` を手動で叩き、401/200 を確認
- もし 401 → core チームにデプロイ依頼 (ブロッカー)
- 一時回避: `TASTILE_DEV_BEARER_TOKEN` 環境変数が設定されていれば Cognito をスキップして直接 Bearer 注入できる dev モード (プロダクションビルドには含めない、`#if DEBUG` 配下のみ)

### 7.2 [HIGH] OAuth callback URL の Cognito User Pool App Client 登録
`ta5stile://auth/callback` を Cognito User Pool App Client の **Allowed callback URLs** に追加登録する必要がある (現状は `http://localhost:3000/auth/callback` のみのはず)。

**対応**:
- Phase 1 開始前に core チームに依頼
- 登録 URL: `tastile://auth/callback` (Cognito はカスタムスキームを受け入れる)

### 7.3 [MEDIUM] `tastile://` プロトコルの Windows ハンドリング
- unpackaged app (`<AppxPackage>false</AppxPackage>`) では Registry ベースの `ProtocolHandler.RegisterProtocol` が動くはず
- MSIX packaged ビルドでは `Package.appxmanifest` の `Protocol` extension 宣言が必要
- 両方で `tastile://` が Windows シェルに渡るよう、両方の経路をサポート

### 7.4 [MEDIUM] SSE の安定性 (有効化した場合)
- `/read/events/state` は `EventSource` でクエリパラメータ `?access_token=...` を使う (ヘッダを送れない)
- id_token が URL ログ (DevTools, プロキシ) に残るのは既知リスク
- デフォルト OFF。`TASTILE_ENABLE_SSE=1` 明示時のみ opt-in

### 7.5 [MEDIUM] `MainWindow.Activated` の発火頻度
WinUI 3 の `Window.Activated` は最小化→復元でも発火するため、debounce (1s) とアイドルタイマーの組み合わせで過剰ポーリングを防ぐ。タスクマネージャで実測しながら調整。

### 7.6 [LOW] DPAPI のユーザー切り替え
DPAPI は CurrentUser スコープ。同一 Windows ユーザーであれば復号可、切り替え時は不可。`SecureTokenStore.LoadAsync` が null を返したら「再認証が必要です」を UI で通知。

### 7.7 [LOW] `RuntimeProfile.cs` 削除の影響
`TastileSettings.UpdateManifestUrl` の `RuntimeProfile.ResolveEnvironmentValue("TASTILE_UPDATE_URL")` を `AppSettings` または素の `Environment.GetEnvironmentVariable` 呼び出しに置換。`GetAppDataDirectory` / `GetLocalAppDataDirectory` の呼び出し箇所 (`SettingsService` / `App.DebugLogPath`) は `Environment.SpecialFolder.LocalApplicationData + "\Tastile"` ハードコードに置換。

---

## 8. Critical Files (実装で触る重要ファイル)

| ファイル | 役割 | Phase |
|---|---|---|
| `src/TastileDesktop/Services/CoreApiClient.cs` | Bearer ヘッダ注入 + 401 refresh の中核 | 0, 4 |
| `src/TastileDesktop/Services/CognitoAuthService.cs` (新規) | Cognito PKCE / refresh / signout | 1 |
| `src/TastileDesktop/Services/SecureTokenStore.cs` (新規) | DPAPI トークン保存 | 1 |
| `src/TastileDesktop/Services/AuthService.cs` | Cognito ファサード化 | 1 |
| `src/TastileDesktop/Services/EventDrivenPoller.cs` (新規) | イベント駆動 refresh | 2 |
| `src/TastileDesktop/ViewModels/MainViewModel.cs` | PollingService → EventDrivenPoller 置換 | 2 |
| `src/TastileDesktop/App.xaml.cs` | 起動フロー再設計 + DaemonManager 削除 | 1, 2, 3 |
| `src/TastileDesktop/MainWindow.xaml.cs` | Activated → RefreshAsync | 2 |
| `src/TastileDesktop/Views/AuthWindow.xaml.cs` | Cognito Hosted UI フロー | 1 |
| `src/TastileDesktop/ProtocolHandler.cs` | `tastile://` 簡素化 | 1 |
| `src/TastileDesktop/Services/TrayIconService.cs` | SignInWithGoogleAsync 簡素化 | 1 |
| `TastileDesktop.csproj` | daemon bundle Target 削除 | 3 |

**削除ファイル (参照のみ)**: `DaemonManager.cs`, `DaemonCompatibility.cs`, `PollingService.cs`, `PollingHealthCoordinator.cs`, `LocalOAuthServer.cs`, `OAuthCallbackHandoff.cs`, `PkceHelper.cs`, `RuntimeProfile.cs`, `ITilesChangedSource.cs`

**tastile-web 参考ファイル (C# 移植元)**:
- `tastile-web/src/lib/daemon/client.ts` → `CoreApiClient.cs`
- `tastile-web/src/lib/cognito/server.ts` → `CognitoAuthService.cs` のトークン交換/リフレッシュ部分
- `tastile-web/src/lib/cognito/env.ts` → `CognitoConfig.cs`
- `tastile-web/src/app/auth/cognito/login/route.ts` → `CognitoAuthService.StartHostedUiAsync()`
- `tastile-web/src/app/auth/callback/route.ts` → `CognitoAuthService.HandleAuthorizationCodeAsync()`

---

## 9. 補足: なぜこの順序か

1. **Phase 0 → 1** は「既存機能を一切壊さず Cognito 認証を追加」する準備。`AuthSession` を別名前空間に隔離することで衝突回避。
2. **Phase 2 (常時プロセス廃止)** を Phase 3 (子プロセス削除) より先にしたのは、`EventDrivenPoller` の動作を **まだローカルデーモンが生きている状態** で検証できるため、問題切り分けが楽。
3. **Phase 4 (旧 API 削除)** を最後に回したのは、`CoreApiClient` の API 表面を段階的に削る方が revert しやすく安全。Phase 0 で Bearer ヘッダを「追加」、Phase 4 で不要メソッドを「削除」。
4. **Phase 5 (統合テスト)** はリスク 7.1 (Cognito JWT 検証デプロイ) のブロッカー次第。解消されない限り prod サインインは通らない。
