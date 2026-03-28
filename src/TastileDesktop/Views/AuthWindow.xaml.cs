using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using TastileDesktop.Services;

namespace TastileDesktop.Views;

/// <summary>
/// OAuth authentication window - Daemon-mediated flow.
/// Desktop asks daemon to open browser and handle OAuth callback.
/// </summary>
public sealed partial class AuthWindow : Window
{
    private readonly CoreApiClient _api;
    private readonly TaskCompletionSource<AuthResult> _tcs = new();
    private string? _authUrl;
    private string? _expectedState;

    public Task<AuthResult> AuthResultTask => _tcs.Task;

    public AuthWindow(CoreApiClient api)
    {
        Log("[AuthWindow.ctor] === START ===");
        
        try
        {
            Log("[AuthWindow.ctor] Calling InitializeComponent...");
            this.InitializeComponent();
            Log("[AuthWindow.ctor] InitializeComponent completed");
            
            _api = api;
            Log("[AuthWindow.ctor] CoreApiClient assigned");

            Log("[AuthWindow.ctor] Setting floating window chrome...");
            FloatingWindowHelper.Configure(this, TitleBarArea, 400, 300);
            Log("[AuthWindow.ctor] Window chrome set");
            this.Closed += OnWindowClosed;

            // Start daemon-mediated authentication flow
            Log("[AuthWindow.ctor] Starting StartAuthenticationAsync...");
            _ = StartAuthenticationAsync();
            Log("[AuthWindow.ctor] StartAuthenticationAsync dispatched");
        }
        catch (Exception ex)
        {
            Log($"[AuthWindow.ctor] ERROR: {ex.GetType().Name}: {ex.Message}");
            Log($"[AuthWindow.ctor] StackTrace: {ex.StackTrace}");
            throw;
        }
        
        Log("[AuthWindow.ctor] === END ===");
    }

