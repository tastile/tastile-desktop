using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading.Tasks;
using TastileDesktop.Services;

namespace TastileDesktop.Views;

/// <summary>
/// Login window - authenticates via tastile-core daemon.
/// </summary>
public sealed partial class LoginWindow : Window
{
    private readonly CoreApiClient _api;

    public LoginWindow(CoreApiClient api)
    {
        this.InitializeComponent();
        _api = api;
        FloatingWindowHelper.Configure(this, TitleBarArea, 480, 600);
    }

    private async void OnSignInClick(object sender, RoutedEventArgs e)
    {
        await SignInAsync();
    }

    private async void OnSignUpClick(object sender, RoutedEventArgs e)
    {
        await SignUpAsync();
    }

    private void OnEmailKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            PasswordBox.Focus(FocusState.Programmatic);
        }
    }

    private void OnPasswordKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            _ = SignInAsync();
        }
    }

    private async Task SignInAsync()
    {
        var email = EmailTextBox.Text?.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(email))
        {
            ShowError("Please enter your email address.");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Please enter your password.");
            return;
        }

        ShowLoading(true);

        try
        {
            var success = await AuthService.Instance.SignInAsync(_api, email, password);
            
            if (success)
            {
                this.Close();
            }
            else
            {
                ShowError("Invalid email or password.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Sign in failed: {ex.Message}");
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private async Task SignUpAsync()
    {
        var email = EmailTextBox.Text?.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(email))
        {
            ShowError("Please enter your email address.");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Please enter a password.");
            return;
        }

        if (password.Length < 6)
        {
            ShowError("Password must be at least 6 characters.");
            return;
        }

        ShowLoading(true);

        try
        {
            var success = await AuthService.Instance.SignUpAsync(_api, email, password);
            
            if (success)
            {
                this.Close();
            }
            else
            {
                ShowError("Failed to create account.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Sign up failed: {ex.Message}");
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorBorder.Visibility = Visibility.Visible;
    }

    private void ShowLoading(bool show)
    {
        LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show) ErrorBorder.Visibility = Visibility.Collapsed;
    }
}
