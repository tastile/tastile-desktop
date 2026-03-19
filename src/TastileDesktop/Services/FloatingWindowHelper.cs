using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using System.Collections.Generic;

namespace TastileDesktop.Services;

internal static class FloatingWindowHelper
{
    private static readonly List<WeakReference<Window>> OpenWindows = [];

    public static void Configure(Window window, FrameworkElement titleBar, int width, int height)
    {
        Register(window);
        ApplyWindowTheme(window);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(titleBar);

        ApplyTitleBarTheme(window);
    }

    public static void ApplyWindowTheme(Window window)
    {
        Register(window);
        ApplyWindowTheme(window, new SettingsService().Current.ThemeMode);
    }

    public static void RefreshOpenWindows(string mode)
    {
        for (var i = OpenWindows.Count - 1; i >= 0; i--)
        {
            if (!OpenWindows[i].TryGetTarget(out var window))
            {
                OpenWindows.RemoveAt(i);
                continue;
            }

            ApplyWindowTheme(window, mode);
            ApplyTitleBarTheme(window);
        }
    }

    private static void ApplyWindowTheme(Window window, string mode)
    {
        if (window.Content is not FrameworkElement root)
        {
            return;
        }

        root.RequestedTheme = ThemeManager.GetElementTheme(mode);
    }

    private static void ApplyTitleBarTheme(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var chrome = appWindow.TitleBar;
        chrome.BackgroundColor = ThemeManager.GetColor("AppSurfaceElevatedBrush");
        chrome.ForegroundColor = ThemeManager.GetColor("AppForegroundBrush");
        chrome.InactiveBackgroundColor = ThemeManager.GetColor("AppSurfaceElevatedBrush");
        chrome.InactiveForegroundColor = ThemeManager.GetColor("AppForegroundMutedBrush");
        chrome.ButtonBackgroundColor = ThemeManager.GetColor("AppSurfaceElevatedBrush");
        chrome.ButtonInactiveBackgroundColor = ThemeManager.GetColor("AppSurfaceElevatedBrush");
        chrome.ButtonForegroundColor = ThemeManager.GetColor("AppForegroundBrush");
        chrome.ButtonInactiveForegroundColor = ThemeManager.GetColor("AppForegroundMutedBrush");
        chrome.ButtonHoverBackgroundColor = ThemeManager.GetColor("AppSurface2Brush");
        chrome.ButtonHoverForegroundColor = ThemeManager.GetColor("AppForegroundBrush");
        chrome.ButtonPressedBackgroundColor = ThemeManager.GetColor("AppSurface1Brush");
        chrome.ButtonPressedForegroundColor = ThemeManager.GetColor("AppForegroundBrush");
    }

    private static void Register(Window window)
    {
        foreach (var existing in OpenWindows)
        {
            if (existing.TryGetTarget(out var current) && ReferenceEquals(current, window))
            {
                return;
            }
        }

        OpenWindows.Add(new WeakReference<Window>(window));
    }
}
