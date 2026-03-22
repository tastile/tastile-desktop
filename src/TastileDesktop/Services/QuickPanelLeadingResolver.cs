namespace TastileDesktop.Services;

public static class QuickPanelLeadingResolver
{
    public static string Resolve(
        bool isConnected,
        bool isWorking,
        bool isOnBreak,
        int readyCount,
        string? workRemaining,
        string? breakRemaining)
    {
        if (!isConnected) return "--";
        if (isWorking) return workRemaining ?? "--";
        if (isOnBreak) return breakRemaining ?? "--";
        return readyCount.ToString();
    }
}
