using TastileDesktop.Models;

namespace TastileDesktop.Services;

public sealed record CreateTileWindowTextContract(
    string WindowTitle,
    string HeadingText,
    string PrimaryButtonText);

public sealed record CreateTileDurationContract(
    int? Hours,
    int? Minutes);

public sealed record CreateTileWorkflowTextContract(
    string Label,
    string Description,
    string HeadingCreate,
    string HeadingEdit);

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

    public static CreateTileWorkflowKind ResolveWorkflowKind(
        string? tileKind,
        string? objectiveMode,
        bool? fixedStart,
        bool? fixedEnd)
    {
        if (string.Equals(objectiveMode, "recurring", StringComparison.Ordinal)
            || string.Equals(tileKind, "recurring", StringComparison.Ordinal))
        {
            return CreateTileWorkflowKind.Recurring;
        }

        if (string.Equals(tileKind, "label", StringComparison.Ordinal))
        {
            return CreateTileWorkflowKind.Task;
        }

        // Both fixed_start and fixed_end present ⇒ bounded event window.
        var hasFixedWindow = fixedStart == true && fixedEnd == true;
        return hasFixedWindow ? CreateTileWorkflowKind.Event : CreateTileWorkflowKind.Task;
    }

    public static CreateTileWorkflowTextContract ResolveWorkflowText(
        CreateTileWorkflowKind kind,
        bool isJapanese)
    {
        return kind switch
        {
            CreateTileWorkflowKind.Event => new CreateTileWorkflowTextContract(
                Label: isJapanese ? "イベント" : "Event",
                Description: isJapanese ? "開始と終了が固定された予定" : "Scheduled block of time",
                HeadingCreate: isJapanese ? "イベントを作成" : "Create event",
                HeadingEdit: isJapanese ? "イベントを編集" : "Edit event"),
            CreateTileWorkflowKind.Task => new CreateTileWorkflowTextContract(
                Label: isJapanese ? "タスク" : "Task",
                Description: isJapanese ? "実行して完了する作業" : "Work to be done",
                HeadingCreate: isJapanese ? "タスクを作成" : "Create task",
                HeadingEdit: isJapanese ? "タスクを編集" : "Edit task"),
            CreateTileWorkflowKind.Recurring => new CreateTileWorkflowTextContract(
                Label: isJapanese ? "繰り返し" : "Recurring",
                Description: isJapanese ? "定期的に実行する作業" : "Repeating schedule",
                HeadingCreate: isJapanese ? "繰り返しを作成" : "Create recurring",
                HeadingEdit: isJapanese ? "繰り返しを編集" : "Edit recurring"),
            _ => new CreateTileWorkflowTextContract(
                Label: isJapanese ? "詳細" : "Detailed",
                Description: isJapanese ? "全フィールドを直接編集" : "Edit every field directly",
                HeadingCreate: isJapanese ? "詳細で作成" : "Create (detailed)",
                HeadingEdit: isJapanese ? "詳細で編集" : "Edit (detailed)"),
        };
    }
}
