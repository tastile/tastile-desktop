namespace TastileDesktop.Services;

public enum QuickPanelActionRole
{
    PrimaryCreation,
    SecondaryUtility
}

public sealed record QuickPanelIconStyle(string ForegroundBrushKey);

public static class QuickPanelIconStyleResolver
{
    public static QuickPanelIconStyle Resolve(QuickPanelActionRole role)
    {
        return role switch
        {
            QuickPanelActionRole.PrimaryCreation => new QuickPanelIconStyle("PrimaryForegroundBrush"),
            _ => new QuickPanelIconStyle("TertiaryForegroundBrush")
        };
    }
}
