using System.Net;
using TastileDesktop.Services;

namespace TastileDesktop.Task2.Tests;

public sealed class PollingServiceWallClockBehaviorTests
{
    [Fact]
    public async Task PollingService_RegistersWallClockTick_AndTickInvokesPollAction()
    {
        var scheduler = new FakeWallClockPollScheduler();
        var pollCalls = 0;
        var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:19083/");
        listener.Start();
        var serverTask = Task.Run(async () =>
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

                var bytes = System.Text.Encoding.UTF8.GetBytes("{}");
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        });

        using var sut = new PollingService(
            new CoreApiClient("http://127.0.0.1:19083"),
            new DaemonManager(),
            scheduler,
            () =>
            {
                pollCalls++;
                return Task.CompletedTask;
            });

        Assert.Equal(TimeSpan.FromSeconds(1), scheduler.Interval);
        scheduler.RaiseTick();
        for (var i = 0; i < 20 && pollCalls == 0; i++)
        {
            await Task.Delay(50);
        }
        Assert.Equal(1, pollCalls);

        listener.Stop();
        listener.Close();
        await serverTask;
    }

    [Fact]
    public async Task PollingService_WallClockTick_TriggersRealPollAsync()
    {
        var scheduler = new FakeWallClockPollScheduler();
        var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:19082/");
        listener.Start();
        var healthCalls = 0;
        var serverTask = Task.Run(async () =>
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
                if (path == "/health")
                {
                    Interlocked.Increment(ref healthCalls);
                }
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
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        });

        var api = new CoreApiClient("http://127.0.0.1:19082");
        PollingService? sut = null;
        sut = new PollingService(api, new DaemonManager(), scheduler, () => sut!.PollAsync());
        using (sut)
        {
            await sut.PollAsync();
            var before = Volatile.Read(ref healthCalls);

            scheduler.RaiseTick();
            await Task.Delay(80);

            var after = Volatile.Read(ref healthCalls);
            Assert.True(after > before);
        }

        listener.Stop();
        listener.Close();
        await serverTask;
    }

    private sealed class FakeWallClockPollScheduler : IWallClockPollScheduler
    {
        private Action? _tick;

        public TimeSpan? Interval { get; private set; }

        public void Start(TimeSpan interval, Action tick)
        {
            Interval = interval;
            _tick = tick;
        }

        public void Stop() { }

        public void RaiseTick() => _tick?.Invoke();
    }
}
