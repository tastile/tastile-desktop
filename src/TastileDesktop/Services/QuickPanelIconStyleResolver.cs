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
            QuickPanelActionRole.PrimaryCreation => new QuickPanelIconStyle("TextFillColorPrimaryBrush"),
            _ => new QuickPanelIconStyle("TextFillColorTertiaryBrush")
        };
    }
}
