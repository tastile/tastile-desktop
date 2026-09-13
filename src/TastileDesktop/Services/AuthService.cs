using System;
using System.Threading.Tasks;

namespace TastileDesktop.Services;

/// <summary>
/// Thin facade over <see cref="BetterAuthAuthService"/>. Existing call sites
/// (TrayIconService, MainViewModel, App) keep the synchronous
/// <c>IsAuthenticated</c> / <c>UserEmail</c> shape; the underlying
/// implementation is async because sign-in / sign-out / token minting are
/// async HTTP calls against BetterAuth.
/// </summary>
public sealed class AuthService
{
    public static AuthService Instance { get; } = new();

    private BetterAuthAuthService Inner => BetterAuthAuthService.Instance;

    public bool IsAuthenticated => Inner.IsAuthenticated;
    public TastileDesktop.Models.AuthSession? CurrentSession => Inner.CurrentSession;
    public string? UserEmail => Inner.CurrentSession?.Email;
    public string? UserId => Inner.CurrentSession?.UserId;

    public event EventHandler? AuthStateChanged
    {
        add => Inner.AuthStateChanged += value;
        remove => Inner.AuthStateChanged -= value;
    }

    /// <summary>
    /// Returns the BetterAuth session token to use as a Bearer on the v1 API.
    /// BetterAuth sessions are long-lived so no rotation is performed here;
    /// the value is the same one stored in DPAPI by the last sign-in.
    /// </summary>
    public Task<string?> GetAccessTokenAsync() => Inner.GetAccessTokenAsync();

    public Task SignOutAsync() => Inner.SignOutAsync();
}
