using System.Runtime.InteropServices;
using Windows.Graphics;

namespace TastileDesktop.Services;

public static class PromptToastDisplayEnumerator
{
    public static IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var displays = new List<DisplayInfo>();
        var index = 0;
        _ = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MONITORINFOEX();
            info.cbSize = Marshal.SizeOf<MONITORINFOEX>();
            if (!GetMonitorInfo(monitor, ref info))
            {
                return true;
            }

            var work = info.rcWork;
            var width = Math.Max(1, work.Right - work.Left);
            var height = Math.Max(1, work.Bottom - work.Top);
            var id = string.IsNullOrWhiteSpace(info.szDevice) ? $"display-{index}" : info.szDevice;
            var isPrimary = (info.dwFlags & MonitorInfofPrimary) != 0;

            displays.Add(new DisplayInfo(
                id,
                isPrimary,
                new RectInt32(work.Left, work.Top, width, height)));

            index++;
            return true;
        }, IntPtr.Zero);

        if (displays.Count == 0)
        {
            displays.Add(new DisplayInfo("primary", true, new RectInt32(0, 0, 1920, 1080)));
        }

        return displays;
    }

    private const uint MonitorInfofPrimary = 0x00000001;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

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

public sealed record DisplayInfo(string Id, bool IsPrimary, RectInt32 WorkArea);
