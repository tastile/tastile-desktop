using Windows.Graphics;

namespace TastileDesktop.Services;

public static class QuickPanelPlacementResolver
{
    public static RectInt32 ResolveWorkArea(
        IReadOnlyList<DisplayInfo> displays,
        string displayMode,
        string? preferredDisplayId,
        RectInt32 fallback)
    {
        var display = displays.FirstOrDefault(d => d.Id == preferredDisplayId)
            ?? displays.FirstOrDefault(d => d.IsPrimary)
            ?? displays.FirstOrDefault();
        return display?.WorkArea ?? fallback;
    }

    // Windows 11 Snap Assist UIに一致：892×88px、画面上から24px
    public static RectInt32 ComputeBounds(RectInt32 workArea, string anchor, string orientation, int topMargin = 24)
    {
        var width = 892;
        var height = 88;
        // 中央揃え（ディスプレイの中心に配置）- workArea.Xを起点に計算
        var x = workArea.X + ((workArea.Width - width) / 2);
        // 上から24pxの位置
        var y = workArea.Y + topMargin;
        
        // デバッグ出力
        System.Diagnostics.Debug.WriteLine($"[QuickPanelPlacement] workArea: X={workArea.X}, Y={workArea.Y}, W={workArea.Width}, H={workArea.Height}");
        System.Diagnostics.Debug.WriteLine($"[QuickPanelPlacement] computed: X={x}, Y={y}, W={width}, H={height}");
        
        return new RectInt32(x, y, width, height);
    }
}

// QuickPanelAnchors and QuickPanelOrientations are defined in QuickPanelAnchors.cs and QuickPanelOrientations.cs
