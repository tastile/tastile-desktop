namespace Microsoft.UI.Xaml;

public sealed class DispatcherTimer
{
    private Action<object?, object>? _tick;
    private System.Threading.Timer? _timer;

    public TimeSpan Interval { get; set; }

    public event Action<object?, object>? Tick
    {
        add => _tick += value;
        remove => _tick -= value;
    }

    public void Start()
    {
        _timer = new System.Threading.Timer(_ => _tick?.Invoke(this, null!), null, Interval, Interval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
