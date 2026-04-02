using TastileDesktop.Models;

namespace TastileDesktop.Services;

public static class CreateTileParityResolver
{
    public static Func<bool> IsJapaneseFunc { get; set; } = () => System.Globalization.CultureInfo.CurrentCulture.Name.StartsWith("ja");

    public static bool IsJapanese() => IsJapaneseFunc();

    public static int? GetAutoDurationMinutes(CreateTileDraft draft)
    {
        var manual = GetWorkTargetMinutes(draft);
        if (manual > 0) return manual;

        var isRecurring = string.Equals(draft.ObjectiveMode, "recurring", StringComparison.Ordinal);
        if (isRecurring)
        {
            var recurrenceDuration = GetRecurringWindowDurationMinutes(draft);
            if (recurrenceDuration.HasValue) return recurrenceDuration.Value;
        }

        var bounded = GetBoundedDurationMinutes(draft.StartAt, draft.EndAt);
        if (bounded.HasValue) return bounded.Value;

        return null;
    }

    public static CreateTileManualAdjustGuidance GetManualAdjustGuidance(CreateTileRequest request, bool isJapanese)
    {
        var focusStart = !string.IsNullOrWhiteSpace(request.Temporal?.FixedStart);
        var focusEnd = !string.IsNullOrWhiteSpace(request.Temporal?.FixedEnd);
        if (!focusStart && !focusEnd)
        {
            focusStart = true;
        }

        string message;
        if (isJapanese)
        {
            message = focusStart && focusEnd
                ? "開始と終了の日時を手動で調整してください。"
                : focusStart
                    ? "開始日時を手動で調整してください。"
                    : "終了日時を手動で調整してください。";
        }
        else
        {
            message = focusStart && focusEnd
                ? "Please adjust start and end time manually."
                : focusStart
                    ? "Please adjust the start time manually."
                    : "Please adjust the end time manually.";
        }

        return new CreateTileManualAdjustGuidance(focusStart, focusEnd, message);
    }

    public static PromptView BuildCreateConflictToastPrompt(CreateConflictPrompt prompt, bool isJapanese)
    {
        var actions = (prompt.Options ?? [])
            .Where(static option => !string.IsNullOrWhiteSpace(option.Id))
            .Select(static option => new PromptActionView(option.Id, string.IsNullOrWhiteSpace(option.Label) ? option.Id : option.Label))
            .ToList();

        if (!actions.Any(static action => string.Equals(action.Id, "cancel_create", StringComparison.OrdinalIgnoreCase)))
        {
            actions.Add(new PromptActionView("cancel_create", isJapanese ? "作成を中止" : "Cancel create"));
        }

        var title = string.IsNullOrWhiteSpace(prompt.Title)
            ? (isJapanese ? "時間競合を検知しました" : "Time conflict detected")
            : prompt.Title;
        var message = string.IsNullOrWhiteSpace(prompt.Message)
            ? (isJapanese ? "重なりを検出しました。作成方法を選択してください。" : "Overlap detected. Choose how to proceed.")
            : prompt.Message;

        return new PromptView(
            PromptId: $"create-conflict-{Guid.NewGuid():N}",
            Kind: "create_conflict",
            Severity: "warning",
            TileId: null,
            Title: title!,
            Body: message!,
            Why: message!,
            SuggestedMinutes: null,
            Actions: actions,
            ExpiresAt: null,
            Stale: false);
    }

