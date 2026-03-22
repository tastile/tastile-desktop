using System.Runtime.InteropServices;

namespace TastileDesktop.Services;

public static class PromptToastForegroundDisplayResolver
{
    public static string? GetCurrentDisplayId(IReadOnlyList<DisplayInfo> displays)
    {
        if (displays.Count == 0)
        {
            return null;
        }

        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return displays.FirstOrDefault(d => d.IsPrimary)?.Id ?? displays[0].Id;
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return displays.FirstOrDefault(d => d.IsPrimary)?.Id ?? displays[0].Id;
        }

        var info = new MONITORINFOEX();
        info.cbSize = Marshal.SizeOf<MONITORINFOEX>();
        if (!GetMonitorInfo(monitor, ref info) || string.IsNullOrWhiteSpace(info.szDevice))
        {
            return displays.FirstOrDefault(d => d.IsPrimary)?.Id ?? displays[0].Id;
        }

        var matched = displays.FirstOrDefault(d => string.Equals(d.Id, info.szDevice, StringComparison.OrdinalIgnoreCase));
        return matched?.Id ?? displays.FirstOrDefault(d => d.IsPrimary)?.Id ?? displays[0].Id;
    }

    private const uint MonitorDefaultToNearest = 0x00000002;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