    private async Task StartAuthenticationAsync()
    {
        Log("[StartAuthenticationAsync] === START ===");

        // Write a marker file to confirm this method is called
        try
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tastile-auth-started.txt"),
                $"StartAuthenticationAsync called at {DateTime.Now}");
        }
        catch { }

        try
        {
            // Step 1: Ask daemon to start OAuth flow
            Log("[StartAuthenticationAsync] Step 1: Calling _api.StartBrowserAuthAsync('google')...");

            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    StatusTextBlock.Text = "Opening browser for authentication...";
                    Log("[StartAuthenticationAsync] StatusTextBlock updated");
                }
                catch (Exception ex)
                {
                    Log($"[StartAuthenticationAsync] ERROR updating status: {ex.Message}");
                }
            });

            Log("[StartAuthenticationAsync] About to call StartBrowserAuthAsync...");
            string? authUrl;
            try
            {
                authUrl = await _api.StartBrowserAuthAsync("google");
                _authUrl = authUrl;
                _expectedState = OAuthCallbackHandoff.ExtractStateFromAuthUrl(authUrl);
                OAuthCallbackHandoff.StoreExpectedState(_expectedState);
                Log($"[StartAuthenticationAsync] StartBrowserAuthAsync returned URL: {authUrl}");
            }
            catch (Exception ex)
            {
                Log($"[StartAuthenticationAsync] ERROR in StartBrowserAuthAsync: {ex.GetType().Name}: {ex.Message}");
                Log($"[StartAuthenticationAsync] StackTrace: {ex.StackTrace}");
                _tcs.SetResult(new AuthResult
                {
                    Success = false,
                    Error = $"Failed to call daemon: {ex.Message}"
                });
                return;
            }

            if (string.IsNullOrEmpty(authUrl))
            {
                Log("[StartAuthenticationAsync] StartBrowserAuthAsync returned null or empty URL");
                _tcs.SetResult(new AuthResult
                {
                    Success = false,
                    Error = "Failed to start authentication. Daemon returned no URL."
                });
                ShowError("Failed to start authentication.");
                return;
            }

            // Open browser with the auth URL (Desktop App opens browser, not daemon)
            Log($"[StartAuthenticationAsync] Opening browser with URL: {authUrl}");
            try
            {
                OpenBrowser(authUrl);
                Log("[StartAuthenticationAsync] Browser opened successfully");
            }
            catch (Exception ex)
            {
                Log($"[StartAuthenticationAsync] ERROR opening browser: {ex.Message}");
                // Continue anyway - user might open browser manually
            }

            Log("[StartAuthenticationAsync] Browser auth started successfully");

            // Step 2: Poll daemon for authentication completion
            Log("[StartAuthenticationAsync] Step 2: Starting polling for authentication...");
            
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    StatusTextBlock.Text = "Complete sign-in in your browser...";
                    Log("[StartAuthenticationAsync] StatusTextBlock updated to polling message");
                }
                catch (Exception ex)
                {
                    Log($"[StartAuthenticationAsync] ERROR updating polling status: {ex.Message}");
                }
            });
            
            Log("[StartAuthenticationAsync] Starting PollForAuthenticationAsync...");
            var authenticated = await PollForAuthenticationAsync(
                timeout: TimeSpan.FromMinutes(5),
                pollInterval: TimeSpan.FromSeconds(2)
            );
            Log($"[StartAuthenticationAsync] PollForAuthenticationAsync returned: {authenticated}");

            if (authenticated)
            {
                Log("[StartAuthenticationAsync] Authentication successful!");
                await AuthService.Instance.RefreshSessionFromDaemonAsync(_api);
                _tcs.TrySetResult(new AuthResult 
                { 
                    Success = true 
                });
                Log("[StartAuthenticationAsync] Calling Close()...");
                DispatcherQueue.TryEnqueue(() => this.Close());
            }
            else
            {
                Log("[StartAuthenticationAsync] Authentication timed out");
                _tcs.TrySetResult(new AuthResult 
                { 
                    Success = false, 
                    Error = "Authentication timed out. Please try again." 
                });
                ShowError("Authentication timed out. Please try again.");
            }
        }
        catch (Exception ex)
        {
            Log($"[StartAuthenticationAsync] UNEXPECTED ERROR: {ex.GetType().Name}: {ex.Message}");
            Log($"[StartAuthenticationAsync] StackTrace: {ex.StackTrace}");
            _tcs.TrySetResult(new AuthResult 
            { 
                Success = false, 
                Error = ex.Message 
            });
            ShowError($"Authentication error: {ex.Message}");
        }
        
        Log("[StartAuthenticationAsync] === END ===");
    }

    private async Task<bool> PollForAuthenticationAsync(TimeSpan timeout, TimeSpan pollInterval)
    {
        Log($"[PollForAuthenticationAsync] Starting poll: timeout={timeout}, interval={pollInterval}");
        var startTime = DateTime.UtcNow;
        int attemptCount = 0;

        while (DateTime.UtcNow - startTime < timeout)
        {
            attemptCount++;
            if (attemptCount <= 5 || attemptCount % 10 == 0)
            {
                Log($"[PollForAuthenticationAsync] Poll attempt {attemptCount}...");
            }

            try
            {
                var callbackUrl = OAuthCallbackHandoff.Peek();
                if (!string.IsNullOrWhiteSpace(callbackUrl))
                {
                    Log("[PollForAuthenticationAsync] Found callback handoff; exchanging code via daemon");
                    var parsed = ProtocolHandler.ParseOAuthCallback(callbackUrl);
                    if (parsed != null)
                    {
                        var (code, state) = parsed.Value;
                        if (!OAuthCallbackHandoff.MatchesExpectedState(state))
                        {
                            Log("[PollForAuthenticationAsync] Ignoring callback handoff because OAuth state did not match.");
                            continue;
                        }

                        var exchanged = await _api.SignInWithOAuthAsync("google", code, "tastile://auth/callback");
                        if (!string.IsNullOrWhiteSpace(exchanged?.AccessToken))
                        {
                            OAuthCallbackHandoff.ClearCallback();
                            OAuthCallbackHandoff.ClearExpectedState();
                            Log("[PollForAuthenticationAsync] Callback handoff exchange succeeded");
                            return true;
                        }
                    }
                }

                Log($"[PollForAuthenticationAsync] About to call IsAuthenticatedAsync...");
                var isAuthenticated = await _api.IsAuthenticatedAsync();
                Log($"[PollForAuthenticationAsync] IsAuthenticatedAsync returned: {isAuthenticated}");

                if (isAuthenticated)
                {
                    Log($"[PollForAuthenticationAsync] Authenticated after {attemptCount} attempts!");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"[PollForAuthenticationAsync] Poll error (attempt {attemptCount}): {ex.GetType().Name}: {ex.Message}");
                Log($"[PollForAuthenticationAsync] StackTrace: {ex.StackTrace}");
            }

            await Task.Delay(pollInterval);
        }

        Log($"[PollForAuthenticationAsync] Timeout after {attemptCount} attempts");
        return false;
    }

    private void ShowError(string message)
    {
        Log($"[ShowError] {message}");
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                StatusTextBlock.Text = $"Error: {message}";
                StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    ThemeManager.GetColor("AppPrimaryBrush"));
            }
            catch (Exception ex)
            {
                Log($"[ShowError] ERROR updating UI: {ex.Message}");
            }
        });
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Log("[OnCancelClick] User cancelled authentication");
        if (!_tcs.Task.IsCompleted)
        {
            OAuthCallbackHandoff.ClearCallback();
            OAuthCallbackHandoff.ClearExpectedState();
            _tcs.TrySetResult(new AuthResult 
            { 
                Success = false, 
                Error = "User cancelled" 
            });
        }
        this.Close();
    }

    private void OnOpenBrowserClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_authUrl))
        {
            ShowError("Authentication URL is not ready yet.");
            return;
        }

        try
        {
            OpenBrowser(_authUrl);
            StatusTextBlock.Text = "Browser reopened. Complete Google sign-in there.";
        }
        catch (Exception ex)
        {
            ShowError($"Failed to open browser: {ex.Message}");
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (!_tcs.Task.IsCompleted)
        {
            OAuthCallbackHandoff.ClearCallback();
            OAuthCallbackHandoff.ClearExpectedState();
            _tcs.TrySetResult(new AuthResult
            {
                Success = false,
                Error = "Authentication window closed"
            });
        }
    }
    
    private void Log(string message)
    {
        var path = Path.Combine(Path.GetTempPath(), "tastile-desktop-debug.log");
        File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        Debug.WriteLine(message);
    }

    private static void OpenBrowser(string url)
    {
        var psi = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };
        Process.Start(psi);
    }
}
