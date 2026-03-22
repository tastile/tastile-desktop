namespace TastileDesktop.Services;

public sealed class PollingHealthCoordinator
{
    public static PollingHealthCoordinator Instance { get; } = new();
    
    public PollingHealthCoordinator() { }
    public PollingHealthCoordinator(string arg) { }
    
    public bool IsHealthy => true;
    public DateTimeOffset LastPollTime => DateTimeOffset.Now;
    
    public void RecordPoll() { }
    public void RecordError() { }
    public bool TryBeginPoll() => true;
    public bool TryBeginRecovery(object? arg = null) => true;
    public void EndPoll() { }
}
