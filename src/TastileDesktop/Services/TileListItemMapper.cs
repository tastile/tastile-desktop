using TastileDesktop.Models;
using TastileDesktop.ViewModels;

namespace TastileDesktop.Services;

public static class TileListItemMapper
{
    public static TileListItem Map(TileView tv)
    {
        var recFromTemporal = tv.Recurrence;
        var recFromObjective = tv.Objective?.Recurrence;
        var effectiveRecurrence = recFromTemporal ?? recFromObjective;

        var recurrenceSettings = effectiveRecurrence != null
            ? $"every {effectiveRecurrence.StepMin}min ({effectiveRecurrence.WindowStartMin}-{effectiveRecurrence.WindowEndMin})"
            : null;

        return new TileListItem
        {
            Id = tv.Id,
            Title = tv.Title,
            Lifecycle = tv.Lifecycle,
            WorkedMinutes = tv.WorkedMinutes,
            NextAction = tv.NextAction,
            DoneDefinition = tv.DoneDefinition,
            TargetWorkMin = tv.TargetWorkMin,
            TargetRestMin = tv.TargetRestMin,
            DoneRule = tv.DoneRule,
            ObjectiveMode = tv.ObjectiveMode,
            ProgressPercent = tv.TargetWorkMin.HasValue && tv.TargetWorkMin.Value > 0
                ? Math.Clamp((double)tv.WorkedMinutes / tv.TargetWorkMin.Value * 100d, 0d, 100d)
                : 0d,
            ProjectedNextStartAt = tv.ProjectedNextStartAt,
            NextStartLabel = TileTimeDisplayResolver.ResolveNextStartLabel(tv.ProjectedNextStartAt, tv.Temporal?.Tz),
            FixedStart = tv.Temporal?.FixedStart,
            ActiveStart = tv.Temporal?.ActiveStart,
            FixedEnd = tv.Temporal?.FixedEnd,
            ActiveEnd = tv.Temporal?.ActiveEnd,
            ReleaseAt = tv.Temporal?.ReleaseAt,
            DueAt = tv.Temporal?.DueAt,
            Tz = tv.Temporal?.Tz,
            InterruptPenalty = tv.Interruption?.InterruptPenalty ?? 0,
            ResumePenalty = tv.Interruption?.ResumePenalty ?? 0,
            BreakSplitsWork = tv.Interruption?.BreakSplitsWork ?? false,
            ExternalInterruptOnly = tv.Interruption?.ExternalInterruptOnly ?? false,
            AutoStart = tv.Automation?.AutoStart ?? false,
            AutoComplete = tv.Automation?.AutoComplete ?? false,
            SemanticRole = tv.SemanticRole,
            Labels = tv.Labels,
            RecurrenceSettings = recurrenceSettings,
            RecurrenceFromObjective = recFromObjective,
            RecurrenceStepMin = effectiveRecurrence?.StepMin,
            RecurrenceWindowStartMin = effectiveRecurrence?.WindowStartMin,
            RecurrenceWindowEndMin = effectiveRecurrence?.WindowEndMin,
            RecurrenceExpression = effectiveRecurrence?.Expression,
        };
    }
}
