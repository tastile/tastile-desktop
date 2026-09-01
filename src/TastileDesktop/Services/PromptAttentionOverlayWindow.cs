using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace TastileDesktop.Services;

public sealed class PromptAttentionOverlayWindow : Window
{
    public const int OverlayThickness = 8;

    private Border? _bar;

    public PromptAttentionOverlayWindow()
    {
        _bar = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        Content = _bar;
        FloatingWindowHelper.ConfigurePanel(this, 120, OverlayThickness);
        FloatingWindowHelper.SetAlwaysOnTop(this, true);
    }

    public void ShowOverlay()
    {
        var queue = DispatcherQueue;
        if (queue != null)
        {
            queue.TryEnqueue(() =>
            {
                RefreshAccentColor();
                WindowExtensions.Show(this);
            });
        }
        else
        {
            RefreshAccentColor();
            WindowExtensions.Show(this);
        }
    }

    private void RefreshAccentColor()
    {
        if (_bar == null) return;
        _bar.Background = GetAccentColorBrush();
    }

    private static SolidColorBrush GetAccentColorBrush()
    {
        if (Application.Current?.Resources.TryGetValue("AppPrimaryBrush", out var resource) == true
            && resource is SolidColorBrush brush && brush.Color.A != 0)
        {
            return brush;
        }

        try
        {
            var uiSettings = new UISettings();
            var color = uiSettings.GetColorValue(UIColorType.Accent);
            if (color.A != 0)
            {
                return new SolidColorBrush(color);
            }
        }
        catch { }

        return new SolidColorBrush(ThemeManager.CurrentSnapshot.AccentColorValue);
    }
}
