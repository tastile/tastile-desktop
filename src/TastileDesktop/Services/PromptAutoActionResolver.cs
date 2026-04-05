using TastileDesktop.Models;

namespace TastileDesktop.Services;

public static class PromptAutoActionResolver
{
    private static readonly string[] StartupRecoveryPriority =
    [
        "CONFIRM_CONTINUE",
        "CONFIRM_STOP_AT",
        "CONFIRM_EXECUTED",
        "CONFIRM_SKIPPED",
    ];

    public static string? Resolve(PromptView prompt)
    {
        foreach (var actionId in StartupRecoveryPriority)
        {
            if (prompt.Actions.Any(action => string.Equals(action.Id, actionId, StringComparison.OrdinalIgnoreCase)))
            {
                return actionId;
            }
        }

        // Do not auto-execute normal prompt actions.
        // This avoids forced break/start loops when prompt timing races with state updates.
        return null;
    }
}
