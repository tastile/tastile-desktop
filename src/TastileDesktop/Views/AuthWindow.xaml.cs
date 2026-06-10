using Microsoft.UI.Xaml;
using System;
using TastileDesktop.Services;

namespace TastileDesktop.Views;

/// <summary>
/// Hosted UI sign-in window. Opens the system browser pointed at the
/// Cognito Hosted UI URL and auto-closes on successful sign-in.
/// </summary>
public sealed partial class AuthWindow : Window
{
    private EventHandler? _authStateChangedHandler;

    public AuthWindow()
    {
        this.InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 400, 300);
        SignInButton.Click += OnSignInClick;
        CancelButton.Click += OnCancelClick;

        _authStateChangedHandler = (_, _) =>
        {
            if (CognitoAuthService.Instance.IsAuthenticated)
            {
                DispatcherQueue.TryEnqueue(() => this.Close());
            }
        };
        CognitoAuthService.Instance.AuthStateChanged += _authStateChangedHandler;
        Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (_authStateChangedHandler is not null)
        {
            CognitoAuthService.Instance.AuthStateChanged -= _authStateChangedHandler;
            _authStateChangedHandler = null;
        }
    }

    private async void OnSignInClick(object sender, RoutedEventArgs e)
    {
        SignInButton.IsEnabled = false;
        StatusTextBlock.Text = "ブラウザでサインインを完了してください…";

        try
        {
            await CognitoAuthService.Instance.StartHostedUiAsync();
        }
        catch (Exception ex)
        {
            ShowError($"サインインを開始できませんでした: {ex.Message}");
            SignInButton.IsEnabled = true;
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
                ThemeManager.GetColor("AppPrimaryBrush"));
        });
    }
}
