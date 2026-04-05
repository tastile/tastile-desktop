using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace TastileDesktop.Services;

public sealed class PromptToastDisplayService : IDisposable
{
    public static PromptToastDisplayService Instance { get; } = new();
    public static PromptToastDisplayService Current => Instance;

    private readonly List<PromptToastWindow> _windows = new();
    private readonly Dictionary<string, DateTimeOffset> _stackedPrompts = new(StringComparer.OrdinalIgnoreCase);
    private readonly SettingsService _settings = new();
    private CancellationTokenSource? _hideCts;
    private DispatcherTimer? _zOrderTimer;
    private bool _isVisible;

    public PromptToastDisplayService() { }

    public void ShowPrompt(Models.PromptView prompt, int maxActions, Func<string, DateTimeOffset?, Task> actionHandler, Func<string, int?, Task>? deferHandler = null)
    {
        var now = DateTimeOffset.UtcNow;
        _stackedPrompts[prompt.PromptId] = now;
        foreach (var key in _stackedPrompts.Where(kv => (now - kv.Value).TotalSeconds > 30).Select(kv => kv.Key).ToList())
        {
            _stackedPrompts.Remove(key);
        }

        var displays = PromptToastDisplayEnumerator.GetDisplays();
        EnsureWindowCount(displays.Count);

        for (var i = 0; i < displays.Count; i++)
        {
            var window = _windows[i];
            var display = displays[i];
            window.PlaceOnDisplay(display.WorkArea);
            window.ShowPrompt(prompt, maxActions, actionHandler, deferHandler);
        }

        StartZOrderGuard();
        _settings.Load();
        PromptToastSoundService.Instance.TriggerFromPromptToast(_settings.Current);
    }

    public void ShowBackdrop(Models.PromptView prompt, int waitingBehind)
    {
        var displays = PromptToastDisplayEnumerator.GetDisplays();
        EnsureWindowCount(displays.Count);

        for (var i = 0; i < displays.Count; i++)
        {
            var window = _windows[i];
            var display = displays[i];
            window.PlaceOnDisplay(display.WorkArea);
            window.ShowBackdrop(prompt, waitingBehind);
        }

        StartZOrderGuard();
    }

    public void Hide()
    {
        _hideCts?.Cancel();
        _hideCts?.Dispose();
        _hideCts = null;
        StopZOrderGuard();

        foreach (var window in _windows)
        {
            window.HidePrompt();
        }
    }

    private void StartZOrderGuard()
    {
        _isVisible = true;
        if (_zOrderTimer != null) return;
        _zOrderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _zOrderTimer.Tick += (_, _) =>
        {
            if (!_isVisible) return;
            foreach (var window in _windows)
            {
                window.ReassertTopMost();
            }
        };
        _zOrderTimer.Start();
    }

    private void StopZOrderGuard()
    {
        _isVisible = false;
        _zOrderTimer?.Stop();
        _zOrderTimer = null;
    }

    private void EnsureWindowCount(int count)
    {
        while (_windows.Count < count)
        {
            var window = new PromptToastWindow();
            _windows.Add(window);
        }

        while (_windows.Count > count)
        {
            var last = _windows[^1];
            _windows.RemoveAt(_windows.Count - 1);
            last.HidePrompt();
        }
    }

    public void Dispose()
    {
        StopZOrderGuard();
        _hideCts?.Cancel();
        _hideCts?.Dispose();
        foreach (var window in _windows)
        {
            window.HidePrompt();
        }
        _windows.Clear();
    }
}
