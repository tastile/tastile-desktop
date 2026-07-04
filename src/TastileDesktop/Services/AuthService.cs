using System;
using System.Threading.Tasks;

namespace TastileDesktop.Services;

/// <summary>
/// Thin facade over <see cref="CognitoAuthService"/>. Existing call sites
/// (TrayIconService, MainViewModel, App) keep the synchronous
/// <c>IsAuthenticated</c> / <c>UserEmail</c> shape; the underlying
/// implementation is async because Cognito refresh is async.
/// </summary>
public sealed class AuthService
{
    public static AuthService Instance { get; } = new();

    private CognitoAuthService Inner => CognitoAuthService.Instance;

    public bool IsAuthenticated => Inner.IsAuthenticated;
    public TastileDesktop.Models.AuthSession? CurrentSession => Inner.CurrentSession;
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
        {
            return s.AccessToken;
        }
        var refreshed = await Inner.RefreshAsync();
        return refreshed?.AccessToken;
    }

    public Task SignOutAsync() => Inner.SignOutAsync();
}
