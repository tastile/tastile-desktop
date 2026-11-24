using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TastileDesktop.Services;

/// <summary>
/// Minimal HTTP/1.1 loopback listener that receives the Authorization Code
/// redirect for the Desktop PKCE flow (NF04-D01).
///
/// The contract (<c>docs/auth/desktop-auth-contract.md</c>, web#253) fixes
/// <c>redirect_uri=http://127.0.0.1:&lt;port&gt;/callback</c> on an ephemeral
/// port. A raw <see cref="TcpListener"/> is used instead of
/// <see cref="System.Net.HttpListener"/> so the unpackaged app does not
/// depend on http.sys URL ACLs.
///
/// The listener accepts requests until one targets <c>/callback</c>; every
/// other path gets a 404 and the wait continues. The browser is expected to
/// follow with <c>?code=&lt;opaque&gt;&amp;state=&lt;echo&gt;</c> (or an
/// <c>error</c> pair on denial). At most one successful callback is
/// returned; the listener is single-use per sign-in attempt.
/// </summary>
public sealed class LoopbackCallbackListener : IAsyncDisposable
{
    private const int MaxRequestHeadBytes = 16 * 1024;

    private readonly TcpListener _listener;
    private bool _disposed;

    public LoopbackCallbackListener()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        RedirectUri = $"http://127.0.0.1:{port}{Pkce.CallbackPath}";
    }

    /// <summary>The exact <c>redirect_uri</c> value sent to <c>/cli/authorize</c>.</summary>
    public string RedirectUri { get; }

    /// <summary>
    /// Waits for the browser redirect. Returns the parsed callback query on
    /// the first <c>/callback</c> request; throws
    /// <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/>
    /// is cancelled or the listener is disposed.
    /// </summary>
    public async Task<LoopbackCallback> WaitForCallbackAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        while (true)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SocketException) when (_disposed)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            using (client)
            {
                var callback = await HandleClientAsync(client, cancellationToken).ConfigureAwait(false);
                if (callback is not null)
                {
                    return callback;
                }
            }
        }
    }

    private async Task<LoopbackCallback?> HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        string? requestTarget = null;
        string? method = null;

        try
        {
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

            // Read the request head (request line + headers) with a hard cap so
            // a hostile or broken peer cannot stream headers forever.
            var head = new StringBuilder();
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                if (line.Length == 0)
                {
                    break; // end of headers
                }

                head.Append(line).Append('\n');
                if (head.Length > MaxRequestHeadBytes)
                {
                    await WriteResponseAsync(stream, 431, "Request Header Fields Too Large", "Request too large.").ConfigureAwait(false);
                    return null;
                }
            }

            var requestLine = head.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.TrimEnd('\r');
            if (!string.IsNullOrEmpty(requestLine))
            {
                var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    method = parts[0];
                    requestTarget = parts[1];
                }
            }

            if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(requestTarget)
                || !Uri.TryCreate(requestTarget, UriKind.RelativeOrAbsolute, out var requestUri))
            {
                await WriteResponseAsync(stream, 400, "Bad Request", "Bad request.").ConfigureAwait(false);
                return null;
            }

            var absolutePath = requestUri.IsAbsoluteUri ? requestUri.AbsolutePath : requestUri.OriginalString.Split('?', '#')[0];
            if (!string.Equals(absolutePath, Pkce.CallbackPath, StringComparison.Ordinal))
            {
                await WriteResponseAsync(stream, 404, "Not Found", "Not found.").ConfigureAwait(false);
                return null;
            }

            var queryIndex = requestTarget.IndexOf('?', StringComparison.Ordinal);
            var query = queryIndex >= 0 ? requestTarget[(queryIndex + 1)..] : string.Empty;
            var fragmentIndex = query.IndexOf('#', StringComparison.Ordinal);
            if (fragmentIndex >= 0)
            {
                query = query[..fragmentIndex];
            }

            var values = System.Web.HttpUtility.ParseQueryString(query);
            var callback = new LoopbackCallback(
                Code: values["code"],
                State: values["state"],
                Error: values["error"],
                ErrorDescription: values["error_description"]);

            await WriteResponseAsync(stream, 200, "OK", SuccessPageHtml).ConfigureAwait(false);
            return callback;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Malformed request or client disconnect: respond best-effort and
            // keep waiting for the real callback.
            try
            {
                await WriteResponseAsync(client.GetStream(), 400, "Bad Request", "Bad request.").ConfigureAwait(false);
            }
            catch
            {
                // Ignore — the peer is gone.
            }

            return null;
        }
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int statusCode, string statusText, string body)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var head =
            $"HTTP/1.1 {statusCode} {statusText}\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            "Connection: close\r\n" +
            "\r\n";
        var headBytes = Encoding.ASCII.GetBytes(head);
        await stream.WriteAsync(headBytes).ConfigureAwait(false);
        await stream.WriteAsync(bodyBytes).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _listener.Stop();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private const string SuccessPageHtml =
        "<!doctype html><html><head><meta charset=\"utf-8\">" +
        "<title>Tastile</title></head><body>" +
        "<p>Sign-in complete. You can close this tab and return to Tastile.</p>" +
        "</body></html>";
}

/// <summary>Parsed query values from the loopback <c>/callback</c> redirect.</summary>
public sealed record LoopbackCallback(
    string? Code,
    string? State,
    string? Error,
    string? ErrorDescription);
