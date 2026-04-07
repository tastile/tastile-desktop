using TastileDesktop.Models;

namespace TastileDesktop.Services;

public static class PromptAutoActionPolicy
{
    public static string? Resolve(PromptView prompt, bool isFixedScheduleTile)
    {
        if (!isFixedScheduleTile || prompt.Actions.Count == 0)
        {
            return null;
        }

        if (HasAnyAction(prompt, "CONFIRM_CONTINUE", "CONFIRM_STOP_AT", "CONFIRM_EXECUTED", "CONFIRM_SKIPPED"))
        {
            return null;
        }

        if (string.Equals(prompt.Kind, "start", StringComparison.OrdinalIgnoreCase))
        {
            return FindAction(prompt, "START", "START_TILE");
        }

        if (string.Equals(prompt.Kind, "end", StringComparison.OrdinalIgnoreCase))
        {
            return FindAction(prompt, "COMPLETE_PHASE", "COMPLETE", "COMPLETE_TILE", "COMPLETE_AND_START_NEXT");
        }

        return null;
    }

    private static bool HasAnyAction(PromptView prompt, params string[] candidates)
        => FindAction(prompt, candidates) is not null;

    private static string? FindAction(PromptView prompt, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var matched = prompt.Actions.FirstOrDefault(action =>
                string.Equals(action.Id, candidate, StringComparison.OrdinalIgnoreCase));
            if (matched is not null && !string.IsNullOrWhiteSpace(matched.Id))
            {
                return matched.Id;
            }
        }

        return null;
    }
}
