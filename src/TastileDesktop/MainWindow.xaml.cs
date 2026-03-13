using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TastileDesktop.Models;
using TastileDesktop.Services;

namespace TastileDesktop;

/// <summary>
/// Bindable item for tile list display.
/// </summary>
public sealed class TileListItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Lifecycle { get; init; }
    public required long WorkedMinutes { get; init; }
    public string WorkedText => WorkedMinutes > 0 ? $"{WorkedMinutes}m" : "";

    public SolidColorBrush BadgeBackground => Lifecycle switch
    {
        "Ready" => new SolidColorBrush(ColorHelper.FromArgb(255, 0, 120, 212)),
        "Started" => new SolidColorBrush(ColorHelper.FromArgb(255, 16, 124, 16)),
        "Done" => new SolidColorBrush(ColorHelper.FromArgb(255, 96, 96, 96)),
        _ => new SolidColorBrush(ColorHelper.FromArgb(255, 128, 128, 128)),
    };

    public SolidColorBrush BadgeForeground => new SolidColorBrush(Colors.White);
}

public sealed partial class MainWindow : Window
{
    private readonly CoreApiClient _api = new();
    private readonly DispatcherTimer _pollTimer;
    private string? _activeTileId;

    public MainWindow()
    {
        this.InitializeComponent();

        // Set window size via AppWindow API
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(520, 780));

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _pollTimer.Tick += async (_, _) => await PollAsync();
        _pollTimer.Start();

        // Fire initial poll immediately
        _ = PollAsync();
    }

    // ── Polling ──────────────────────────────────────────────────────

    private async Task PollAsync()
    {
        try
        {
            var healthy = await _api.CheckHealthAsync();
            if (!healthy)
            {
                SetConnectionStatus(false);
                return;
            }
            SetConnectionStatus(true);

            var activeTask = _api.GetActiveTileAsync();
            var tilesTask = _api.GetTilesAsync();
            await Task.WhenAll(activeTask, tilesTask);

            UpdateExecutionPanel(activeTask.Result);
            UpdateTileList(tilesTask.Result);
        }
        catch (Exception ex)
        {
            SetConnectionStatus(false);
            StatusText.Text = $"Poll error: {ex.Message}";
        }
    }

    // ── Connection Status ────────────────────────────────────────────

    private void SetConnectionStatus(bool connected)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (connected)
            {
                ConnectionDot.Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 16, 124, 16));
                ConnectionText.Text = "Connected";
            }
            else
            {
                ConnectionDot.Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28));
                ConnectionText.Text = "Daemon offline";
            }
        });
    }

    // ── Execution Panel ──────────────────────────────────────────────

    private void UpdateExecutionPanel(ActiveTileResponse? active)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (active?.Tile == null || string.Equals(active.Phase, "Idle", StringComparison.OrdinalIgnoreCase))
            {
                _activeTileId = null;
                IdlePanel.Visibility = Visibility.Visible;
                WorkPanel.Visibility = Visibility.Collapsed;
                BreakPanel.Visibility = Visibility.Collapsed;
                CompleteButton.Visibility = Visibility.Collapsed;
                EndBreakButton.Visibility = Visibility.Collapsed;
                BreakButton.Visibility = Visibility.Visible;
                return;
            }

            _activeTileId = active.Tile.Id;

            if (string.Equals(active.Phase, "Break", StringComparison.OrdinalIgnoreCase))
            {
                IdlePanel.Visibility = Visibility.Collapsed;
                WorkPanel.Visibility = Visibility.Collapsed;
                BreakPanel.Visibility = Visibility.Visible;

                // Countdown
                if (DateTimeOffset.TryParse(active.PhaseEndsAt, out var endsAt))
                {
                    var remaining = endsAt - DateTimeOffset.UtcNow;
                    if (remaining.TotalSeconds > 0)
                        BreakCountdown.Text = $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2} remaining";
                    else
                        BreakCountdown.Text = "Break ended";
                }
                else
                {
                    BreakCountdown.Text = "On break";
                }

                CompleteButton.Visibility = Visibility.Collapsed;
                EndBreakButton.Visibility = Visibility.Visible;
                BreakButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Work phase
                IdlePanel.Visibility = Visibility.Collapsed;
                WorkPanel.Visibility = Visibility.Visible;
                BreakPanel.Visibility = Visibility.Collapsed;

                ActiveTileTitle.Text = active.Tile.Title;
                ActiveTileNextAction.Text = !string.IsNullOrEmpty(active.Tile.NextAction)
                    ? $"Next: {active.Tile.NextAction}" : "";

                // Elapsed time
                if (DateTimeOffset.TryParse(active.PhaseStartedAt, out var startedAt))
                {
                    var elapsed = DateTimeOffset.UtcNow - startedAt;
                    WorkElapsed.Text = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
                }

                CompleteButton.Visibility = Visibility.Visible;
                EndBreakButton.Visibility = Visibility.Collapsed;
                BreakButton.Visibility = Visibility.Visible;
            }
        });
    }

    // ── Tile List ────────────────────────────────────────────────────

    private void UpdateTileList(TilesResponse? tiles)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (tiles?.Tiles == null) return;

            var items = tiles.Tiles.Select(t => new TileListItem
            {
                Id = t.Id,
                Title = t.Title,
                Lifecycle = t.Lifecycle,
                WorkedMinutes = t.WorkedMinutes,
            }).ToList();

            TileListView.ItemsSource = items;
        });
    }

    // ── Button Handlers ──────────────────────────────────────────────

    private async void OnComplete(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _api.CompleteTileAsync();
            if (result != null && !result.Ok)
                StatusText.Text = $"Error: {result.Error}";
            else
                StatusText.Text = "Tile completed";
            await PollAsync();
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
    }

    private async void OnStartBreak(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _api.StartBreakAsync(5);
            if (result != null && !result.Ok)
                StatusText.Text = $"Error: {result.Error}";
            else
                StatusText.Text = "Break started (5 min)";
            await PollAsync();
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
    }

    private async void OnEndBreak(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _api.EndBreakAsync();
            if (result != null && !result.Ok)
                StatusText.Text = $"Error: {result.Error}";
            else
                StatusText.Text = "Break ended";
            await PollAsync();
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
    }

    private async void OnCreateTile(object sender, RoutedEventArgs e)
    {
        var title = NewTileTitle.Text?.Trim();
        if (string.IsNullOrEmpty(title)) return;

        try
        {
            var result = await _api.CreateTileAsync(title);
            if (result != null && !result.Ok)
                StatusText.Text = $"Error: {result.Error}";
            else
            {
                StatusText.Text = $"Created: {title}";
                NewTileTitle.Text = "";
            }
            await PollAsync();
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
    }

    private async void OnTileClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not TileListItem item) return;

        // Only start tiles that are Ready
        if (!string.Equals(item.Lifecycle, "Ready", StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            var result = await _api.StartTileAsync(item.Id);
            if (result != null && !result.Ok)
                StatusText.Text = $"Error: {result.Error}";
            else
                StatusText.Text = $"Started: {item.Title}";
            await PollAsync();
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
    }

    private async void OnSendMemo(object sender, RoutedEventArgs e)
    {
        var text = MemoText.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        try
        {
            var result = await _api.AttachMemoAsync(_activeTileId, text);
            if (result != null && !result.Ok)
                StatusText.Text = $"Error: {result.Error}";
            else
            {
                StatusText.Text = "Memo sent";
                MemoText.Text = "";
            }
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
    }
}
