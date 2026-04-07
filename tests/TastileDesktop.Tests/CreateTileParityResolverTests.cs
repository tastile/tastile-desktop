using TastileDesktop.Models;
using TastileDesktop.Services;
using Xunit;

namespace TastileDesktop.Tests;

public sealed class CreateTileParityResolverTests
{
    [Fact]
    public void BuildRequest_MapsLabelKind_ToSelectedObjectiveMode()
    {
        var draft = new CreateTileDraft(
            Title: "Label tile",
            TileKind: "label",
            ObjectiveMode: "finish_once",
            UseStartAt: true,
            UseEndAt: true,
            StartAt: DateTimeOffset.Parse("2026-04-07T09:00:00+09:00"),
            EndAt: DateTimeOffset.Parse("2026-04-07T10:00:00+09:00"));

        var request = CreateTileParityResolver.BuildRequest(draft, isJapanese: true);

        Assert.NotNull(request.Objective);
        Assert.Equal("finish_once", request.Objective!.ObjectiveMode);
        Assert.Equal("manual", request.Objective.DoneRule);
        Assert.Null(request.Objective.TargetWorkMin);
        Assert.NotNull(request.Temporal);
        Assert.NotNull(request.Temporal!.ActiveStart);
        Assert.NotNull(request.Temporal.ActiveEnd);
        Assert.NotNull(request.Annotation);
        Assert.Equal("label", request.Annotation!.SemanticRole);
    }

    [Fact]
    public void BuildRequest_MapsRecurringLabel_ToRecurringObjective()
    {
        var draft = new CreateTileDraft(
            Title: "Recurring label",
            TileKind: "label",
            ObjectiveMode: "recurring",
            RecurrenceFrequency: "daily",
            RecurrenceInterval: 1,
            RecurrenceUseStartAt: true,
            RecurrenceUseEndAt: true,
            RecurrenceStartTime: TimeSpan.FromHours(9),
            RecurrenceEndTime: TimeSpan.FromHours(10));

        var request = CreateTileParityResolver.BuildRequest(draft, isJapanese: true);

        Assert.NotNull(request.Objective);
        Assert.Equal("recurring", request.Objective!.ObjectiveMode);
        Assert.NotNull(request.Objective.Recurrence);
        Assert.Equal(9 * 60, request.Objective.Recurrence!.Window.StartOffsetMin);
        Assert.Equal(10 * 60, request.Objective.Recurrence.Window.EndOffsetMin);
        Assert.NotNull(request.Annotation);
        Assert.Equal("label", request.Annotation!.SemanticRole);
    }

    [Fact]
    public void BuildRequest_UsesManualDuration_ForNonRecurringTile_EvenIfRecurringWindowDefaultsExist()
    {
        var draft = new CreateTileDraft(
            Title: "Long Task",
            TileKind: "work",
            ObjectiveMode: "finish_once",
            WorkHours: 25,
            WorkMinutes: 30,
            RecurrenceUseStartAt: true,
            RecurrenceUseEndAt: true,
            RecurrenceStartTime: TimeSpan.FromHours(9),
            RecurrenceEndTime: TimeSpan.FromHours(10));

        var request = CreateTileParityResolver.BuildRequest(draft, isJapanese: true);

        Assert.NotNull(request.Objective);
        Assert.Equal(1530, request.Objective!.TargetWorkMin);
    }

    [Fact]
    public void BuildRequest_PreservesManualDuration_WhenFixedWindowIsLonger()
    {
        var draft = new CreateTileDraft(
            Title: "Windowed task",
            TileKind: "work",
            ObjectiveMode: "finish_once",
            UseStartAt: true,
            UseEndAt: true,
            StartAt: DateTimeOffset.Parse("2026-04-02T09:00:00+09:00"),
            EndAt: DateTimeOffset.Parse("2026-04-02T12:00:00+09:00"),
            WorkHours: 0,
            WorkMinutes: 25,
            DurationManuallyEdited: true);

        var request = CreateTileParityResolver.BuildRequest(draft, isJapanese: true);

        Assert.NotNull(request.Objective);
        Assert.Equal(25, request.Objective!.TargetWorkMin);
    }

