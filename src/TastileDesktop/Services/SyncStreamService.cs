using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// Long-running SSE consumer for `GET /v1/sync/stream` (v1/14 §6-6,
/// desktop NF04-D03). The stream is a notification-only doorbell: every
/// `change` event triggers a debounced delta pull through
/// <see cref="LocalSyncService"/>; entity state is never read from SSE.
///
/// Recovery semantics honored here:
/// - Best-effort delivery: drops, duplicates, reordering, and disconnects
///   are all tolerated — the persisted cursor + `GET /v1/sync` is the
///   authoritative path (§6-6).
/// - `Last-Event-ID` resumes from the last applied sync cursor.
/// - `cursor-expired` / `overflow` / `rejected` terminal events and any
///   transport failure schedule a delta pull (which self-heals via the
///   store's 410 → resnapshot path) and then reconnect.
/// - Reconnect honors the server `retry:` hint; consecutive failures use
///   capped exponential backoff with ±25% jitter to avoid reconnect
///   storms (§6-6).
/// - When SSE is unavailable the existing poller-driven delta pull
///   (60s throttle in <see cref="EventDrivenPoller"/>) remains the
///   adaptive polling fallback.
/// </summary>
public sealed class SyncStreamService : IAsyncDisposable
{
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan NotifyDebounce = TimeSpan.FromMilliseconds(250);

    private readonly CoreApiClient _api;
    private readonly LocalSyncService _sync;
    private readonly LocalReadModelStore _store;
    private readonly object _notifyGate = new();
    private CancellationTokenSource? _notifyCts;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private int _consecutiveFailures;
    private int _serverRetryMs;

    public SyncStreamService(CoreApiClient api, LocalSyncService sync)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        _store = sync.Store;
    }

    /// <summary>Raised for observability/tests when a `change` notification arrives.</summary>
    public event EventHandler? ChangeNotificationReceived;

    public void Start()
    {
        if (_loopTask is not null)
        {
            return;
        }
        _loopCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunAsync(_loopCts.Token));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Resume from the last applied sync cursor; the server
                // replays notifications after it and dedup is handled by
                // the cursor comparison on our side.
                var lastEventId = _store.SyncCursor;
                using var response = await _api
                    .OpenSyncStreamAsync(lastEventId, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    // 401/403/429/5xx — treat as a failed connect attempt.
                    _consecutiveFailures++;
                    await Task.Delay(NextDelay(), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                _consecutiveFailures = 0;
                await ReadStreamAsync(response, cancellationToken).ConfigureAwait(false);

                // Clean or terminal close: pull once (covers cursor-expired
                // and any notifications dropped during the gap), then
                // reconnect after the negotiated delay.
                await NotifySyncAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(NextDelay(), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                _consecutiveFailures++;
                try
                {
                    await Task.Delay(NextDelay(), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ReadStreamAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        await foreach (var sse in SseParser.ReadAsync(stream, cancellationToken).ConfigureAwait(false))
        {
            if (sse.RetryMs is { } retry && retry > 0)
            {
                _serverRetryMs = retry;
            }

            switch (sse.Event)
            {
                case "change":
                    ChangeNotificationReceived?.Invoke(this, EventArgs.Empty);
                    // Fire-and-forget: do not stall the read loop behind
                    // the debounce + delta pull.
                    _ = NotifySyncAsync(cancellationToken);
                    break;

                case "cursor-expired":
                case "overflow":
                case "rejected":
                    // Terminal server events: the connection is closing.
                    // A delta pull self-heals (410 → resnapshot inside
                    // LocalSyncService); return to trigger reconnect.
                    await NotifySyncAsync(cancellationToken).ConfigureAwait(false);
                    return;
            }
        }
    }

    /// <summary>
    /// Debounces notification bursts into a single delta pull. Multiple
    /// `change` events inside <see cref="NotifyDebounce"/> collapse to one
    /// <see cref="LocalSyncService.SyncOnceAsync"/> call; the service's
    /// own gate collapses calls that would overlap an in-flight sync.
    /// </summary>
    private async Task NotifySyncAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource cts;
        lock (_notifyGate)
        {
            _notifyCts?.Cancel();
            cts = new CancellationTokenSource();
            _notifyCts = cts;
        }

        try
        {
            await Task.Delay(NotifyDebounce, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return; // superseded by a newer notification
        }

        try
        {
            await _sync.SyncOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort: the next notification or poll-cycle retries.
            // Swallows cancellation too — this path is fire-and-forget.
        }
    }

    private TimeSpan NextDelay()
    {
        // Server `retry:` hint wins while the connection is healthy;
        // consecutive failures escalate exponentially up to the cap.
        var baseMs = _serverRetryMs > 0 ? _serverRetryMs : DefaultRetryDelay.TotalMilliseconds;
        var factor = Math.Min(Math.Pow(2, _consecutiveFailures), 16);
        var jitter = 0.75 + (Random.Shared.NextDouble() * 0.5); // ±25%
        var ms = Math.Min(baseMs * factor * jitter, MaxRetryDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(ms);
    }

    public async ValueTask DisposeAsync()
    {
        _loopCts?.Cancel();
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch
            {
                // Loop exits via cancellation; nothing to surface.
            }
        }
        lock (_notifyGate)
        {
            _notifyCts?.Cancel();
            _notifyCts?.Dispose();
            _notifyCts = null;
        }
        _loopCts?.Dispose();
        _loopCts = null;
        _loopTask = null;
    }
}
