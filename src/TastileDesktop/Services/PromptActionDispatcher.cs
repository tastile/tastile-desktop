using TastileDesktop.Models;

namespace TastileDesktop.Services;

public sealed record PromptActionDispatchResult(
    bool IsResolved,
    string? ResolvedActionId,
    string? Error);

public static class PromptActionDispatcher
{
    public static async Task<PromptActionDispatchResult> ExecuteAsync(
        CoreApiClient api,
        PromptView prompt,
        string? requestedActionId,
        DateTimeOffset? stopAt,
        string? fallbackTileId = null,
        int defaultBreakMinutes = 5)
    {
        if (!PromptActionSelectionPolicy.TryResolveAction(prompt, requestedActionId, out var resolvedActionId))
        {
            return new PromptActionDispatchResult(
                IsResolved: false,
                ResolvedActionId: null,
                Error: null);
        }

        var id = resolvedActionId!.ToUpperInvariant();
        if (IsStartupRecoveryPrompt(prompt) && IsStartupRecoveryAction(id))
        {
            if (string.IsNullOrWhiteSpace(prompt.PromptId) || string.IsNullOrWhiteSpace(prompt.TileId))
            {
                return new PromptActionDispatchResult(true, id, "startup recovery prompt is missing required identifiers");
            }

            var response = await api.RespondStartupRecoveryPromptAsync(
                prompt.PromptId,
                prompt.TileId,
                id,
                stopAt);
            if (response is { Ok: false })
            {
                return new PromptActionDispatchResult(true, id, response.Error ?? "failed to respond startup recovery prompt");
            }

            return new PromptActionDispatchResult(true, id, null);
        }

        var targetTileId = string.IsNullOrWhiteSpace(prompt.TileId)
            ? fallbackTileId
            : prompt.TileId;
        if (RequiresTileId(id) && string.IsNullOrWhiteSpace(targetTileId))
        {
            return new PromptActionDispatchResult(true, id, $"prompt action requires tile id: {id}");
        }

        var breakMinutes = Math.Max(1, defaultBreakMinutes);
        CommandResponse? result;
        result = id switch
        {
            "CONTINUE" or "DISMISS" => null,
            "BREAK" or "START_BREAK" => await api.StartBreakAsync(breakMinutes),
            "START_BREAK_PARALLEL" => await api.StartBreakAsync(breakMinutes, insertionMode: "parallel"),
            "START_BREAK_SPLIT" => await api.StartBreakAsync(breakMinutes, insertionMode: "split"),
            "START_BREAK_SPLIT_EXTEND" => await api.StartBreakAsync(breakMinutes, insertionMode: "split_and_extend"),
            "COMPLETE" or "COMPLETE_AND_START_NEXT" or "COMPLETE_TILE"
                => await api.CompleteTileAsync(targetTileId, scope: "tile"),
            "COMPLETE_PHASE"
                => await api.CompleteTileAsync(targetTileId, scope: "phase"),
            "END_BREAK" => await api.EndBreakAsync(),
            "EXTEND" or "EXTEND_PHASE" => await api.ExtendTileAsync(10),
            "DEFER" or "DEFER_TILE"
                => await api.DeferTileAsync(targetTileId!),
            "START" or "START_TILE"
                => await api.StartTileAsync(targetTileId!),
            _ => null,
        };
        if (result is null && id is not "CONTINUE" and not "DISMISS")
        {
            return new PromptActionDispatchResult(true, id, $"unsupported prompt action: {id}");
        }

        if (result is { Ok: false })
        {
            return new PromptActionDispatchResult(true, id, result.Error ?? $"command failed for action {id}");
        }

        return new PromptActionDispatchResult(true, id, null);
    }

    private static bool IsStartupRecoveryPrompt(PromptView prompt)
        => prompt.Actions.Any(action =>
            action.Id.Equals("confirm_continue", StringComparison.OrdinalIgnoreCase)
            || action.Id.Equals("confirm_stop_at", StringComparison.OrdinalIgnoreCase)
            || action.Id.Equals("confirm_executed", StringComparison.OrdinalIgnoreCase)
            || action.Id.Equals("confirm_skipped", StringComparison.OrdinalIgnoreCase));

    private static bool RequiresTileId(string actionId)
        => actionId is
            "COMPLETE"
            or "COMPLETE_AND_START_NEXT"
            or "COMPLETE_TILE"
            or "COMPLETE_PHASE"
            or "DEFER"
            or "DEFER_TILE"
            or "START"
            or "START_TILE";

    private static bool IsStartupRecoveryAction(string actionId)
        => actionId is "CONFIRM_CONTINUE" or "CONFIRM_STOP_AT" or "CONFIRM_EXECUTED" or "CONFIRM_SKIPPED" or "DISMISS";
}
