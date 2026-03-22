namespace TastileDesktop.Services;

public sealed record NativeQuickPanelAction(string Id, string Glyph);

public sealed record NativeQuickPanelSnapshot(
    string? LeadingText,
    string? Title,
    string StatusKind,
    bool PromptWaiting,
    bool ShowProgress,
    double ProgressPercent,
    List<NativeQuickPanelAction> Actions);

public static class NativeQuickPanelStatusKind
{
    public const string Offline = "offline";
    public const string Break = "break";
    public const string Working = "working";
    public const string Ready = "ready";
}

public sealed record QuickPanelBounds(int X, int Y, int Width, int Height)
{
    public static implicit operator QuickPanelBounds(Windows.Graphics.RectInt32 r) => new(r.X, r.Y, r.Width, r.Height);
    public static implicit operator Windows.Graphics.RectInt32(QuickPanelBounds b) => new(b.X, b.Y, b.Width, b.Height);
}