    [Fact]
    public void BuildRequest_PreservesManualDuration_ForRecurringTile_WhenWindowIsWider()
    {
        var draft = new CreateTileDraft(
            Title: "Recurring manual",
            TileKind: "work",
            ObjectiveMode: "recurring",
            RecurrenceUseStartAt: true,
            RecurrenceUseEndAt: true,
            RecurrenceStartTime: TimeSpan.FromHours(9),
            RecurrenceEndTime: TimeSpan.FromHours(11),
            WorkHours: 0,
            WorkMinutes: 30,
            DurationManuallyEdited: true);

        var request = CreateTileParityResolver.BuildRequest(draft, isJapanese: true);

        Assert.NotNull(request.Objective);
        Assert.Equal(30, request.Objective!.TargetWorkMin);
    }

    [Fact]
    public void BuildRequest_UsesRecurringWindowDuration_WhenManualDurationIsNotEntered()
    {
        var draft = new CreateTileDraft(
            Title: "Recurring auto",
            TileKind: "work",
            ObjectiveMode: "recurring",
            RecurrenceUseStartAt: true,
            RecurrenceUseEndAt: true,
            RecurrenceStartTime: TimeSpan.FromHours(9),
            RecurrenceEndTime: TimeSpan.FromHours(10),
            WorkHours: 0,
            WorkMinutes: 0);

        var request = CreateTileParityResolver.BuildRequest(draft, isJapanese: true);

        Assert.NotNull(request.Objective);
        Assert.Equal(60, request.Objective!.TargetWorkMin);
    }

    [Fact]
    public void GetManualAdjustGuidance_FocusesBothBounds_WhenFixedStartAndEndExist()
    {
        var request = new CreateTileRequest(
            Title: "Fixed",
            NextAction: null,
            DoneDefinition: null,
            Temporal: new CreateTileTemporalRequest(
                ReleaseAt: null,
                DueAt: null,
                FixedStart: "2026-03-25T09:00:00Z",
                FixedEnd: "2026-03-25T10:00:00Z",
                ActiveStart: null,
                ActiveEnd: null),
            Objective: null,
            Interruption: null,
            Automation: null,
            Annotation: null,
            ConflictResolution: null);

        var guidance = CreateTileParityResolver.GetManualAdjustGuidance(request, isJapanese: true);

        Assert.True(guidance.FocusStart);
        Assert.True(guidance.FocusEnd);
        Assert.Contains("開始", guidance.Message);
        Assert.Contains("終了", guidance.Message);
    }

