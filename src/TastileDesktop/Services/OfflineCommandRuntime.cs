using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// Process-wide composition root for the offline sync stack
/// (NF04-D02/D03/D05/D06): one CoreApiClient, one local read model,
/// one sync driver, one command outbox, and the SSE stream. Windows
/// that need to submit commands (MainWindow view model, TilesWindow)
/// share this instance so a single outbox ordering and a single SSE
/// connection are preserved.
/// </summary>
public sealed class OfflineCommandRuntime : IDisposable
{
    private static readonly Lazy<OfflineCommandRuntime> SharedInstance = new(() => new OfflineCommandRuntime());

    public static OfflineCommandRuntime Shared => SharedInstance.Value;

    public CoreApiClient Api { get; }
    public LocalReadModelStore Store { get; }
    public LocalSyncService Sync { get; }
    public CommandOutbox Outbox { get; }
    public SyncCommandService Commands { get; }
    public LocalExecutionProjector Executions { get; }

    private SyncStreamService? _stream;
    private readonly object _streamGate = new();
    private bool _disposed;

    private OfflineCommandRuntime()
    {
        Api = new CoreApiClient(
            getAccessToken: AuthService.Instance.GetAccessTokenAsync,
            refreshTokens: BetterAuthAuthService.Instance.RefreshAsync);
        Store = new LocalReadModelStore(LocalReadModelStore.DefaultDbPath());
        Sync = new LocalSyncService(Api, Store);
        var outboxStore = new CommandOutboxStore(CommandOutboxStore.DefaultDbPath());
        Outbox = new CommandOutbox(outboxStore);
        Commands = new SyncCommandService(Api, Outbox, Store, Sync);
        Executions = new LocalExecutionProjector(Store);
    }

    /// <summary>
    /// Starts the SSE change-notification consumer exactly once.
    /// Idempotent; safe to call from every window that wants live sync.
    /// </summary>
    public void EnsureSyncStream()
    {
        lock (_streamGate)
        {
            if (_disposed)
            {
                return;
            }
            _stream ??= new SyncStreamService(Api, Sync);
            _stream.Start();
        }
    }

    /// <summary>
    /// Builds the edit-form seed (`EditableTileView`) for `tileId` from
    /// the local read model. The legacy `GET /v1/tiles/{id}/editable`
    /// endpoint only returns `{editable: bool}` on v1 and the legacy
    /// temporal/objective fields have no v1 counterpart, so only the
    /// title is populated; the rest stays null and the form degrades to
    /// title/description editing (NF04-D04).
    /// </summary>
    public EditableTileView? GetEditableTile(string tileId)
    {
        var tile = Executions.GetTile(tileId);
        if (tile is null)
        {
            return null;
        }
        return new EditableTileView(
            Id: tile.TileId,
            Title: tile.Title ?? tile.TileId,
            NextAction: null,
            DoneDefinition: null,
            Temporal: null,
            Objective: null,
            Interruption: null,
            Automation: null,
            Annotation: null);
    }

    public void Dispose()
    {
        lock (_streamGate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _stream?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _stream = null;
        }
        Outbox.Store.Dispose();
        Store.Dispose();
    }
}
