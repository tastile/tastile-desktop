namespace TastileDesktop.Services;

public sealed record QuickPanelActionState(
    string? PrimaryActionId,
    string? PrimaryLabel,
    string? SecondaryActionId,
    string? SecondaryLabel,
    string Hint);

public static class QuickPanelActionResolver
{
    public static QuickPanelActionState Resolve(
        bool isConnected,
        bool hasPendingPrompt,
        bool isWorking,
        bool isOnBreak,
        bool hasNextTile)
    {
        if (!isConnected)
            return new QuickPanelActionState(null, null, null, null, "Daemon offline");

        if (isOnBreak)
            return new QuickPanelActionState("resume", "Resume", null, null, "");

        if (isWorking)
        {
            if (hasPendingPrompt)
                return new QuickPanelActionState("complete", "Complete", "break", "Break", "Prompt waiting");
            return new QuickPanelActionState("complete", "Complete", "break", "Break", "");
        }

        if (hasNextTile)
            return new QuickPanelActionState("start-next", "Start", null, null, "");

        return new QuickPanelActionState("refresh", "Refresh", null, null, "");
    }
}
