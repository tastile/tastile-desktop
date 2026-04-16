using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using Windows.Graphics;
using System.Runtime.InteropServices;

namespace TastileDesktop.Services;

public sealed class PromptToastWindow : Window
{
    public const int WindowWidth = 420;
    public const int WindowHeight = 150;

    private readonly Border _rootBorder;
    private readonly TextBlock _titleText;
    private readonly TextBlock _bodyText;
    private readonly TextBlock _countdownText;
    private readonly Grid _actionsGrid;
    private readonly StackPanel _deferOptionsPanel;
    private readonly Button _deferBackButton;
    private readonly DispatcherTimer _countdownTimer;
    private DateTimeOffset? _expiresAtUtc;
    private Func<string, DateTimeOffset?, Task>? _actionHandler;
    private Func<string, int?, Task>? _deferHandler;
    private string? _timeoutActionId;
    private bool _timeoutActionTriggered;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;

    public PromptToastWindow()
    {
        _titleText = new TextBlock
        {
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = (Brush)Application.Current.Resources["PrimaryForegroundBrush"],
        };

        _bodyText = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 2,
            Foreground = (Brush)Application.Current.Resources["SecondaryForegroundBrush"],
            Margin = new Thickness(0, 4, 0, 0),
        };
        _countdownText = new TextBlock
        {
            FontSize = 11,
            TextWrapping = TextWrapping.NoWrap,
            Foreground = (Brush)Application.Current.Resources["SecondaryForegroundBrush"],
            Margin = new Thickness(0, 4, 0, 0),
            Visibility = Visibility.Collapsed,
        };
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _countdownTimer.Tick += (_, _) => RefreshCountdown();

