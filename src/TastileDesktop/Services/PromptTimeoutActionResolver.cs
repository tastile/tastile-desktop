using TastileDesktop.Models;

namespace TastileDesktop.Services;

public static class PromptTimeoutActionResolver
{
    public static string? Resolve(PromptView prompt)
    {
        if (prompt.Actions.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(prompt.DefaultActionId))
        {
            return null;
        }

        var matched = prompt.Actions.FirstOrDefault(action =>
            string.Equals(action.Id, prompt.DefaultActionId, StringComparison.OrdinalIgnoreCase));
        return matched?.Id;
    }
}
