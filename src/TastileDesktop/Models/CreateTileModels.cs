namespace TastileDesktop.Models;

public enum CreateTileWorkflowKind
{
    Event,
    Task,
    Recurring,
    Detailed,
}

public enum CreateTileRepeatMode
{
    Once,
    Daily,
    Weekly,
    Monthly,
    Interval,
}

public enum CreateTileMonthlyKind
{
    None,
    ByDay,
    ByWeekday,
}

public enum CreateTileTimeModel
{
    DurationOnly,
    FixedWindow,
    WindowWithDuration,
}

public enum CreateTileTimeOfDayMode
{
    Unspecified,
    Range,
    AllDay,
}

public sealed record CreateTileDraft(
    string Title,
    string? Description = null,
    TimeSpan? RecurrenceStartTime = null,
    TimeSpan? RecurrenceEndTime = null,
    string? ObjectiveMode = null,
    int? RecurrenceInterval = null,
    bool RecurrenceUseStartAt = false,
    bool RecurrenceUseEndAt = false,
    bool UseEndAt = false,
    DateTimeOffset? StartAt = null,
    DateTimeOffset? EndAt = null,
    string? TileKind = null,
    bool UseStartAt = false,
    string? RecurrenceFrequency = null,
    List<int>? RecurrenceWeekdays = null,
    int? RecurrenceMonthlyWeek = null,
    int? RecurrenceMonthlyWeekday = null,
    bool RecurrenceValidFromEnabled = false,
    bool RecurrenceValidToEnabled = false,
    DateTimeOffset? RecurrenceValidFromDate = null,
    DateTimeOffset? RecurrenceValidToDate = null,
    int? WorkHours = null,
    int? WorkMinutes = null,
    bool DurationManuallyEdited = false,
    bool BreakSplitsWork = true,
    string? Project = null,
    List<string>? Tags = null,
    string? Memo = null,
    // Workflow parity fields (UI-only — BuildRequest may read these to
    // mirror web semantics where useful; defaults preserve legacy behaviour).
    CreateTileWorkflowKind WorkflowKind = CreateTileWorkflowKind.Task,
    CreateTileRepeatMode RepeatMode = CreateTileRepeatMode.Once,
    CreateTileMonthlyKind MonthlyKind = CreateTileMonthlyKind.None,
    int? MonthlyDayOfMonth = null,
    int? MonthlyWeekOfMonth = null,
    int? MonthlyWeekday = null,
    int? IntervalValue = null,
    string? IntervalUnit = null,
    DateTimeOffset? IntervalFirstAt = null,
    DateTimeOffset? RecurringEndDate = null,
    CreateTileTimeModel TimeModel = CreateTileTimeModel.FixedWindow,
    CreateTileTimeOfDayMode TimeOfDayMode = CreateTileTimeOfDayMode.Range,
    TimeSpan? TimeOfDayStart = null,
    TimeSpan? TimeOfDayEnd = null,
    string? ColorHex = null,
    List<string>? SubtaskTitles = null);

public sealed record CreateTileManualAdjustGuidance(
    bool FocusStart,
    bool FocusEnd,
    string Message);

