using TastileDesktop.Services;

namespace TastileDesktop.Task2.Tests;

public sealed class PollingServiceWallClockBehaviorTests
{
    [Fact]
    public void PollingService_RegistersWallClockTick_AndTickInvokesPollAction()
    {
        var scheduler = new FakeWallClockPollScheduler();
        var pollCalls = 0;

        using var sut = new PollingService(
            new CoreApiClient("http://127.0.0.1:1"),
            new DaemonManager(),
            scheduler,
            () =>
            {
                pollCalls++;
                return Task.CompletedTask;
            });

        Assert.Equal(TimeSpan.FromSeconds(1), scheduler.Interval);
        scheduler.RaiseTick();
        Assert.Equal(1, pollCalls);
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
