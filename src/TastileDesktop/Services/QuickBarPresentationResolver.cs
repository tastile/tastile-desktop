namespace TastileDesktop.Services;

public sealed record QuickBarPresentation(
    string Title,
    string? Subtitle,
    double? ProgressValue,
    bool ShowProgress,
    string Status = "",
    string Meta = "");

public static class QuickBarPresentationResolver
{
    public static QuickBarPresentation Resolve(string status, string title)
    {
        return new QuickBarPresentation(title, null, null, false);
    }
    
    public static QuickBarPresentation Resolve(
        bool isConnected,
        bool isWorking,
        bool isOnBreak,
        string? activeTitle,
        string? activeNextAction,
        string? nextUpTitle,
        string? nextUpAction,
        string? workElapsedText,
        string? breakRemainingText,
        bool hasPendingPrompt)
    {
        return new QuickBarPresentation(activeTitle ?? "Ready", null, null, false, "", "");
    }
}
