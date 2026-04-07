using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.UI;
using Windows.Graphics;

namespace TastileDesktop.Services;

internal static class FloatingWindowHelper
{
    private static readonly List<WeakReference<Window>> OpenWindows = [];

    public static void Configure(Window window, FrameworkElement titleBar, int width, int height)
    {
        Register(window);
        ApplyWindowTheme(window);
        ApplyBackdrop(window);

        var appWindow = GetAppWindow(window);
        if (appWindow is null)
        {
            return;
        }

        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(titleBar);

        ApplyTitleBarTheme(window);
    }

    public static void ConfigurePanel(Window window, int width, int height)
    {
        Register(window);
        ApplyWindowTheme(window);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        
        var appWindow = GetAppWindow(window);
        if (appWindow is null)
        {
            return;
        }

        appWindow.Resize(new SizeInt32(width, height));
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            // SetBorderAndTitleBar(false,false) は WinUI3 の描画パイプラインを壊すため使用しない
            // 代わりに Win32 スタイルのみで制御する
        }

        StripPanelWindowStyles(hwnd);
        ApplyPanelChrome(hwnd);
        HideFromTaskbar(hwnd); // タスクバーから隠す

        // ExtendsContentIntoTitleBar で XAML コンテンツをウィンドウ全体に広げる
        window.ExtendsContentIntoTitleBar = true;

