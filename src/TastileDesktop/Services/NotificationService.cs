using Microsoft.Toolkit.Uwp.Notifications;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// Manages Windows toast notifications for Tastile.
/// </summary>
public class NotificationService : IDisposable
{
    private readonly CoreApiClient _api;

    /// <summary>
    /// Raised when a toast action asks to surface the main panel
    /// (NF04-D07 schedule-change toasts use <c>action=open</c>).
    /// </summary>
    public event EventHandler? PanelOpenRequested;

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
    /// Generic informational toast used by sync-driven notifications
    /// (NF04-D07). When <paramref name="actionId"/>/<paramref name="actionLabel"/>
    /// are provided a single action button is attached; <c>open</c> raises
    /// <see cref="PanelOpenRequested"/>.
    /// </summary>
    public void ShowInfoToast(string title, string body, string? actionId = null, string? actionLabel = null)
    {
        var builder = new ToastContentBuilder()
            .AddText(title)
            .AddText(body);
        if (!string.IsNullOrEmpty(actionId) && !string.IsNullOrEmpty(actionLabel))
        {
            builder.AddButton(new ToastButton(actionLabel, $"action={actionId}"));
        }
        builder.Show();
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
                case "open":
                    PanelOpenRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case "break":
                    await _api.StartBreakAsync(5);
                    break;
                case "complete":
                    // NF04-D04: complete the live tile via the outbox.
                    var tileId = OfflineCommandRuntime.Shared.Commands.ActiveTileId;
                    if (!string.IsNullOrEmpty(tileId))
                    {
                        await OfflineCommandRuntime.Shared.Commands.CompleteTileAsync(tileId);
                    }
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