    public static CreateTileRequest BuildRequest(CreateTileDraft draft, bool isJapanese)
    {
        var isRecurring = string.Equals(draft.ObjectiveMode, "recurring", StringComparison.Ordinal);
        var workMinutes = GetAutoDurationMinutes(draft);
        var startAt = !isRecurring && draft.UseStartAt ? draft.StartAt : null;
        var endAt = !isRecurring && draft.UseEndAt ? draft.EndAt : null;
        var recurrenceValidFrom = isRecurring && draft.RecurrenceValidFromEnabled ? draft.RecurrenceValidFromDate : null;
        var recurrenceValidTo = isRecurring && draft.RecurrenceValidToEnabled ? draft.RecurrenceValidToDate : null;
        var recurrenceStartOffset = draft.RecurrenceUseStartAt ? ToOffsetMinutes(draft.RecurrenceStartTime) : null;
        var recurrenceEndOffset = draft.RecurrenceUseEndAt ? ToOffsetMinutes(draft.RecurrenceEndTime) : null;
        var project = NormalizeTag(draft.Project);
        var labels = BuildLabels(project, draft.Tags ?? []);
        var title = string.IsNullOrWhiteSpace(draft.Title) ? GetSuggestedTitle(draft, isJapanese) : draft.Title.Trim();

        var temporal = new CreateTileTemporalRequest(
            ReleaseAt: recurrenceValidFrom.HasValue ? ToIsoString(recurrenceValidFrom.Value.Date) : null,
            DueAt: recurrenceValidTo.HasValue ? ToIsoString(recurrenceValidTo.Value.Date.AddDays(1).AddMinutes(-1)) : null,
            FixedStart: startAt.HasValue ? ToIsoString(startAt.Value) : null,
            FixedEnd: endAt.HasValue ? ToIsoString(endAt.Value) : null,
            ActiveStart: startAt.HasValue ? ToIsoString(startAt.Value) : null,
            ActiveEnd: endAt.HasValue ? ToIsoString(endAt.Value) : null);

        var recurrence = isRecurring
            ? new CreateTileRecurrenceRequest(
                Generator: new CreateTileRecurrenceGeneratorRequest(
                    StepMin: string.Equals(draft.RecurrenceFrequency, "weekly", StringComparison.Ordinal) ? 7 * 24 * 60 : 24 * 60,
                    AnchorEpochMin: recurrenceValidFrom.HasValue ? ToEpochMinutes(recurrenceValidFrom.Value.Date) : null),
                Window: new CreateTileRecurrenceWindowRequest(
                    StartOffsetMin: recurrenceStartOffset ?? 0,
                    EndOffsetMin: recurrenceEndOffset ?? 0),
                Selector: new CreateTileRecurrenceSelectorRequest(
                    Expression: BuildRecurrenceSelectorExpression(draft)))
            : null;

        var objective = new CreateTileObjectiveRequest(
            ObjectiveMode: draft.ObjectiveMode ?? "finish_once",
            TargetWorkMin: string.Equals(draft.TileKind, "work", StringComparison.Ordinal) ? workMinutes : null,
            TargetRestMin: null,
            DoneRule: ResolveDoneRule(draft),
            Recurrence: recurrence);

        var interruption = new CreateTileInterruptionRequest(
            InterruptPenalty: 3,
            ResumePenalty: 3,
            BreakSplitsWork: draft.BreakSplitsWork,
            ExternalInterruptOnly: false);

        var automation = new CreateTileAutomationRequest(
            PromptOnStart: false,
            PromptOnEnd: true,
            AutoStartAllowed: false,
            AutoEndAllowed: false);

        var annotation = new CreateTileAnnotationRequest(
            SemanticRole: draft.TileKind ?? "work",
            Labels: labels,
            TimedLabels: []);

        return new CreateTileRequest(
            Title: title,
            NextAction: ResolveNextAction(draft, isJapanese),
            DoneDefinition: ResolveDoneDefinition(draft, isJapanese),
            Temporal: temporal,
            Objective: objective,
            Interruption: interruption,
            Automation: automation,
            Annotation: annotation,
            ConflictResolution: null);
    }

