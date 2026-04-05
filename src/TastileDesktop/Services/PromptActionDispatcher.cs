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
        string? fallbackTileId = null)
    {
        if (!PromptActionSelectionPolicy.TryResolveAction(prompt, requestedActionId, out var resolvedActionId))
        {
            return new PromptActionDispatchResult(
                IsResolved: false,
                ResolvedActionId: null,
                Error: null);
        }

        var id = resolvedActionId!.ToUpperInvariant();
        if (IsStartupRecoveryAction(id))
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
        var settings = new SettingsService();
        CommandResponse? result = id switch
        {
            "CONTINUE" or "DISMISS" => null,
            "BREAK" or "START_BREAK" => await api.StartBreakAsync(settings.Current.DefaultBreakMinutes),
            "START_BREAK_PARALLEL" => await api.StartBreakAsync(settings.Current.DefaultBreakMinutes, insertionMode: "parallel"),
            "START_BREAK_SPLIT" => await api.StartBreakAsync(settings.Current.DefaultBreakMinutes, insertionMode: "split"),
            "START_BREAK_SPLIT_EXTEND" => await api.StartBreakAsync(settings.Current.DefaultBreakMinutes, insertionMode: "split_and_extend"),
            "COMPLETE" or "COMPLETE_AND_START_NEXT" or "COMPLETE_TILE"
                when !string.IsNullOrWhiteSpace(targetTileId)
                => await api.CompleteTileAsync(targetTileId, scope: "tile"),
            "COMPLETE_PHASE"
                when !string.IsNullOrWhiteSpace(targetTileId)
                => await api.CompleteTileAsync(targetTileId, scope: "phase"),
            "END_BREAK" => await api.EndBreakAsync(),
            "EXTEND" => await api.ExtendTileAsync(10),
            "DEFER"
                when !string.IsNullOrWhiteSpace(targetTileId)
                => await api.DeferTileAsync(targetTileId),
            "START" or "START_TILE"
                when !string.IsNullOrWhiteSpace(targetTileId)
                => await api.StartTileAsync(targetTileId),
            _ => null,
        };

        if (result is { Ok: false })
        {
            return new PromptActionDispatchResult(true, id, result.Error ?? $"command failed for action {id}");
        }

        return new PromptActionDispatchResult(true, id, null);
    }

    private static bool IsStartupRecoveryAction(string actionId)
        => actionId is "CONFIRM_CONTINUE" or "CONFIRM_STOP_AT" or "CONFIRM_EXECUTED" or "CONFIRM_SKIPPED" or "DISMISS";
}