    [Fact]
    public void GetManualAdjustGuidance_DefaultsToStartFocus_WhenNoFixedBoundsExist()
    {
        var request = new CreateTileRequest(
            Title: "Flexible",
            NextAction: null,
            DoneDefinition: null,
            Temporal: new CreateTileTemporalRequest(null, null, null, null, null, null),
            Objective: null,
            Interruption: null,
            Automation: null,
            Annotation: null,
            ConflictResolution: null);

        var guidance = CreateTileParityResolver.GetManualAdjustGuidance(request, isJapanese: false);

        Assert.True(guidance.FocusStart);
        Assert.False(guidance.FocusEnd);
        Assert.Contains("start", guidance.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildCreateConflictToastPrompt_AlwaysIncludesCancelAction_AndOverlapMessage()
    {
        var prompt = new CreateConflictPrompt(
            Kind: "create_conflict",
            Title: null,
            Message: null,
            Options:
            [
                new CreateConflictOption("keep_overlap", "重ねたまま作成"),
                new CreateConflictOption("manual_adjust", "手動で調整")
            ]);

        var toastPrompt = CreateTileParityResolver.BuildCreateConflictToastPrompt(prompt, isJapanese: true);

        Assert.Equal("create_conflict", toastPrompt.Kind);
        Assert.Equal("warning", toastPrompt.Severity);
        Assert.Contains("重なり", toastPrompt.Body);
        Assert.Contains(toastPrompt.Actions, static action => action.Id == "keep_overlap");
        Assert.Contains(toastPrompt.Actions, static action => action.Id == "manual_adjust");
        Assert.Contains(toastPrompt.Actions, static action => action.Id == "cancel_create");
    }

    [Fact]
    public void BuildCreateConflictToastPrompt_PreservesGivenTitleAndMessage()
    {
        var prompt = new CreateConflictPrompt(
            Kind: "create_conflict",
            Title: "Fixed time conflict detected",
            Message: "Overlap detected",
            Options: [new CreateConflictOption("auto_nearest", "Move to nearest free slot")]);

        var toastPrompt = CreateTileParityResolver.BuildCreateConflictToastPrompt(prompt, isJapanese: false);

        Assert.Equal("Fixed time conflict detected", toastPrompt.Title);
        Assert.Equal("Overlap detected", toastPrompt.Body);
        Assert.Contains(toastPrompt.Actions, static action => action.Id == "auto_nearest");
        Assert.Contains(toastPrompt.Actions, static action => action.Id == "cancel_create");
    }

    [Fact]
    public void BuildRequest_MapsRecurringDoneRule_ToTimeReached_WhenEndTimeSpecified()
    {
        var draft = new CreateTileDraft(
            Title: "Recurring with end",
            TileKind: "work",
            ObjectiveMode: "recurring",
            RecurrenceUseStartAt: true,
            RecurrenceUseEndAt: true,
            RecurrenceStartTime: TimeSpan.FromHours(22),
            RecurrenceEndTime: TimeSpan.FromHours(23),
            RecurrenceFrequency: "daily",
            RecurrenceInterval: 1,
            WorkHours: 0,
            WorkMinutes: 30);

        var request = CreateTileParityResolver.BuildRequest(draft, isJapanese: true);

        Assert.NotNull(request.Objective);
        Assert.Equal("time_reached", request.Objective!.DoneRule);
    }

    [Fact]
    public void BuildRequest_MapsRecurringValidityAndDailyWindow_ToTemporalAndRecurrencePayload()
    {
        var validFrom = DateTimeOffset.Parse("2026-04-05T00:00:00+09:00");
        var validTo = DateTimeOffset.Parse("2026-04-10T00:00:00+09:00");
        var draft = new CreateTileDraft(
            Title: "Recurring payload",
            TileKind: "work",
            ObjectiveMode: "recurring",
            RecurrenceFrequency: "daily",
            RecurrenceInterval: 1,
            RecurrenceUseStartAt: true,
            RecurrenceUseEndAt: true,
            RecurrenceStartTime: TimeSpan.FromHours(8),
            RecurrenceEndTime: TimeSpan.FromHours(9),
            RecurrenceValidFromEnabled: true,
            RecurrenceValidToEnabled: true,
            RecurrenceValidFromDate: validFrom,
            RecurrenceValidToDate: validTo,
            WorkHours: 0,
            WorkMinutes: 0);

        var request = CreateTileParityResolver.BuildRequest(draft, isJapanese: true);

        Assert.NotNull(request.Temporal);
        Assert.NotNull(request.Objective?.Recurrence);
        Assert.NotNull(request.Temporal!.ReleaseAt);
        Assert.NotNull(request.Temporal.DueAt);
        Assert.Equal("2026-04-04T15:00:00.0000000+00:00", request.Temporal.ReleaseAt);
        Assert.Equal("2026-04-10T14:59:00.0000000+00:00", request.Temporal.DueAt);
        Assert.Equal(1440, request.Objective!.Recurrence!.Generator.StepMin);
        Assert.NotNull(request.Objective.Recurrence.Generator.AnchorEpochMin);
        Assert.Equal("freq=daily;interval=1", request.Objective.Recurrence.Selector.Expression);
        Assert.Equal(8 * 60, request.Objective!.Recurrence!.Window.StartOffsetMin);
        Assert.Equal(9 * 60, request.Objective.Recurrence.Window.EndOffsetMin);
    }

    [Fact]
    public void BuildRequest_DefaultAutomation_EnablesPromptOnStart()
    {
        var draft = new CreateTileDraft(
            Title: "Prompt on start default",
            TileKind: "work",
            ObjectiveMode: "finish_once");

        var request = CreateTileParityResolver.BuildRequest(draft, isJapanese: true);

        Assert.NotNull(request.Automation);
        Assert.True(request.Automation!.PromptOnStart);
    }

    [Fact]
    public void BuildRequest_MapsMaximizeDoneRule_ToIntervalEnd()
    {
        var draft = new CreateTileDraft(
            Title: "Maximize",
            TileKind: "work",
            ObjectiveMode: "maximize_within_interval",
            UseStartAt: true,
            UseEndAt: true,
            StartAt: DateTimeOffset.Parse("2026-03-31T09:00:00+09:00"),
            EndAt: DateTimeOffset.Parse("2026-03-31T10:00:00+09:00"),
            WorkHours: 0,
            WorkMinutes: 60);

        var request = CreateTileParityResolver.BuildRequest(draft, isJapanese: true);

        Assert.NotNull(request.Objective);
        Assert.Equal("interval_end", request.Objective!.DoneRule);
    }
}
