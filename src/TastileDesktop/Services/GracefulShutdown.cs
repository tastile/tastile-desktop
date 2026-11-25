namespace TastileDesktop.Services;

/// <summary>
/// Ordered teardown for tray-initiated quit (NF04-D08). Quitting must
/// actually stop the resident services — SSE stream, poller, outbox —
/// not just hide windows, while a best-effort outbox flush runs first so
/// pending commands get one last chance to reach the server.
///
/// The sequence never throws: each step is isolated so a failing flush
/// or dispose cannot prevent <paramref name="exit"/> from terminating
/// the process.
/// </summary>
public static class GracefulShutdown
{
    /// <summary>Default bound for the final outbox flush during quit.</summary>
    public static readonly TimeSpan DefaultFlushTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Runs flush → dispose → exit. <paramref name="flushAsync"/> receives
    /// a cancellation token bounded by <paramref name="flushTimeout"/>;
    /// <paramref name="disposeResources"/> stops long-running services
    /// (SSE loop, timers); <paramref name="exit"/> terminates the app.
    /// </summary>
    public static async Task RunAsync(
        Func<CancellationToken, Task>? flushAsync,
        Action? disposeResources,
        Action exit,
        TimeSpan? flushTimeout = null)
    {
        if (flushAsync is not null)
        {
            try
            {
                using var cts = new CancellationTokenSource(flushTimeout ?? DefaultFlushTimeout);
                await flushAsync(cts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Offline / signed-out / server-down: pending commands stay
                // durable in the outbox for the next launch. Quit proceeds.
            }
        }

        try
        {
            disposeResources?.Invoke();
        }
        catch
        {
            // Disposal is best-effort during process exit.
        }

        exit();
    }
}
