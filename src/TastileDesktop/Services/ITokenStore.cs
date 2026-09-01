using System.Threading.Tasks;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// Persistence boundary for the active Cognito
/// <see cref="TastileDesktop.Models.AuthSession"/>.
/// Implementations must guarantee the persisted bytes are unreadable to
/// other Windows users (DPAPI CurrentUser scope is the desktop's contract).
/// </summary>
public interface ITokenStore
{
    Task<TastileDesktop.Models.AuthSession?> LoadAsync();
    Task SaveAsync(TastileDesktop.Models.AuthSession session);
    Task ClearAsync();
}
