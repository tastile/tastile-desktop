using TastileDesktop.Models;

namespace TastileDesktop.Services;

public static class PromptActionSelectionPolicy
{
    public static bool TryResolveAction(PromptView prompt, string? actionId, out string? resolvedActionId)
    {
        resolvedActionId = null;
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return false;
        }

        var requested = actionId.Trim();
        var matched = prompt.Actions.FirstOrDefault(action =>
            string.Equals(action.Id, requested, StringComparison.OrdinalIgnoreCase));

        if (matched is null || string.IsNullOrWhiteSpace(matched.Id))
        {
            return false;
        }

        resolvedActionId = matched.Id;
        return true;
    }
}
