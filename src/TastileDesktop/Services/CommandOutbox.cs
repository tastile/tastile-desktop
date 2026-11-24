using System.Text.Json;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// Replays the persistent command outbox in enqueue order (v1/14 §1-1,
/// desktop NF04-D06). Each command is sent with the exact envelope and
/// idempotency key captured at enqueue time; the outcome is classified:
///
/// - 2xx → `confirmed` (Committed; `accepted_at` kept for audit)
/// - ApiError kind STALE_REVISION / IDEMPOTENCY_KEY_REUSED / CONFLICT /
///   BLOCKED → `conflict` — surfaced, never auto-resolved (no LWW)
/// - ApiError kind VALIDATION / FORBIDDEN / NOT_FOUND → `failed`
/// - RETRYABLE / 5xx / transport failure → stays `pending`; the flush
///   stops so later commands do not overtake the failed one
///
/// A conflict or permanent failure does NOT stop the flush — those
/// commands are terminally resolved and later commands still replay in
/// order. Only a retryable/transport failure halts the pass.
/// </summary>
public sealed class CommandOutbox
{
    private readonly CommandOutboxStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CommandOutbox(CommandOutboxStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public CommandOutboxStore Store => _store;

    /// <summary>Persists a command before any send attempt (v1/14 §1-1).</summary>
    public long? Enqueue(OutboxEnqueueRequest request) => _store.Enqueue(request);

    /// <summary>All queued commands in replay order (any state).</summary>
    public IReadOnlyList<PendingCommandRecord> GetAll() => _store.GetByState();

    /// <summary>Commands still awaiting a server outcome, in replay order.</summary>
    public IReadOnlyList<PendingCommandRecord> GetPending() => _store.GetPending();

    /// <summary>Conflicts awaiting user resolution.</summary>
    public IReadOnlyList<PendingCommandRecord> GetConflicts()
        => _store.GetByState(OutboxCommandState.Conflict);

    /// <summary>Requeue a conflict/failed command for another replay pass.</summary>
    public void Requeue(long seq) => _store.Requeue(seq);

    /// <summary>
    /// Sends every pending command in enqueue order through `sender`.
    /// `sender` performs the actual HTTP call; transport exceptions are
    /// treated as retryable and halt the pass.
    /// </summary>
    public async Task<OutboxFlushResult> FlushAsync(
        Func<PendingCommandRecord, CancellationToken, Task<CommandTransportResult>> sender,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new OutboxFlushResult(0, 0, 0, _store.PendingCount(), "flush already in flight");
        }

        try
        {
            var confirmed = 0;
            var conflicts = 0;
            var failed = 0;
            string? stopReason = null;

            foreach (var command in _store.GetPending())
            {
                cancellationToken.ThrowIfCancellationRequested();

                CommandTransportResult result;
                try
                {
                    result = await sender(command, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Transport failure: command stays pending, flush stops —
                    // later commands must not overtake it.
                    _store.RecordAttempt(command.Seq, ex.Message);
                    stopReason = $"transport error on seq {command.Seq}: {ex.Message}";
                    break;
                }

                if (result.IsSuccess)
                {
                    _store.MarkConfirmed(command.Seq, result.Body, ExtractAcceptedAt(result.Body));
                    confirmed++;
                    continue;
                }

                var kind = ExtractApiErrorKind(result.Body);
                switch (kind)
                {
                    case ApiErrorKind.StaleRevision:
                    case ApiErrorKind.IdempotencyKeyReused:
                    case ApiErrorKind.Conflict:
                    case ApiErrorKind.Blocked:
                        _store.MarkConflict(command.Seq, Truncate(result.Body));
                        conflicts++;
                        break;

                    case ApiErrorKind.Validation:
                    case ApiErrorKind.Forbidden:
                    case ApiErrorKind.NotFound:
                        _store.MarkFailed(command.Seq, Truncate(result.Body));
                        failed++;
                        break;

                    default:
                        // RETRYABLE, CURSOR_EXPIRED (n/a for commands),
                        // unparseable body, or 5xx — stays pending.
                        _store.RecordAttempt(command.Seq, $"HTTP {result.StatusCode}: {Truncate(result.Body)}");
                        stopReason = $"retryable failure on seq {command.Seq} (HTTP {result.StatusCode})";
                        break;
                }

                if (stopReason is not null)
                {
                    break;
                }
            }

            return new OutboxFlushResult(confirmed, conflicts, failed, _store.PendingCount(), stopReason);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string? ExtractAcceptedAt(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("accepted_at", out var element)
                && element.ValueKind == JsonValueKind.String
                    ? element.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? ExtractApiErrorKind(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("kind", out var element)
                && element.ValueKind == JsonValueKind.Number
                && element.TryGetInt32(out var kind)
                    ? kind
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value, int max = 2000)
        => value.Length <= max ? value : value[..max];
}