        _deferOptionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = Visibility.Collapsed,
        };
        _deferBackButton = new Button
        {
            Content = "←",
            MinHeight = 30,
            MinWidth = 36,
            Margin = new Thickness(0, 0, 4, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _deferBackButton.Click += (_, _) => ShowActionButtons();

        _deferOptionsPanel.Children.Add(_deferBackButton);

        var deferDurations = new (string Label, int Minutes)[]
        {
            ("30分", 30),
            ("1時間", 60),
            ("2時間", 120),
            ("明日", 1440),
            ("来週", 10080),
        };

        foreach (var d in deferDurations)
        {
            var btn = new Button
            {
                Content = d.Label,
                Tag = d.Minutes,
                MinHeight = 30,
                MinWidth = 60,
                Margin = new Thickness(0, 0, 4, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            btn.Click += OnDeferDurationClick;
            _deferOptionsPanel.Children.Add(btn);
        }

        _actionsGrid = new Grid
        {
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var contentGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetRow(_titleText, 0);
        Grid.SetRow(_bodyText, 1);
        Grid.SetRow(_countdownText, 2);
        Grid.SetRow(_actionsGrid, 4);
        Grid.SetRow(_deferOptionsPanel, 4);
        contentGrid.Children.Add(_titleText);
        contentGrid.Children.Add(_bodyText);
        contentGrid.Children.Add(_countdownText);
        contentGrid.Children.Add(_actionsGrid);
        contentGrid.Children.Add(_deferOptionsPanel);

        _rootBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["AppBorderBrush"],
            Background = (Brush)Application.Current.Resources["AppSurfaceElevatedBrush"],
            Padding = new Thickness(10, 8, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = contentGrid,
        };

        Content = _rootBorder;
        FloatingWindowHelper.ConfigurePanel(this, WindowWidth, WindowHeight);
        FloatingWindowHelper.SetAlwaysOnTop(this, true);
    }

    public void ShowPrompt(Models.PromptView prompt, int maxActions, Func<string, DateTimeOffset?, Task> actionHandler, Func<string, int?, Task>? deferHandler = null)
    {
        _actionHandler = actionHandler;
        _deferHandler = deferHandler;
        _timeoutActionId = PromptTimeoutActionResolver.Resolve(prompt);
        _timeoutActionTriggered = false;
        _titleText.Text = string.IsNullOrWhiteSpace(prompt.Title) ? "Prompt" : prompt.Title;
        _bodyText.Text = ResolveBodyText(prompt);
        ConfigureCountdown(_timeoutActionId is null ? null : prompt.ExpiresAt);
        _actionsGrid.Visibility = Visibility.Visible;
        _deferOptionsPanel.Visibility = Visibility.Collapsed;
        _actionsGrid.Children.Clear();
        _actionsGrid.ColumnDefinitions.Clear();

        var actions = prompt.Actions.Take(Math.Clamp(maxActions, 1, 5)).ToList();

        var totalButtons = actions.Count;
        for (var i = 0; i < totalButtons; i++)
        {
            _actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var i = 0; i < totalButtons; i++)
        {
            var action = actions[i];
            var isFirst = i == 0;
            var isLast = i == totalButtons - 1;
            var button = new Button
            {
                Content = TranslateActionLabel(action.Id, action.Label),
                Tag = action.Id,
                MinHeight = 30,
                Margin = new Thickness(isFirst ? 0 : 4, 0, isLast ? 0 : 4, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            button.Click += OnActionClick;
            Grid.SetColumn(button, i);
            _actionsGrid.Children.Add(button);
        }

        ShowOnTop(this);
    }

    private void ShowActionButtons()
    {
        _deferOptionsPanel.Visibility = Visibility.Collapsed;
        _actionsGrid.Visibility = Visibility.Visible;
    }

    private void ShowDeferOptions()
    {
        _actionsGrid.Visibility = Visibility.Collapsed;
        _deferOptionsPanel.Visibility = Visibility.Visible;
    }

    private async void OnDeferDurationClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not int minutes) return;
        ShowActionButtons();
        if (_deferHandler != null)
        {
            await _deferHandler("DEFER", minutes);
        }
        else if (_actionHandler != null)
        {
            await _actionHandler("DEFER", null);
        }
    }

    private static string TranslateActionLabel(string actionId, string fallbackLabel)
    {
        var id = actionId.ToUpperInvariant();
        return id switch
        {
            "START" or "START_TILE" => "開始",
            "DEFER" => "先送り",
            "COMPLETE" or "COMPLETE_AND_START_NEXT" => "完了",
            "EXTEND" => "延長",
            "BREAK" or "START_BREAK" => "休憩",
            "END_BREAK" => "休憩終了",
            "DISMISS" or "CONTINUE" => "閉じる",
            "CONFIRM_CONTINUE" => "まだ継続中",
            "CONFIRM_STOP_AT" => "ここで終了した",
            "CONFIRM_EXECUTED" => "実施した",
            "CONFIRM_SKIPPED" => "実施しなかった",
            _ => fallbackLabel,
        };
    }

    private static string ResolveBodyText(Models.PromptView prompt)
    {
        if (!string.IsNullOrWhiteSpace(prompt.Why))
        {
            return prompt.Why;
        }

        if (!string.IsNullOrWhiteSpace(prompt.Body))
        {
            return prompt.Body;
        }

        return string.Empty;
    }

    private async Task TriggerTimeoutActionAsync()
    {
        if (_timeoutActionTriggered)
        {
            return;
        }
        if (_actionHandler is null || string.IsNullOrWhiteSpace(_timeoutActionId))
        {
            return;
        }

        _timeoutActionTriggered = true;
        try
        {
            await _actionHandler(_timeoutActionId!, null);
        }
        catch
        {
            _timeoutActionTriggered = false;
        }
    }

    public void ShowBackdrop(Models.PromptView prompt, int waitingBehind)
    {
        _actionHandler = null;
        _deferHandler = null;
        _timeoutActionId = null;
        _timeoutActionTriggered = false;
        _titleText.Text = string.IsNullOrWhiteSpace(prompt.Title) ? "Prompt" : prompt.Title;
        _bodyText.Text = waitingBehind > 0
            ? $"{Math.Clamp(waitingBehind, 1, 99)} more prompt(s) in queue"
            : ResolveBodyText(prompt);
        ConfigureCountdown(null);
        _actionsGrid.Children.Clear();
        _actionsGrid.ColumnDefinitions.Clear();
        _actionsGrid.Visibility = Visibility.Collapsed;
        _deferOptionsPanel.Visibility = Visibility.Collapsed;

        ShowOnTop(this);
    }

    public void HidePrompt()
    {
        _countdownTimer.Stop();
        _expiresAtUtc = null;
        _timeoutActionTriggered = false;
        _countdownText.Visibility = Visibility.Collapsed;
        WindowExtensions.Hide(this);
    }

    public void ReassertTopMost()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public void PlaceOnDisplay(RectInt32 workArea)
    {
        var appWindow = GetAppWindow(this);
        if (appWindow == null) return;

        var x = workArea.X + (workArea.Width - WindowWidth) / 2;
        var y = workArea.Y + 24;
        appWindow.Move(new PointInt32(x, y));
    }

    public static void ShowOnTop(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
    }

    public static Microsoft.UI.Windowing.AppWindow? GetAppWindow(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero) return null;
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
    }

    private async void OnActionClick(object sender, RoutedEventArgs e)
    {
        if (_actionHandler == null) return;
        if (sender is not FrameworkElement element || element.Tag is not string actionId || string.IsNullOrWhiteSpace(actionId)) return;

        var id = actionId.ToUpperInvariant();
        if (id == "DEFER")
        {
            ShowDeferOptions();
            return;
        }

        await _actionHandler(actionId, null);
    }

    private void ConfigureCountdown(string? expiresAt)
    {
        _countdownTimer.Stop();
        _expiresAtUtc = null;
        _countdownText.Visibility = Visibility.Collapsed;
        _countdownText.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(expiresAt))
        {
            return;
        }
        if (!PromptExpiryParser.TryParseExpiryIso8601(expiresAt, out var parsed))
        {
            return;
        }
        _expiresAtUtc = parsed;
        RefreshCountdown();
        _countdownTimer.Start();
    }

    private void RefreshCountdown()
    {
        if (_expiresAtUtc is null)
        {
            _countdownTimer.Stop();
            _countdownText.Visibility = Visibility.Collapsed;
            return;
        }
        var remaining = _expiresAtUtc.Value - DateTimeOffset.UtcNow;
        var remainingSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        var minutes = remainingSeconds / 60;
        var seconds = remainingSeconds % 60;
        _countdownText.Text = $"自動実行まで {minutes:00}:{seconds:00}";
        _countdownText.Visibility = Visibility.Visible;
        if (remainingSeconds == 0)
        {
            _countdownTimer.Stop();
            _ = TriggerTimeoutActionAsync();
        }
    }
}