/// <summary>
/// Mutable form-state mirror of <see cref="CreateTileDraft"/>. Body
/// UserControls mutate this object directly through their ApplyState /
/// WriteState methods, so they don't have to rebuild the immutable record
/// for every keystroke. The orchestrator calls ToDraft() once at submit
/// time to produce the wire shape.
/// </summary>
public sealed class CreateTileFormState
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TimeSpan? RecurrenceStartTime { get; set; }
    public TimeSpan? RecurrenceEndTime { get; set; }
    public string? ObjectiveMode { get; set; }
    public int? RecurrenceInterval { get; set; }
    public bool RecurrenceUseStartAt { get; set; }
    public bool RecurrenceUseEndAt { get; set; }
    public bool UseEndAt { get; set; }
    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public string? TileKind { get; set; }
    public bool UseStartAt { get; set; }
    public string? RecurrenceFrequency { get; set; }
    public List<int>? RecurrenceWeekdays { get; set; }
    public int? RecurrenceMonthlyWeek { get; set; }
    public int? RecurrenceMonthlyWeekday { get; set; }
    public bool RecurrenceValidFromEnabled { get; set; }
    public bool RecurrenceValidToEnabled { get; set; }
    public DateTimeOffset? RecurrenceValidFromDate { get; set; }
    public DateTimeOffset? RecurrenceValidToDate { get; set; }
    public int? WorkHours { get; set; }
    public int? WorkMinutes { get; set; }
    public bool DurationManuallyEdited { get; set; }
    public bool BreakSplitsWork { get; set; } = true;
    public string? Project { get; set; }
    public List<string>? Tags { get; set; }
    public string? Memo { get; set; }
    public CreateTileWorkflowKind WorkflowKind { get; set; } = CreateTileWorkflowKind.Task;
    public CreateTileRepeatMode RepeatMode { get; set; } = CreateTileRepeatMode.Once;
    public CreateTileMonthlyKind MonthlyKind { get; set; } = CreateTileMonthlyKind.None;
    public int? MonthlyDayOfMonth { get; set; }
    public int? MonthlyWeekOfMonth { get; set; }
    public int? MonthlyWeekday { get; set; }
    public int? IntervalValue { get; set; }
    public string? IntervalUnit { get; set; }
    public DateTimeOffset? IntervalFirstAt { get; set; }
    public DateTimeOffset? RecurringEndDate { get; set; }
    public CreateTileTimeModel TimeModel { get; set; } = CreateTileTimeModel.FixedWindow;
    public CreateTileTimeOfDayMode TimeOfDayMode { get; set; } = CreateTileTimeOfDayMode.Range;
    public TimeSpan? TimeOfDayStart { get; set; }
    public TimeSpan? TimeOfDayEnd { get; set; }
    public string? ColorHex { get; set; }
    public List<string>? SubtaskTitles { get; set; }

    public CreateTileDraft ToDraft() => new(
        Title, Description, RecurrenceStartTime, RecurrenceEndTime, ObjectiveMode,
        RecurrenceInterval, RecurrenceUseStartAt, RecurrenceUseEndAt, UseEndAt, StartAt, EndAt,
        TileKind, UseStartAt, RecurrenceFrequency, RecurrenceWeekdays, RecurrenceMonthlyWeek,
        RecurrenceMonthlyWeekday, RecurrenceValidFromEnabled, RecurrenceValidToEnabled,
        RecurrenceValidFromDate, RecurrenceValidToDate, WorkHours, WorkMinutes,
        DurationManuallyEdited, BreakSplitsWork, Project, Tags, Memo, WorkflowKind, RepeatMode,
        MonthlyKind, MonthlyDayOfMonth, MonthlyWeekOfMonth, MonthlyWeekday, IntervalValue,
        IntervalUnit, IntervalFirstAt, RecurringEndDate, TimeModel, TimeOfDayMode,
        TimeOfDayStart, TimeOfDayEnd, ColorHex, SubtaskTitles);

    public void LoadFrom(CreateTileDraft draft)
    {
        Title = draft.Title;
        Description = draft.Description;
        RecurrenceStartTime = draft.RecurrenceStartTime;
        RecurrenceEndTime = draft.RecurrenceEndTime;
        ObjectiveMode = draft.ObjectiveMode;
        RecurrenceInterval = draft.RecurrenceInterval;
        RecurrenceUseStartAt = draft.RecurrenceUseStartAt;
        RecurrenceUseEndAt = draft.RecurrenceUseEndAt;
        UseEndAt = draft.UseEndAt;
        StartAt = draft.StartAt;
        EndAt = draft.EndAt;
        TileKind = draft.TileKind;
        UseStartAt = draft.UseStartAt;
        RecurrenceFrequency = draft.RecurrenceFrequency;
        RecurrenceWeekdays = draft.RecurrenceWeekdays is null ? null : new List<int>(draft.RecurrenceWeekdays);
        RecurrenceMonthlyWeek = draft.RecurrenceMonthlyWeek;
        RecurrenceMonthlyWeekday = draft.RecurrenceMonthlyWeekday;
        RecurrenceValidFromEnabled = draft.RecurrenceValidFromEnabled;
        RecurrenceValidToEnabled = draft.RecurrenceValidToEnabled;
        RecurrenceValidFromDate = draft.RecurrenceValidFromDate;
        RecurrenceValidToDate = draft.RecurrenceValidToDate;
        WorkHours = draft.WorkHours;
        WorkMinutes = draft.WorkMinutes;
        DurationManuallyEdited = draft.DurationManuallyEdited;
        BreakSplitsWork = draft.BreakSplitsWork;
        Project = draft.Project;
        Tags = draft.Tags is null ? null : new List<string>(draft.Tags);
        Memo = draft.Memo;
        WorkflowKind = draft.WorkflowKind;
        RepeatMode = draft.RepeatMode;
        MonthlyKind = draft.MonthlyKind;
        MonthlyDayOfMonth = draft.MonthlyDayOfMonth;
        MonthlyWeekOfMonth = draft.MonthlyWeekOfMonth;
        MonthlyWeekday = draft.MonthlyWeekday;
        IntervalValue = draft.IntervalValue;
        IntervalUnit = draft.IntervalUnit;
        IntervalFirstAt = draft.IntervalFirstAt;
        RecurringEndDate = draft.RecurringEndDate;
        TimeModel = draft.TimeModel;
        TimeOfDayMode = draft.TimeOfDayMode;
        TimeOfDayStart = draft.TimeOfDayStart;
        TimeOfDayEnd = draft.TimeOfDayEnd;
        ColorHex = draft.ColorHex;
        SubtaskTitles = draft.SubtaskTitles is null ? null : new List<string>(draft.SubtaskTitles);
    }
}

/// <summary>
/// Contract every workflow body satisfies. Lets the orchestrator swap
/// between Event / Task / Recurring / Detailed without caring which
/// concrete UserControl is mounted.
/// </summary>
public interface ICreateTileBody
{
    void ApplyState(CreateTileFormState state);
    void WriteState(CreateTileFormState state);
    event EventHandler? StateChanged;
    event EventHandler? DurationChanged;
}

public sealed class CreateTileCatalog
{
    public static CreateTileCatalog Instance { get; } = new();

    public List<string> ExistingProjects { get; } = new();
    public List<string> ExistingTitles { get; } = new();
    public List<string> ExistingTags { get; } = new();
    public List<CreateTileDraft> GetDrafts() => new();
    public void SaveDraft(CreateTileDraft draft) { }

    public CreateTileCatalog() { }
    public CreateTileCatalog(string arg1, int arg2, bool arg3) { }
    public CreateTileCatalog(List<string> projects, List<string> titles, List<string> tags)
    {
        ExistingProjects.AddRange(projects);
        ExistingTitles.AddRange(titles);
        ExistingTags.AddRange(tags);
    }
}