    public static string GetSuggestedTitle(CreateTileDraft draft, bool isJapanese)
    {
        var localeJa = isJapanese;
        var tileKind = draft.TileKind ?? "work";
        var objectiveMode = draft.ObjectiveMode ?? "finish_once";
        var duration = GetAutoDurationMinutes(draft);
        var durationText = duration.HasValue && duration.Value > 0 ? FormatDuration(duration.Value, localeJa) : null;
        var hasEnd = draft.UseEndAt && draft.EndAt.HasValue;
        var showFocusUntilEnd = tileKind == "work" && objectiveMode != "recurring" && hasEnd;

        if (tileKind == "label")
        {
            return localeJa ? "期間ラベル" : "Period label";
        }

        if (objectiveMode == "recurring")
        {
            if (!string.IsNullOrWhiteSpace(durationText))
            {
                return localeJa ? $"定期タスク {durationText}" : $"Recurring task {durationText}";
            }
            return localeJa ? "定期タスク" : "Recurring task";
        }

        if (objectiveMode == "maximize_within_interval" && showFocusUntilEnd)
        {
            if (draft.StartAt.HasValue && draft.EndAt.HasValue)
            {
                return localeJa
                    ? $"{FormatDateShort(draft.StartAt.Value, localeJa)} - {FormatDateShort(draft.EndAt.Value, localeJa)} で最大化"
                    : $"Maximize in {FormatDateShort(draft.StartAt.Value, localeJa)} - {FormatDateShort(draft.EndAt.Value, localeJa)}";
            }
            return localeJa ? "できる限り進める" : "Maximize progress";
        }

        if (!string.IsNullOrWhiteSpace(durationText))
        {
            return localeJa ? $"作業 {durationText}" : $"Task {durationText}";
        }

        return localeJa ? "作業タスク" : "Task";
    }

    public static CreateTileCatalog DeriveCatalog(List<TileView> tiles)
    {
        var titles = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var projects = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var tags = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

        foreach (var tile in tiles)
        {
            if (!string.IsNullOrWhiteSpace(tile.Title)) titles.Add(tile.Title.Trim());
            foreach (var label in tile.Labels ?? [])
            {
                if (string.IsNullOrWhiteSpace(label)) continue;
                var normalized = label.Trim();
                if (normalized.StartsWith("project:", StringComparison.OrdinalIgnoreCase))
                {
                    var project = normalized["project:".Length..].Trim();
                    if (!string.IsNullOrWhiteSpace(project)) projects.Add(project);
                    continue;
                }
                tags.Add(normalized);
            }
        }

        return new CreateTileCatalog(
            projects.OrderBy(static value => value, StringComparer.CurrentCultureIgnoreCase).ToList(),
            titles.OrderBy(static value => value, StringComparer.CurrentCultureIgnoreCase).ToList(),
            tags.OrderBy(static value => value, StringComparer.CurrentCultureIgnoreCase).ToList());
    }

    private static int GetWorkTargetMinutes(CreateTileDraft draft)
    {
        return Math.Max(0, draft.WorkHours ?? 0) * 60 + Math.Max(0, draft.WorkMinutes ?? 0);
    }

    private static int? GetBoundedDurationMinutes(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (!start.HasValue || !end.HasValue) return null;
        var diff = (int)Math.Floor((end.Value - start.Value).TotalMinutes);
        return diff > 0 ? diff : null;
    }

    private static int? GetRecurringWindowDurationMinutes(CreateTileDraft draft)
    {
        if (!draft.RecurrenceUseStartAt || !draft.RecurrenceUseEndAt) return null;
        var startOffset = ToOffsetMinutes(draft.RecurrenceStartTime);
        var endOffset = ToOffsetMinutes(draft.RecurrenceEndTime);
        if (!startOffset.HasValue || !endOffset.HasValue || endOffset <= startOffset) return null;
        return endOffset.Value - startOffset.Value;
    }

    private static int? ToOffsetMinutes(TimeSpan? time)
    {
        return time.HasValue ? (int)time.Value.TotalMinutes : null;
    }

    private static string BuildRecurrenceSelectorExpression(CreateTileDraft draft)
    {
        var frequency = draft.RecurrenceFrequency ?? "daily";
        var interval = Math.Max(1, draft.RecurrenceInterval ?? 1);
        if (frequency == "daily")
        {
            return $"freq=daily;interval={interval}";
        }
        if (frequency == "weekly")
        {
            var weekdays = draft.RecurrenceWeekdays is { Count: > 0 }
                ? draft.RecurrenceWeekdays
                : [1];
            return $"freq=weekly;interval={interval};weekdays={string.Join(",", weekdays.OrderBy(static v => v))}";
        }

        var week = Math.Max(1, draft.RecurrenceMonthlyWeek ?? 1);
        var weekday = Math.Clamp(draft.RecurrenceMonthlyWeekday ?? 0, 0, 6);
        return $"freq=monthly;interval={interval};week={week};weekday={weekday}";
    }

