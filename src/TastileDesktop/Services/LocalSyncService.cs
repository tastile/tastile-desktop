using TastileDesktop.Models;

namespace TastileDesktop.Services;

public enum SyncOutcome
{
    /// <summary>Snapshot (if needed) + delta pull completed.</summary>
    Synced,

    /// <summary>A concurrent sync was already in flight; this call did nothing.</summary>
    Skipped,

    /// <summary>The server or transport failed; the local model is unchanged or partially advanced per-page.</summary>
    Failed,

    /// <summary>Internal: the delta cursor hit the retention frontier (HTTP 410); triggers one resnapshot.</summary>
    CursorExpired,
}

public sealed record SyncResult(SyncOutcome Outcome, string? Detail = null)
{
    public bool Success => Outcome == SyncOutcome.Synced;
}

/// <summary>
/// Drives the Sync API v2 contract (tastile-core v1/14 §6) against
/// <see cref="LocalReadModelStore"/> (desktop NF04-D02/D03 foundation).
///
/// - No persisted cursor → full snapshot walk (paged until
///   `next_cursor == null`), then <see cref="LocalReadModelStore.CommitSnapshot"/>
///   seeds `snapshotCursor` as the delta start.
/// - Persisted cursor → delta pull loop; each page's changes + cursor
///   advance commit in one local transaction inside the store.
/// - HTTP 410 (`CURSOR_EXPIRED`, v1/14 §6-5) → drop the cursor, keep the
///   stale entities for offline reads, and re-snapshot once.
/// </summary>
public sealed class LocalSyncService
{
    /// <summary>Sync API page size limit is 1..=1000 (v1/14 §15-2).</summary>
    public const int PageLimit = 1000;

    private readonly CoreApiClient _api;
    private readonly LocalReadModelStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalSyncService(CoreApiClient api, LocalReadModelStore store)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public LocalReadModelStore Store => _store;

    /// <summary>
    /// Runs one sync pass: snapshot when no cursor is persisted, then drain
    /// the delta log. Safe to call repeatedly; concurrent calls collapse
    /// to a single in-flight pass.
    /// </summary>
    public async Task<SyncResult> SyncOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SyncResult(SyncOutcome.Skipped, "sync already in flight");
        }

        try
        {
            // CURSOR_EXPIRED recovery gets exactly one resnapshot attempt;
            // a second 410 means the contract is broken, not retryable here.
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (_store.SyncCursor is null)
                {
                    var snapshot = await RunSnapshotAsync(cancellationToken).ConfigureAwait(false);
                    if (!snapshot.Success)
                    {
                        return snapshot;
                    }
                }

                var delta = await RunDeltaAsync(cancellationToken).ConfigureAwait(false);
                if (delta.Outcome != SyncOutcome.CursorExpired)
                {
                    return delta;
                }

                _store.ResetSyncCursor();
            }

            return new SyncResult(SyncOutcome.Failed, "sync cursor expired twice");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SyncResult> RunSnapshotAsync(CancellationToken cancellationToken)
    {
        _store.BeginSnapshot();

        string? pageCursor = null;
        string snapshotCursor = string.Empty;
        while (true)
        {
            var page = await _api
                .GetSyncSnapshotPageWithStatusAsync(pageCursor, PageLimit, cancellationToken)
                .ConfigureAwait(false);
            if (!page.IsSuccess || page.Data is null)
            {
                return new SyncResult(SyncOutcome.Failed, $"snapshot page failed: HTTP {(int?)page.StatusCode}");
            }

            // snapshotCursor is identical on every page of one walk
            // (v1/14 §6-1); keep the latest seen value defensively.
            snapshotCursor = page.Data.SnapshotCursor;
            _store.StageSnapshotEntities(page.Data.Entities);

            if (page.Data.NextCursor is null)
            {
                break;
            }
            pageCursor = page.Data.NextCursor;
        }

        _store.CommitSnapshot(snapshotCursor);
        return new SyncResult(SyncOutcome.Synced);
    }

    private async Task<SyncResult> RunDeltaAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var cursor = _store.SyncCursor;
            var page = await _api
                .GetSyncDeltaWithStatusAsync(cursor, PageLimit, cancellationToken)
                .ConfigureAwait(false);

            if (page.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                // v1/14 §6-5: cursor predates the retention frontier.
                return new SyncResult(SyncOutcome.CursorExpired, "sync cursor expired (HTTP 410)");
            }

            if (!page.IsSuccess || page.Data is null)
            {
                return new SyncResult(SyncOutcome.Failed, $"delta page failed: HTTP {(int?)page.StatusCode}");
            }

            _store.ApplyDeltaPage(page.Data.Changes, page.Data.NextCursor);

            // Empty next_cursor = tail of the change log reached. The last
            // non-empty cursor stays persisted; the tail events are
            // re-delivered once on the next pass and applied idempotently.
            if (string.IsNullOrEmpty(page.Data.NextCursor))
            {
                break;
            }
        }

        _store.SetMeta(LocalReadModelStore.LastSyncAtKey, DateTimeOffset.UtcNow.ToString("O"));
        return new SyncResult(SyncOutcome.Synced);
    }
}
