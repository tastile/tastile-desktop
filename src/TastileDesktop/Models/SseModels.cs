using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;

namespace TastileDesktop.Models;

/// <summary>
/// One parsed Server-Sent Events frame (v1/14 §6-6). `Event` is the
/// `event:` field name (`change`, `cursor-expired`, `overflow`,
/// `rejected`, or empty for comment-only frames which are not emitted),
/// `Id` is the SSE `id:` field (a sync cursor for `change` events),
/// `Data` is the joined `data:` payload, and `RetryMs` carries the
/// `retry:` reconnection hint when present.
/// </summary>
public sealed record SseEvent(
    string Event,
    string? Id,
    string Data,
    int? RetryMs);

/// <summary>Payload of a `change` notification — never carries entity state.</summary>
public sealed record SyncChangeNotification(
    [property: JsonPropertyName("entityKind")] int EntityKind,
    [property: JsonPropertyName("entityId")] string EntityId,
    [property: JsonPropertyName("changeKind")] int ChangeKind);

/// <summary>
/// Minimal SSE parser for `GET /v1/sync/stream` (text/event-stream).
/// Handles `event:`/`data:`/`id:`/`retry:` fields, `:` comment lines
/// (heartbeats), multi-line `data:` joining with '\n', and CRLF/LF
/// endings. Frames are yielded on each blank-line dispatch.
/// </summary>
public static class SseParser
{
    public static async IAsyncEnumerable<SseEvent> ReadAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);

        var eventName = string.Empty;
        var data = new StringBuilder();
        string? id = null;
        int? retryMs = null;
        var hasFields = false;

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                // Dispatch on blank line; skip frames with no fields at all
                // (e.g. heartbeat-only blocks).
                if (hasFields)
                {
                    yield return new SseEvent(eventName, id, data.ToString(), retryMs);
                }
                eventName = string.Empty;
                data.Clear();
                id = null;
                retryMs = null;
                hasFields = false;
                continue;
            }

            if (line[0] == ':')
            {
                // Comment (e.g. ": ping" heartbeat) — never a field.
                continue;
            }

            var colon = line.IndexOf(':');
            var field = colon < 0 ? line : line[..colon];
            var value = colon < 0 ? string.Empty : line[(colon + 1)..];
            if (value.StartsWith(' '))
            {
                value = value[1..];
            }

            hasFields = true;
            switch (field)
            {
                case "event":
                    eventName = value;
                    break;
                case "data":
                    if (data.Length > 0)
                    {
                        data.Append('\n');
                    }
                    data.Append(value);
                    break;
                case "id":
                    id = value;
                    break;
                case "retry":
                    if (int.TryParse(value, out var retry))
                    {
                        retryMs = retry;
                    }
                    break;
            }
        }

        // Flush a trailing frame not terminated by a blank line.
        if (hasFields)
        {
            yield return new SseEvent(eventName, id, data.ToString(), retryMs);
        }
    }
}
