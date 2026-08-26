using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using TastileDesktop.Resources;
using TastileDesktop.Services;

namespace TastileDesktop.Views;

/// <summary>
/// Native BetterAuth sign-in window. The user enters their email + password
/// here; on submit we POST to <c>/api/auth/sign-in/email</c> on the web
/// backend (no embedded WebView, no OAuth callback handler). Social sign-in
/// (Google/Apple) still opens the system browser to the web sign-in page
/// because the redirect-to-desktop follow-up is owned by tastile-web and is
/// not implemented yet (see ADR 2026-08-22).
/// </summary>
public sealed partial class AuthWindow : Window
{
    private EventHandler? _authStateChangedHandler;
    private bool _busy;

    public AuthWindow()
    {
        this.InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 460, 540);
        SignInButton.Click += OnSignInClick;
        CancelButton.Click += OnCancelClick;

        _authStateChangedHandler = (_, _) =>
        {
            if (BetterAuthAuthService.Instance.IsAuthenticated)
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

        await RunSignInAsync(() => BetterAuthAuthService.Instance.SignInWithEmailAsync(email, password));
    }

    private async void OnCreateAccountClick(object sender, RoutedEventArgs e)
    {
        OpenExternalUrl($"{AppSettings.WebBaseUrl}/auth/signup");
        await System.Threading.Tasks.Task.CompletedTask;
    }

    private async void OnSocialSignInClick(object sender, RoutedEventArgs e)
    {
        // MVP social fallback: hand off to the web sign-in surface so the
        // existing provider-based sign-in keeps working. The follow-up that
        // routes the success back to the desktop client (via the tastile://
        // protocol or an installed-app handoff) lives in tastile-web and is
        // tracked under the same ADR-2026-08-22 work item.
        OpenExternalUrl($"{AppSettings.WebBaseUrl}/login?provider=google");
        await System.Threading.Tasks.Task.CompletedTask;
    }

    private async System.Threading.Tasks.Task RunSignInAsync(Func<System.Threading.Tasks.Task<AuthResult>> operation)
    {
        SetBusy(true);
        StatusTextBlock.Text = Strings.Get("Auth_SignInInProgress");
        try
        {
            var result = await operation();
            if (!result.Success)
            {
                var message = !string.IsNullOrEmpty(result.Detail)
                    ? $"{result.ErrorCode}: {result.Detail}"
                    : (result.ErrorCode ?? "unknown");
                ShowError(string.Format(Strings.Get("Auth_SignInFailed"), message));
                return;
            }
        }
        catch (Exception ex)
        {
            ShowError(string.Format(Strings.Get("Auth_SignInFailed"), ex.Message));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        SignInButton.IsEnabled = !busy;
        EmailTextBox.IsEnabled = !busy;
        PasswordInput.IsEnabled = !busy;
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