    private static List<string> BuildLabels(string projectInput, List<string> selectedTags)
    {
        var labels = new List<string>();
        if (!string.IsNullOrWhiteSpace(projectInput))
        {
            labels.Add($"project:{projectInput}");
        }
        foreach (var tag in selectedTags.Select(NormalizeTag).Where(static tag => !string.IsNullOrWhiteSpace(tag)))
        {
            if (!labels.Any(existing => string.Equals(existing, tag, StringComparison.CurrentCultureIgnoreCase)))
            {
                labels.Add(tag);
            }
        }
        return labels;
    }

    private static string ResolveDoneDefinition(CreateTileDraft draft, bool isJapanese)
    {
        var tileKind = draft.TileKind ?? "work";
        var objectiveMode = draft.ObjectiveMode ?? "finish_once";
        var duration = GetAutoDurationMinutes(draft);
        var durationText = duration.HasValue && duration.Value > 0 ? FormatDuration(duration.Value, isJapanese) : null;

        if (tileKind == "label")
        {
            return isJapanese ? "指定した期間のラベル付けを完了" : "Complete labeling for the selected period";
        }

        if (objectiveMode == "recurring")
        {
            return isJapanese ? "1サイクル実行したら完了（定期）" : "Complete one cycle (recurring)";
        }

        if (objectiveMode == "maximize_within_interval")
        {
            if (draft.StartAt.HasValue && draft.EndAt.HasValue)
            {
                return isJapanese
                    ? $"{FormatDateShort(draft.StartAt.Value, isJapanese)} から {FormatDateShort(draft.EndAt.Value, isJapanese)} の間で最大化"
                    : $"Maximize progress from {FormatDateShort(draft.StartAt.Value, isJapanese)} to {FormatDateShort(draft.EndAt.Value, isJapanese)}";
            }
            return isJapanese ? "できる限り進める" : "Maximize progress";
        }

        if (!string.IsNullOrWhiteSpace(durationText))
        {
            return isJapanese ? $"{durationText}の実行を完了" : $"Complete {durationText} of work";
        }
        return isJapanese ? "1回の実行を完了" : "Complete one run";
    }

    private static string ResolveNextAction(CreateTileDraft draft, bool isJapanese)
    {
        if (!string.IsNullOrWhiteSpace(draft.Memo))
        {
            return draft.Memo!.Trim();
        }
        var tileKind = draft.TileKind ?? "work";
        if (tileKind == "label")
        {
            return isJapanese ? "この期間にラベルを適用" : "Apply this label within the selected period";
        }
        return isJapanese ? "開始して最初の1手を実行" : "Start and execute the first step";
    }

    private static string? ResolveDoneRule(CreateTileDraft draft)
    {
        if (string.Equals(draft.ObjectiveMode, "maximize_within_interval", StringComparison.Ordinal))
        {
            return "interval_end";
        }

        if (string.Equals(draft.ObjectiveMode, "recurring", StringComparison.Ordinal)
            && draft.RecurrenceUseEndAt
            && draft.RecurrenceEndTime.HasValue)
        {
            return "time_reached";
        }

        return "manual";
    }

    private static string NormalizeTag(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string FormatDuration(int totalMinutes, bool isJapanese)
    {
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        if (isJapanese)
        {
            if (hours > 0 && minutes > 0) return $"{hours}時間{minutes}分";
            if (hours > 0) return $"{hours}時間";
            return $"{minutes}分";
        }

        if (hours > 0 && minutes > 0) return $"{hours}h {minutes}m";
        if (hours > 0) return $"{hours}h";
        return $"{minutes}m";
    }

    private static string FormatDateShort(DateTimeOffset date, bool isJapanese)
    {
        var culture = isJapanese ? "ja-JP" : "en-US";
        return date.ToString("M/d HH:mm", System.Globalization.CultureInfo.GetCultureInfo(culture));
    }

    private static long ToEpochMinutes(DateTimeOffset value)
    {
        return (long)Math.Floor(value.ToUnixTimeSeconds() / 60d);
    }

    private static string ToIsoString(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O");
    }
}
