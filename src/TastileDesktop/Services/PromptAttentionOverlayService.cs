using Microsoft.UI.Xaml;

namespace TastileDesktop.Services;

public sealed class PromptAttentionOverlayService : IDisposable
{
    public static PromptAttentionOverlayService Instance { get; } = new();
    public static PromptAttentionOverlayService Current => Instance;

    public event EventHandler<TimeSpan>? OverlayRequested;

    private enum OverlayEdge
    {
        Top,
        Right,
        Bottom,
        Left
    }

    private sealed record OverlaySlot(DisplayInfo Display, OverlayEdge Edge);

    private PollingService? _pollingService;
    private readonly SettingsService _settings = new();
    private readonly Dictionary<string, DateTimeOffset> _stackedPrompts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PromptAttentionOverlayWindow> _overlayWindows = [];
    private CancellationTokenSource? _overlayHideCts;

    public PromptAttentionOverlayService() { }

    public PromptAttentionOverlayService(PollingService pollingService)
    {
        Initialize(pollingService);
    }

    public void Initialize(PollingService pollingService)
    {
        if (ReferenceEquals(_pollingService, pollingService))
        {
            return;
        }

        if (_pollingService != null)
        {
            _pollingService.PendingPromptChanged -= OnPendingPromptChanged;
        }

        _pollingService = pollingService;
        _pollingService.PendingPromptChanged += OnPendingPromptChanged;
    }

    private void OnPendingPromptChanged(object? sender, Models.PendingPromptResponse? payload)
    {
        if (payload?.Prompt == null || !_settings.Current.PromptOverlayEnabled)
        {
            return;
        }

        var prompt = payload.Prompt;
        var now = DateTimeOffset.UtcNow;
        _stackedPrompts[prompt.PromptId] = now;
        foreach (var key in _stackedPrompts.Where(kv => (now - kv.Value).TotalSeconds > 30).Select(kv => kv.Key).ToList())
        {
            _stackedPrompts.Remove(key);
        }

        var duration = TimeSpan.FromSeconds(Math.Clamp(_settings.Current.PromptOverlayDurationSeconds, 1, 15));
        var stackedCount = Math.Clamp(_stackedPrompts.Count, 1, Math.Max(1, _settings.Current.PromptToastMaxVisible));
        var effective = TimeSpan.FromSeconds(Math.Clamp(duration.TotalSeconds * Math.Min(2, stackedCount), 1, 20));
        TriggerOverlay(effective);
    }

    public void Show()
    {
        TriggerOverlay(TimeSpan.FromSeconds(2));
    }

    public void Hide()
    {
        _overlayHideCts?.Cancel();
        _overlayHideCts?.Dispose();
        _overlayHideCts = null;
        foreach (var overlay in _overlayWindows)
        {
            overlay.Hide();
        }
    }

    public void Dispose()
    {
        if (_pollingService != null)
        {
            _pollingService.PendingPromptChanged -= OnPendingPromptChanged;
        }

        _overlayHideCts?.Cancel();
        _overlayHideCts?.Dispose();
        foreach (var overlay in _overlayWindows)
        {
            overlay.Close();
        }
        _overlayWindows.Clear();
    }

    public Task ShowTestOverlayAsync() => ShowTestOverlayAsync(TimeSpan.FromSeconds(3));

    public Task ShowTestOverlayAsync(TimeSpan duration)
    {
        TriggerOverlay(duration);
        return Task.CompletedTask;
    }

    private void TriggerOverlay(TimeSpan duration)
    {
        ShowOverlayFor(duration);
        OverlayRequested?.Invoke(this, duration);
    }

    private void ShowOverlayFor(TimeSpan duration)
    {
        var slots = ResolveOverlaySlots();
        EnsureOverlayWindowCount(slots.Count);

        for (var i = 0; i < slots.Count; i++)
        {
            var overlay = _overlayWindows[i];
            PlaceOverlayWindow(overlay, slots[i]);
            overlay.ShowOverlay();
        }

        for (var i = slots.Count; i < _overlayWindows.Count; i++)
        {
            _overlayWindows[i].Hide();
        }

        _overlayHideCts?.Cancel();
        _overlayHideCts?.Dispose();
        _overlayHideCts = new CancellationTokenSource();
        var token = _overlayHideCts.Token;
        _ = HideLaterAsync(duration, token);
    }

    private IReadOnlyList<OverlaySlot> ResolveOverlaySlots()
    {
        var displays = PromptToastDisplayEnumerator.GetDisplays();
        if (displays.Count == 0)
        {
            return [];
        }

        var slots = new List<OverlaySlot>(displays.Count * 4);
        foreach (var display in displays)
        {
            slots.Add(new OverlaySlot(display, OverlayEdge.Top));
            slots.Add(new OverlaySlot(display, OverlayEdge.Right));
            slots.Add(new OverlaySlot(display, OverlayEdge.Bottom));
            slots.Add(new OverlaySlot(display, OverlayEdge.Left));
        }
        return slots;
    }

    private void EnsureOverlayWindowCount(int count)
    {
        while (_overlayWindows.Count < count)
        {
            _overlayWindows.Add(new PromptAttentionOverlayWindow());
        }

        while (_overlayWindows.Count > count)
        {
            var last = _overlayWindows[^1];
            _overlayWindows.RemoveAt(_overlayWindows.Count - 1);
            last.Close();
        }
    }

    private static void PlaceOverlayWindow(Window overlay, OverlaySlot slot)
    {
        var appWindow = GetAppWindow(overlay);
        if (appWindow == null)
        {
            return;
        }

        var area = slot.Display.WorkArea;
        var t = PromptAttentionOverlayWindow.OverlayThickness;
        var width = area.Width;
        var height = t;
        var x = area.X;
        var y = area.Y;

        switch (slot.Edge)
        {
            case OverlayEdge.Top:
                width = area.Width + (t * 2);
                height = t;
                x = area.X - t;
                y = area.Y;
                break;
            case OverlayEdge.Right:
                width = t;
                height = area.Height + (t * 2);
                x = area.X + area.Width - t;
                y = area.Y - t;
                break;
            case OverlayEdge.Bottom:
                width = area.Width + (t * 2);
                height = t;
                x = area.X - t;
                y = area.Y + area.Height - t;
                break;
            case OverlayEdge.Left:
                width = t;
                height = area.Height + (t * 2);
                x = area.X;
                y = area.Y - t;
                break;
        }

        appWindow.Resize(new Windows.Graphics.SizeInt32(Math.Max(1, width), Math.Max(1, height)));
        appWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }

    private static Microsoft.UI.Windowing.AppWindow? GetAppWindow(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero) return null;
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
    }

    private async Task HideLaterAsync(TimeSpan duration, CancellationToken token)
    {
        try
        {
            await Task.Delay(duration, token);
            if (!token.IsCancellationRequested)
            {
                foreach (var overlay in _overlayWindows)
                {
                    overlay.Hide();
                }
            }
        }
        catch (TaskCanceledException)
        {
        }
    }
}
