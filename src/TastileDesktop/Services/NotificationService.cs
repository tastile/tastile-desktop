using Microsoft.Toolkit.Uwp.Notifications;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// Manages Windows toast notifications for Tastile.
/// </summary>
public class NotificationService : IDisposable
{
    private readonly CoreApiClient _api;

    public NotificationService(CoreApiClient api)
    {
        _api = api;
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;
    }

    /// <summary>
    /// Show work reminder at ToastNotifyMinutes (e.g., 15 min).
    /// </summary>
    public void ShowWorkReminder(string tileTitle, int elapsedMinutes)
    {
        new ToastContentBuilder()
            .AddText($"{elapsedMinutes} min on \"{tileTitle}\"")
            .AddText("Keep going, or take a break?")
            .AddButton(new ToastButton("Continue", "action=continue"))
            .AddButton(new ToastButton("Break", "action=break"))
            .AddButton(new ToastButton("Complete", "action=complete"))
            .Show();
    }

    /// <summary>
    /// Show break over reminder.
    /// </summary>
    public void ShowBreakOverReminder()
    {
        new ToastContentBuilder()
            .AddText("Break is over!")
            .AddText("Ready to get back to work?")
            .AddButton(new ToastButton("Continue", "action=endbreak"))
            .Show();
    }

    /// <summary>
    /// Show idle reminder when user has been idle.
    /// </summary>
    public void ShowIdleReminder()
    {
        new ToastContentBuilder()
            .AddText("What's next?")
            .AddText("You've been idle. Pick a tile to work on.")
            .Show();
    }

    private async void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        try
        {
            // Parse arguments from query string format (e.g., "action=continue")
            var action = ParseQueryString(e.Argument ?? "").GetValueOrDefault("action");
            switch (action)
            {
                case "continue":
                    // User acknowledged, do nothing
                    break;
                case "break":
                    await _api.StartBreakAsync(5);
                    break;
                case "complete":
                    await _api.CompleteTileAsync();
                    break;
                case "endbreak":
                    await _api.EndBreakAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Toast action failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        ToastNotificationManagerCompat.Uninstall();
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(query)) return result;

        var pairs = query.Split('&');
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                result[parts[0]] = parts[1];
            }
        }
        return result;
    }
}
