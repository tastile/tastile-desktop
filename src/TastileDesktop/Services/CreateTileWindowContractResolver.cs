namespace TastileDesktop.Services;

public sealed record CreateTileWindowTextContract(
    string WindowTitle,
    string HeadingText,
    string PrimaryButtonText);

public sealed record CreateTileDurationContract(
    int? Hours,
    int? Minutes);

public static class CreateTileWindowContractResolver
{
    public static CreateTileWindowTextContract ResolveWindowText(bool isEditMode, bool isJapanese)
    {
        if (isEditMode)
        {
            return new CreateTileWindowTextContract(
                WindowTitle: "Edit Tile",
                HeadingText: "Edit Tile",
                PrimaryButtonText: isJapanese ? "保存" : "Save");
        }

        return new CreateTileWindowTextContract(
            WindowTitle: "Create Tile",
            HeadingText: "Create Tile",
            PrimaryButtonText: isJapanese ? "作成" : "Create");
    }

    public static bool ShouldClearSuggestedTitleOnFirstFocus(
        string? currentTitle,
        string? suggestedTitle,
        bool titleEdited,
        bool alreadyClearedOnFocus)
    {
        if (alreadyClearedOnFocus || titleEdited)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(currentTitle) || string.IsNullOrWhiteSpace(suggestedTitle))
        {
            return false;
        }
        return string.Equals(currentTitle.Trim(), suggestedTitle.Trim(), StringComparison.CurrentCulture);
    }

    public static bool ShouldShowBaseTimingPanel(string objectiveMode)
    {
        return !string.Equals(objectiveMode, "recurring", StringComparison.Ordinal);
    }

    public static CreateTileDurationContract ResolveDurationUpdate(
        int? autoDurationMinutes,
        bool durationManuallyEdited)
    {
        if (durationManuallyEdited || !autoDurationMinutes.HasValue || autoDurationMinutes.Value <= 0)
        {
            return new CreateTileDurationContract(Hours: null, Minutes: null);
        }

        var total = autoDurationMinutes.Value;
        return new CreateTileDurationContract(
            Hours: total / 60,
            Minutes: total % 60);
    }
}
