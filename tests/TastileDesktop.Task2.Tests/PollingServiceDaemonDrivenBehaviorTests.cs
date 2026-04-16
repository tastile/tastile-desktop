using System.Net;
using TastileDesktop.Services;

namespace TastileDesktop.Task2.Tests;

public sealed class PollingServiceDaemonDrivenBehaviorTests
{
    [Fact]
    public async Task PollingService_StartAsync_RegistersDesktopWallClockTick()
    {
        var scheduler = new FakeWallClockPollScheduler();
        var pollCount = 0;
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:19084/");
        listener.Start();
        var serverTask = RunServerAsync(listener);

        using var sut = new PollingService(
            new CoreApiClient("http://127.0.0.1:19084"),
            new DaemonManager(),
            scheduler,
            () =>
            {
                pollCount++;
                return Task.CompletedTask;
            });

        await sut.StartAsync();

        Assert.Equal(TimeSpan.FromSeconds(1), scheduler.Interval);
        scheduler.Fire();
        Assert.True(pollCount > 0);

        listener.Stop();
        await serverTask;
    }

    private static async Task RunServerAsync(HttpListener listener)
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            var path = context.Request.Url?.AbsolutePath ?? "/";
            var payload = path switch
            {
                "/health" => "ok",
                "/read/execution-view" => "{\"tiles_in_progress\":[],\"main_tile\":null,\"is_working\":false,\"is_on_break\":false,\"is_idle\":true,\"main_tile_started_at\":null,\"main_tile_ends_at\":null,\"pending_prompt_id\":null,\"tile_count\":1,\"event_count\":0}",
                "/read/tiles" => "{\"tiles\":[],\"next_actionable_tile_id\":null,\"next_actionable_start_at\":null}",
                "/views/pending-prompt" => "{\"prompt\":null}",
                "/views/timeline/today" => "{\"items\":[]}",
                _ => "{}",
            };

            var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
            context.Response.StatusCode = 200;
            context.Response.ContentType = path == "/health" ? "text/plain" : "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
    }

    private sealed class FakeWallClockPollScheduler : IWallClockPollScheduler
    {
        public TimeSpan? Interval { get; private set; }
        private Action? _tick;

        public void Start(TimeSpan interval, Action tick)
        {
            Interval = interval;
            _tick = tick;
        }

        public void Stop() { }

        public void Fire() => _tick?.Invoke();
    }
}
