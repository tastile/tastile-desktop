using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using TastileDesktop.Resources;
using TastileDesktop.Services;

namespace TastileDesktop.Views;

/// <summary>
/// Native BetterAuth sign-in window. The user enters their email + password
/// here; on submit we POST to <c>/api/auth/sign-in/email</c> on the web
/// backend (no embedded WebView and no OAuth callback handler). Social sign-in
/// stays hidden until an installed-app handoff can persist a desktop session.
/// </summary>
public sealed partial class AuthWindow : Window
{
    private EventHandler? _authStateChangedHandler;
    private bool _busy;
    private CancellationTokenSource? _signInCts;

    public AuthWindow()
    {
        this.InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 460, 540);
        SignInButton.Click += OnSignInClick;
        CancelButton.Click += OnCancelClick;

        _authStateChangedHandler = (_, _) =>
        {
            // AuthStateChanged can fire after Cancel if the in-flight sign-in
            // completed in the same instant we were cancelling. Only auto-close
            // when the user has not already cancelled.
            if (BetterAuthAuthService.Instance.IsAuthenticated
                && (_signInCts is null || !_signInCts.IsCancellationRequested))
            {
                DispatcherQueue.TryEnqueue(() => this.Close());
            }
        };
        BetterAuthAuthService.Instance.AuthStateChanged += _authStateChangedHandler;
        Closed += OnWindowClosed;

        // Enable inputs once the window is fully loaded.
        Activated += OnActivated;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (EmailTextBox.IsEnabled)
        {
            return;
        }

        EmailTextBox.IsEnabled = true;
        PasswordInput.IsEnabled = true;
        SignInButton.IsEnabled = true;
        EmailTextBox.Focus(FocusState.Programmatic);
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (_authStateChangedHandler is not null)
        {
            BetterAuthAuthService.Instance.AuthStateChanged -= _authStateChangedHandler;
            _authStateChangedHandler = null;
        }
        // Cancel any in-flight sign-in so the window-close path can never leave
        // the process authenticated against the user's stated intent.
        try
        {
            _signInCts?.Cancel();
            _signInCts?.Dispose();
            _signInCts = null;
        }
        catch (ObjectDisposedException)
        {
            // Already disposed by OnCancelClick — safe to ignore.
        }
    }

    private async void OnSignInClick(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        var email = EmailTextBox.Text?.Trim();
        var password = PasswordInput.Password;
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowError(Strings.Get("Auth_MissingFields"));
            return;
        }

        await RunSignInAsync(ct => BetterAuthAuthService.Instance.SignInWithEmailAsync(email, password, ct));
    }

    private async void OnCreateAccountClick(object sender, RoutedEventArgs e)
    {
        OpenExternalUrl($"{AppSettings.WebBaseUrl}/auth/signup");
        await Task.CompletedTask;
    }

    private async Task RunSignInAsync(Func<CancellationToken, Task<AuthResult>> operation)
    {
        // Each attempt gets its own CTS so a previous cancellation does not
        // poison the next sign-in.
        var cts = new CancellationTokenSource();
        var previousCts = Interlocked.Exchange(ref _signInCts, cts);
        previousCts?.Cancel();
        previousCts?.Dispose();

        SetBusy(true);
        StatusTextBlock.Text = Strings.Get("Auth_SignInInProgress");
        try
        {
            var result = await operation(cts.Token);
            if (cts.IsCancellationRequested)
            {
                // Window closed (or Cancel pressed) while the call was in
                // flight. BetterAuthAuthService did not persist the session
                // because we check the token before _current assignment, so
                // there is nothing to roll back here. Just exit cleanly.
                return;
            }
            if (!result.Success)
            {
                var message = !string.IsNullOrEmpty(result.Detail)
                    ? $"{result.ErrorCode}: {result.Detail}"
                    : (result.ErrorCode ?? "unknown");
                ShowError(string.Format(Strings.Get("Auth_SignInFailed"), message));
                return;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // User cancelled: do not surface this as an error.
            return;
        }
        catch (Exception ex)
        {
            ShowError(string.Format(Strings.Get("Auth_SignInFailed"), ex.Message));
        }
        finally
        {
            // Only clear busy state if this CTS is still the active one —
            // a newer sign-in attempt must not be demoted by a stale call.
            if (Interlocked.CompareExchange(ref _signInCts, null, cts) == cts)
            {
                cts.Dispose();
                SetBusy(false);
            }
            else
            {
                cts.Dispose();
            }
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        SignInButton.IsEnabled = !busy;
        EmailTextBox.IsEnabled = !busy;
        PasswordInput.IsEnabled = !busy;
        CancelButton.IsEnabled = !busy;
        BusyRing.IsActive = busy;
    }

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            OnSignInClick(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        // Signal cancellation to the in-flight sign-in first, then close the
        // window. Closing triggers OnWindowClosed which also cancels as a
        // belt-and-braces measure.
        try
        {
            _signInCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed — closing is still safe.
        }
        this.Close();
    }

    private void ShowError(string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                ThemeManager.GetColor("OverrideAccentFillBrush"));
        });
    }

    private static void OpenExternalUrl(string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }
}
