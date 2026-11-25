using TastileDesktop.Services;

namespace TastileDesktop.Tests;

/// <summary>
/// NF04-D08: tray-quit teardown ordering — outbox flush first, then
/// service disposal, then process exit; every step isolated so a failure
/// cannot strand the app resident.
/// </summary>
public sealed class GracefulShutdownTests
{
    [Fact]
    public async Task RunsFlushDisposeThenExit_InOrder()
    {
        var order = new List<string>();

        await GracefulShutdown.RunAsync(
            flushAsync: _ =>
            {
                order.Add("flush");
                return Task.CompletedTask;
            },
            disposeResources: () => order.Add("dispose"),
            exit: () => order.Add("exit"));

        Assert.Equal(new[] { "flush", "dispose", "exit" }, order);
    }

    [Fact]
    public async Task FlushFailure_StillDisposesAndExits()
    {
        var disposed = false;
        var exited = false;

        await GracefulShutdown.RunAsync(
            flushAsync: _ => throw new HttpRequestException("offline"),
            disposeResources: () => disposed = true,
            exit: () => exited = true);

        Assert.True(disposed);
        Assert.True(exited);
    }

    [Fact]
    public async Task DisposeFailure_StillExits()
    {
        var exited = false;

        await GracefulShutdown.RunAsync(
            flushAsync: null,
            disposeResources: () => throw new InvalidOperationException("boom"),
            exit: () => exited = true);

        Assert.True(exited);
    }

    [Fact]
    public async Task FlushReceivesBoundedCancellation()
    {
        using var cts = new CancellationTokenSource();
        var observed = false;

        await GracefulShutdown.RunAsync(
            flushAsync: ct =>
            {
                observed = ct.CanBeCanceled;
                return Task.CompletedTask;
            },
            disposeResources: null,
            exit: () => { },
            flushTimeout: TimeSpan.FromMilliseconds(50));

        Assert.True(observed);
        Assert.False(cts.IsCancellationRequested);
    }
}
