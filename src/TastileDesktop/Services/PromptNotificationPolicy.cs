using TastileDesktop.Models;

namespace TastileDesktop.Services;

public sealed record Decision(bool ShowToast, bool ShowIntervention);

public static class PromptNotificationPolicy
{
    private static readonly string[] LabelTileTitles =
    [
        "期間ラベル",
        "Period label",
    ];

    public static string Decide(string context)
    {
        return "Show";
    }
    
    public static Decision Decide(PromptView? prompt, bool isFullscreen)
    {
        if (prompt == null)
        {
            return new Decision(ShowToast: false, ShowIntervention: false);
        }

        if (IsLabelTile(prompt))
        {
            return new Decision(ShowToast: false, ShowIntervention: false);
        }

        return new Decision(ShowToast: true, ShowIntervention: false);
    }

    private static bool IsLabelTile(PromptView prompt)
    {
        return LabelTileTitles.Any(t => string.Equals(prompt.Title, t, StringComparison.OrdinalIgnoreCase));
    }
}
