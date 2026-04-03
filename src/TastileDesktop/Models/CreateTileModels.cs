namespace TastileDesktop.Models;

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
    string? Memo = null);

public sealed record CreateTileManualAdjustGuidance(
    bool FocusStart,
    bool FocusEnd,
    string Message);

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
