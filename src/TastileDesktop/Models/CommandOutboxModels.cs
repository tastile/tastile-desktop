using System.Text.Json.Serialization;

namespace TastileDesktop.Models;

// Offline command outbox models (tastile-core v1/14 §1-1, desktop NF04-D06).
// Commands queued while disconnected are persisted with their
// idempotency key and client-observed time, replayed in enqueue order,
// and classified as confirmed / conflict / failed — never silently
// dropped or marked committed without a server response.

/// <summary>Lifecycle state of a queued command.</summary>
public static class OutboxCommandState
{
    /// <summary>Queued locally; not yet accepted by the server.</summary>
    public const string Pending = "pending";

    /// <summary>Server returned 2xx; the command is committed.</summary>
    public const string Confirmed = "confirmed";

    /// <summary>Server rejected with a conflict-class error (STALE_REVISION / IDEMPOTENCY_KEY_REUSED / CONFLICT / BLOCKED). Surfaced, never auto-resolved.</summary>
    public const string Conflict = "conflict";

    /// <summary>Server rejected with a permanent non-conflict error (VALIDATION / FORBIDDEN / NOT_FOUND).</summary>
    public const string Failed = "failed";
}

/// <summary>Numeric `ApiErrorKind` constants (v1/14 §1-4, core constants.rs).</summary>
public static class ApiErrorKind
{
    public const int Validation = 0;
    public const int Forbidden = 1;
    public const int StaleRevision = 2;
    public const int IdempotencyKeyReused = 3;
    public const int NotFound = 4;
    public const int Conflict = 5;
    public const int Blocked = 6;
    public const int Retryable = 7;
    public const int CursorExpired = 8;
}

/// <summary>Input for enqueueing a command into the outbox.</summary>
public sealed record OutboxEnqueueRequest(
    string Kind,
    string HttpMethod,
    string Path,
    string EnvelopeJson,
    string IdempotencyKey,
    string ObservedAt);

/// <summary>A persisted outbox row.</summary>
public sealed record PendingCommandRecord(
    long Seq,
    string IdempotencyKey,
    string Kind,
    string HttpMethod,
    string Path,
    string EnvelopeJson,
    string ObservedAt,
    string State,
    int AttemptCount,
    string? LastError,
    string? ResponseJson,
    string? AcceptedAt,
    string CreatedAt,
    string UpdatedAt);

/// <summary>Transport-level result of one command send attempt.</summary>
public sealed record CommandTransportResult(
    int StatusCode,
    string Body,
    bool IsSuccess);

/// <summary>Summary of one outbox flush pass.</summary>
public sealed record OutboxFlushResult(
    int Confirmed,
    int Conflicts,
    int Failed,
    int Remaining,
    string? StopReason);
