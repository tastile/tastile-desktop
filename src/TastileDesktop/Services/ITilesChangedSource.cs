using TastileDesktop.Models;

namespace TastileDesktop.Services;

public interface ITilesChangedSource
{
    event EventHandler<TilesResponse?> TilesChanged;
}