        // MicaBackdrop を使用（SetBorderAndTitleBar を呼ばないので競合しない）
        try
        {
            window.SystemBackdrop = new MicaBackdrop();
        }
        catch
        {
            window.SystemBackdrop = null;
        }
    }

    private static int _displayIndex = -1;
    private static RectInt32? _lastWorkArea;
    private static IReadOnlyList<DisplayInfo>? _lastDisplays;
    private static string? _lastVerticalPosition;
    private static bool _forcePositionUpdate;
    
    public static void PlaceQuickPanel(Window window, TastileSettings settings)
    {
        var appWindow = GetAppWindow(window);
        if (appWindow is null)
        {
            return;
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var displays = PromptToastDisplayEnumerator.GetDisplays();
        if (displays.Count == 0)
        {
            return;
        }

        // Initialize to first display on first call
        if (_displayIndex < 0)
        {
            _displayIndex = 0;
        }

        // If displays changed (added/removed), reset to first
        if (_lastDisplays != null && displays.Count != _lastDisplays.Count)
        {
            _displayIndex = 0;
        }

        var workArea = displays[_displayIndex].WorkArea;

        // Check if we need to move: first time, force update, or vertical position changed
        var displayChanged = _lastWorkArea == null || _displayIndex != _lastDisplays?.Count;
        var verticalPositionChanged = _lastVerticalPosition != settings.QuickPanelVerticalPosition;
        
        if (_lastWorkArea == null || displayChanged || verticalPositionChanged || _forcePositionUpdate)
        {
            var width = 892;
            var height = 88;
            var x = workArea.X + (workArea.Width - width) / 2;
            
            var y = string.Equals(settings.QuickPanelVerticalPosition, QuickPanelVerticalPositions.Bottom, StringComparison.Ordinal)
                ? workArea.Y + workArea.Height - height - 24
                : workArea.Y + 24;
            
            System.Diagnostics.Debug.WriteLine($"[PlaceQuickPanel] Display {_displayIndex}: L={workArea.X}, T={workArea.Y}, R={workArea.X + workArea.Width}, B={workArea.Y + workArea.Height}");
            System.Diagnostics.Debug.WriteLine($"[PlaceQuickPanel] Position: X={x}, Y={y}, W={width}, H={height}");
            
            appWindow.Resize(new SizeInt32(width, height));
            appWindow.Move(new Windows.Graphics.PointInt32(x, y));
            
            _lastWorkArea = workArea;
            _lastDisplays = displays;
            _lastVerticalPosition = settings.QuickPanelVerticalPosition;
            _forcePositionUpdate = false;
        }
    }

    public static void RotateToNextDisplay(Window window, TastileSettings settings)
    {
        _forcePositionUpdate = true;
        
        var displays = PromptToastDisplayEnumerator.GetDisplays();
        if (displays.Count == 0)
        {
            return;
        }

        // Rotate to next display
        _displayIndex = (_displayIndex + 1) % displays.Count;
        
        var appWindow = GetAppWindow(window);
        if (appWindow is null)
        {
            return;
        }

        var workArea = displays[_displayIndex].WorkArea;
        var width = 892;
        var height = 88;
        var x = workArea.X + (workArea.Width - width) / 2;
        
        var y = string.Equals(settings.QuickPanelVerticalPosition, QuickPanelVerticalPositions.Bottom, StringComparison.Ordinal)
            ? workArea.Y + workArea.Height - height - 24
            : workArea.Y + 24;
        
        System.Diagnostics.Debug.WriteLine($"[RotateToNextDisplay] Display {_displayIndex}: L={workArea.X}, T={workArea.Y}");
        
        appWindow.Resize(new SizeInt32(width, height));
        appWindow.Move(new Windows.Graphics.PointInt32(x, y));
        
        _lastWorkArea = workArea;
        _lastDisplays = displays;
        _lastVerticalPosition = settings.QuickPanelVerticalPosition;
    }

    public static void ForcePositionUpdate(Window window, TastileSettings settings)
    {
        _forcePositionUpdate = true;
        PlaceQuickPanel(window, settings);
    }
    
    // PowerToys方式: GetMonitorInfoを使用してワークエリアを取得
    private static RECT GetMonitorWorkArea(IntPtr hwnd)
    {
        // ウィンドウがあるモニターを取得
        var hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        
        // MONITORINFOを取得
        var mi = new MONITORINFO();
        mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        
        if (GetMonitorInfo(hMonitor, ref mi))
        {
            return mi.rcWork; // ワークエリア（タスクバー除く）
        }
        
        // フォールバック: プライマリディスプレイ
        return new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
    }

    public static void CenterOnQuickPanelDisplay(Window window, TastileSettings settings)
    {
        var appWindow = GetAppWindow(window);
        if (appWindow is null)
        {
            return;
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var displays = PromptToastDisplayEnumerator.GetDisplays();
        var preferredDisplayId = string.Equals(settings.PromptToastDisplayMode, PromptToastDisplayModes.ActiveWindowDisplay, StringComparison.Ordinal)
            ? PromptToastForegroundDisplayResolver.GetCurrentDisplayId(displays)
            : null;
        var fallback = displays.FirstOrDefault(static display => display.IsPrimary)?.WorkArea ?? new RectInt32(0, 0, 1920, 1080);
        var workArea = QuickPanelPlacementResolver.ResolveWorkArea(displays, settings.PromptToastDisplayMode, preferredDisplayId, fallback);

        if (!GetWindowRect(hwnd, out var rect))
        {
            return;
        }

        var width = Math.Max(1, rect.Right - rect.Left);
        var height = Math.Max(1, rect.Bottom - rect.Top);
        var x = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
        var y = workArea.Y + Math.Max(24, (workArea.Height - height) / 5);
        appWindow.Move(new PointInt32(x, y));
    }

    public static bool ForceShow(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        ShowWindow(hwnd, SwRestore);
        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);
        return true;
    }

    public static void ApplyWindowTheme(Window window)
    {
        Register(window);
        ApplyWindowTheme(window, ThemeManager.CurrentElementTheme);
    }

    public static void RefreshOpenWindows()
    {
        for (var i = OpenWindows.Count - 1; i >= 0; i--)
        {
            if (!OpenWindows[i].TryGetTarget(out var window))
            {
                OpenWindows.RemoveAt(i);
                continue;
            }
            try
            {
                ApplyWindowTheme(window, ThemeManager.CurrentElementTheme);
                ApplyBackdrop(window);
                ApplyTitleBarTheme(window);
            }
            catch
            {
                // Window can be closed between registration and theme refresh.
                OpenWindows.RemoveAt(i);
            }
        }
    }

    private static void ApplyWindowTheme(Window window, ElementTheme theme)
    {
        if (window.Content is not FrameworkElement root)
        {
            return;
        }

        root.RequestedTheme = theme;
    }

    private static void ApplyBackdrop(Window window)
    {
        try
        {
            if (ThemeManager.CurrentSnapshot.HighContrastEnabled || !ThemeManager.CurrentSnapshot.TransparencyEnabled)
            {
                window.SystemBackdrop = null;
                return;
            }

            window.SystemBackdrop = new MicaBackdrop();
        }
        catch
        {
            window.SystemBackdrop = null;
        }
    }

    private static void ApplyTitleBarTheme(Window window)
    {
        var appWindow = GetAppWindow(window);
        if (appWindow is null)
        {
            return;
        }

        var chrome = appWindow.TitleBar;
        chrome.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
        chrome.ForegroundColor = ThemeManager.GetColor("AppForegroundBrush");
        chrome.InactiveBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        chrome.InactiveForegroundColor = ThemeManager.GetColor("AppForegroundMutedBrush");
        chrome.ButtonBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        chrome.ButtonInactiveBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        chrome.ButtonForegroundColor = ThemeManager.GetColor("AppForegroundBrush");
        chrome.ButtonInactiveForegroundColor = ThemeManager.GetColor("AppForegroundMutedBrush");
        chrome.ButtonHoverBackgroundColor = ThemeManager.GetColor("AppSurface2Brush");
        chrome.ButtonHoverForegroundColor = ThemeManager.GetColor("AppForegroundBrush");
        chrome.ButtonPressedBackgroundColor = ThemeManager.GetColor("AppSurface1Brush");
        chrome.ButtonPressedForegroundColor = ThemeManager.GetColor("AppForegroundBrush");
    }

    public static bool SetAlwaysOnTop(Window window, bool alwaysOnTop)
    {
        var appWindow = GetAppWindow(window);
        if (appWindow?.Presenter is not OverlappedPresenter presenter)
        {
            return false;
        }

        presenter.IsAlwaysOnTop = alwaysOnTop;
        ReassertAlwaysOnTop(window, alwaysOnTop);
        return presenter.IsAlwaysOnTop;
    }

    public static void ReassertAlwaysOnTop(Window window, bool alwaysOnTop)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        _ = SetWindowPos(
            hwnd,
            alwaysOnTop ? HwndTopMost : HwndNoTopMost,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    public static bool IsTopMost(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var exStyle = GetWindowLongPtrCompat(hwnd, GwlExStyle).ToInt64();
        return (exStyle & WsExTopMost) != 0;
    }

    private static AppWindow? GetAppWindow(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
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

    private static void EnablePanelTransparency(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        // WS_EX_NOREDIRECTIONBITMAP: DirectComposition経由で描画し、黒背景を排除
        var exStyle = GetWindowLongPtrCompat(hwnd, GwlExStyle).ToInt64();
        exStyle |= WsExNoRedirectionBitmap;
        _ = SetWindowLongPtrCompat(hwnd, GwlExStyle, new IntPtr(exStyle));

        // DWM_BLURBEHIND で完全透過を強制（黒背景の最終手段）
        var blurBehind = new DWM_BLURBEHIND
        {
            dwFlags = DWM_BB_ENABLE | DWM_BB_BLURREGION,
            fEnable = true,
            hRgnBlur = IntPtr.Zero,
            fTransitionOnMaximized = false,
        };
        _ = DwmEnableBlurBehindWindow(hwnd, ref blurBehind);
    }

    private static void ApplyPanelChrome(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        // DWM角丸を有効化（Round = 8px）
        var cornerPreference = DwmWindowCornerPreferenceRound;
        _ = DwmSetWindowAttribute(hwnd, DwmWindowCornerPreferenceAttribute, ref cornerPreference, sizeof(uint));

        // DWMボーダーを完全に無効化
        var borderColor = DwmColorNone;
        _ = DwmSetWindowAttribute(hwnd, DwmBorderColorAttribute, ref borderColor, sizeof(uint));
    }

    private static void HideFromTaskbar(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        // WS_EX_TOOLWINDOW を追加してタスクバーから隠す
        var exStyle = GetWindowLongPtrCompat(hwnd, GwlExStyle).ToInt64();
        exStyle |= WsExToolWindow;
        exStyle &= ~WsExAppWindow; // WS_EX_APPWINDOW を削除
        _ = SetWindowLongPtrCompat(hwnd, GwlExStyle, new IntPtr(exStyle));
    }

    private static void StripPanelWindowStyles(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLongPtrCompat(hwnd, GwlStyle).ToInt64();
        style &= ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu | WsBorder | WsDlgFrame);
        _ = SetWindowLongPtrCompat(hwnd, GwlStyle, new IntPtr(style));

        var exStyle = GetWindowLongPtrCompat(hwnd, GwlExStyle).ToInt64();
        exStyle &= ~(WsExWindowEdge | WsExClientEdge | WsExDlgModalFrame);
        _ = SetWindowLongPtrCompat(hwnd, GwlExStyle, new IntPtr(exStyle));

        _ = SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged | SwpNoActivate);
    }

    private static IntPtr GetWindowLongPtrCompat(IntPtr hwnd, int index)
        => IntPtr.Size == 8 ? GetWindowLongPtr(hwnd, index) : new IntPtr(GetWindowLong(hwnd, index));

    private static IntPtr SetWindowLongPtrCompat(IntPtr hwnd, int index, IntPtr newLong)
        => IntPtr.Size == 8 ? SetWindowLongPtr(hwnd, index, newLong) : new IntPtr(SetWindowLong(hwnd, index, newLong.ToInt32()));

    private const uint DwmWindowCornerPreferenceAttribute = 33;
    private const uint DwmBorderColorAttribute = 34;
    private const uint DwmWindowCornerPreferenceRound = 2;        // 8px (Snap Assist size)
    private const uint DwmWindowCornerPreferenceRoundSmall = 3;  // 4px
    private const uint DwmWindowCornerPreferenceDoNotRound = 1;
    private const uint DwmColorNone = 0xFFFFFFFE;
    
    // Monitor API constants
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const long WsSysMenu = 0x00080000L;
    private const long WsBorder = 0x00800000L;
    private const long WsDlgFrame = 0x00400000L;
    private const long WsExDlgModalFrame = 0x00000001L;
    private const long WsExWindowEdge = 0x00000100L;
    private const long WsExClientEdge = 0x00000200L;
    private const long WsExNoRedirectionBitmap = 0x00200000L;
    private const long WsExTopMost = 0x00000008L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExAppWindow = 0x00040000L;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNoActivate = 0x0010;
    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);
    private const int SwRestore = 9;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attribute, ref uint attributeValue, uint attributeSize);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmEnableBlurBehindWindow(IntPtr hwnd, ref DWM_BLURBEHIND pBlurBehind);

    private const uint DWM_BB_ENABLE = 0x00000001;
    private const uint DWM_BB_BLURREGION = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_BLURBEHIND
    {
        public uint dwFlags;
        public bool fEnable;
        public IntPtr hRgnBlur;
        public bool fTransitionOnMaximized;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr newLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool BringWindowToTop(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
