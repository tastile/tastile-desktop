namespace TastileDesktop.Services;

public static class QuickPanelActionVisualResolver
{
    public static string ResolveGlyph(string? actionId) => actionId switch
    {
        "start-next" => "\uE768",
        "complete" => "\uE73E",
        "break" => "\uE103",
        "resume" => "\uE768",
        "refresh" => "\uE72C",
        "add-tile" => "\uE710",
        "toggle-pin" => "\uE718",
        _ => "\uE712"
    };

    public static string ResolveToolTip(string? actionId) => actionId switch
    {
        "start-next" => "Start next tile",
        "complete" => "Complete tile",
        "break" => "Take a break",
        "resume" => "End break",
        "refresh" => "Refresh",
        "add-tile" => "Add new tile",
        "toggle-pin" => "Pin window",
        _ => "Action"
    };

    public static (string Glyph, string ToolTip) Resolve(string? actionId)
    {
        return (ResolveGlyph(actionId), ResolveToolTip(actionId));
    }
}
