using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TastileDesktop.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TastileDesktop.Models;
using TastileDesktop.ViewModels;

namespace TastileDesktop.Views;

public sealed partial class TilesWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
    private readonly CoreApiClient _api = new(
        getAccessToken: Services.AuthService.Instance.GetAccessTokenAsync,
        refreshTokens: Services.CognitoAuthService.Instance.RefreshAsync);
    private readonly SettingsService _settings = new();
    private readonly PromptToastDisplayService _promptToast = PromptToastDisplayService.Instance;
    private readonly TilesWindowLiveUpdateBridge _liveUpdateBridge;

    public ObservableCollection<TileListItem> ReadyTiles { get; } = new();
    public ObservableCollection<TileListItem> StartedTiles { get; } = new();
    public ObservableCollection<TileListItem> DoneTiles { get; } = new();

    private string _viewMode = "by_state";
    private string _searchQuery = string.Empty;
    private int _readyLimit = 4;
    private int _startedLimit = 4;
    private int _doneLimit = 4;

    private int _readyTotal = 0;
    private int _startedTotal = 0;
    private int _doneTotal = 0;

    private string _readyCountDisplay = "0";
    private string _startedCountDisplay = "0";
    private string _doneCountDisplay = "0";
    private string _readyMoreDisplay = string.Empty;
    private string _startedMoreDisplay = string.Empty;
    private string _doneMoreDisplay = string.Empty;

    public string ReadyCountDisplay
    {
        get => _readyCountDisplay;
        private set => SetField(ref _readyCountDisplay, value);
    }
    public string StartedCountDisplay
    {
        get => _startedCountDisplay;
        private set => SetField(ref _startedCountDisplay, value);
    }
    public string DoneCountDisplay
    {
        get => _doneCountDisplay;
        private set => SetField(ref _doneCountDisplay, value);
    }
    public string ReadyMoreDisplay
    {
        get => _readyMoreDisplay;
        private set => SetField(ref _readyMoreDisplay, value);
    }
    public string StartedMoreDisplay
    {
        get => _startedMoreDisplay;
        private set => SetField(ref _startedMoreDisplay, value);
    }
    public string DoneMoreDisplay
    {
        get => _doneMoreDisplay;
        private set => SetField(ref _doneMoreDisplay, value);
    }

    public TilesWindow(EventDrivenPoller tilesChangedSource)
    {
        ArgumentNullException.ThrowIfNull(tilesChangedSource);
        InitializeComponent();
        _liveUpdateBridge = new TilesWindowLiveUpdateBridge(tilesChangedSource, RefreshTilesAsync);
        FloatingWindowHelper.Configure(this, TitleBarArea, 720, 760);
        _ = RefreshTilesAsync();
        Closed += OnWindowClosed;
    }

    private async Task RefreshTilesAsync()
    {
        try
        {
            var searchParam = string.IsNullOrWhiteSpace(_searchQuery) ? null : _searchQuery;

            var readyTask = _api.GetTilesAsync(_viewMode, "ready", _readyLimit, searchParam);
            var startedTask = _api.GetTilesAsync(_viewMode, "started", _startedLimit, searchParam);
            var doneTask = _api.GetTilesAsync(_viewMode, "done", _doneLimit, searchParam);

            var readyCountTask = _api.GetTilesAsync(_viewMode, "ready", 10000, searchParam);
            var startedCountTask = _api.GetTilesAsync(_viewMode, "started", 10000, searchParam);
            var doneCountTask = _api.GetTilesAsync(_viewMode, "done", 10000, searchParam);

            await Task.WhenAll(readyTask, startedTask, doneTask, readyCountTask, startedCountTask, doneCountTask);

            ReadyTiles.Clear();
            if (readyTask.Result?.Tiles != null)
            {
                foreach (var tile in readyTask.Result.Tiles)
                    ReadyTiles.Add(ToTileListItem(tile));
            }

            StartedTiles.Clear();
            if (startedTask.Result?.Tiles != null)
            {
                foreach (var tile in startedTask.Result.Tiles)
                    StartedTiles.Add(ToTileListItem(tile));
            }

            DoneTiles.Clear();
            if (doneTask.Result?.Tiles != null)
            {
                foreach (var tile in doneTask.Result.Tiles)
                    DoneTiles.Add(ToTileListItem(tile));
            }

            _readyTotal = readyCountTask.Result?.Tiles?.Count ?? 0;
            _startedTotal = startedCountTask.Result?.Tiles?.Count ?? 0;
            _doneTotal = doneCountTask.Result?.Tiles?.Count ?? 0;

            ReadyCountDisplay = _readyTotal.ToString();
            StartedCountDisplay = _startedTotal.ToString();
            DoneCountDisplay = _doneTotal.ToString();

            ReadyMoreDisplay = _readyTotal > _readyLimit ? $"他{_readyTotal - _readyLimit}件 ▼" : string.Empty;
            StartedMoreDisplay = _startedTotal > _startedLimit ? $"他{_startedTotal - _startedLimit}件 ▼" : string.Empty;
            DoneMoreDisplay = _doneTotal > _doneLimit ? $"他{_doneTotal - _doneLimit}件 ▼" : string.Empty;

            var showByState = _viewMode == "by_state";
            ReadySection.Visibility = showByState ? Visibility.Visible : Visibility.Collapsed;
            StartedSection.Visibility = showByState ? Visibility.Visible : Visibility.Collapsed;
            DoneSection.Visibility = showByState ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TilesWindow] RefreshTilesAsync error: {ex.Message}");
        }
    }

    private static TileListItem ToTileListItem(TileView tv) => TileListItemMapper.Map(tv);

    private void OnViewByStateClick(object sender, RoutedEventArgs e)
    {
        _viewMode = "by_state";
        _ = RefreshTilesAsync();
    }

    private void OnViewByGroupClick(object sender, RoutedEventArgs e)
    {
        _viewMode = "by_group";
        _ = RefreshTilesAsync();
    }

    private void OnViewByProjectClick(object sender, RoutedEventArgs e)
    {
        _viewMode = "by_project";
        _ = RefreshTilesAsync();
    }

    private void OnViewByTagClick(object sender, RoutedEventArgs e)
    {
        _viewMode = "by_tag";
        _ = RefreshTilesAsync();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            _searchQuery = textBox.Text;
            _ = RefreshTilesAsync();
        }
    }

    private void OnReadyHeaderClick(object sender, RoutedEventArgs e)
    {
        _readyLimit = _readyLimit >= 64 ? 4 : _readyLimit * 4;
        _ = RefreshTilesAsync();
    }

    private void OnStartedHeaderClick(object sender, RoutedEventArgs e)
    {
        _startedLimit = _startedLimit >= 64 ? 4 : _startedLimit * 4;
        _ = RefreshTilesAsync();
    }

    private void OnDoneHeaderClick(object sender, RoutedEventArgs e)
    {
        _doneLimit = _doneLimit >= 64 ? 4 : _doneLimit * 4;
        _ = RefreshTilesAsync();
    }

    private async void OnTileStatusClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not string tileId || string.IsNullOrWhiteSpace(tileId)) return;

        var allTiles = ReadyTiles.Concat(StartedTiles).Concat(DoneTiles);
        var tile = allTiles.FirstOrDefault(t => t.Id == tileId);
        if (tile == null) return;

        var lifecycle = tile.Lifecycle?.Trim().ToLowerInvariant();
        if (lifecycle == "ready" || lifecycle == "started")
        {
            await RequestPromptForTileAsync(tileId);
        }
        else if (lifecycle == "done")
        {
            var api = new CoreApiClient(
                getAccessToken: Services.AuthService.Instance.GetAccessTokenAsync,
                refreshTokens: Services.CognitoAuthService.Instance.RefreshAsync);
            await api.StartTileAsync(tileId);
            await RefreshTilesAsync();
        }
    }

    private async Task RequestPromptForTileAsync(string tileId)
    {
        try
        {
            var response = await _api.RequestPromptAsync(tileId);
            if (response?.Ok == true && response.Prompt != null)
            {
                _promptToast.ShowPrompt(
                    response.Prompt,
                    5,
                    async (actionId, stopAt) =>
                    {
                        _promptToast.Hide();
                        var dispatch = await PromptActionDispatcher.ExecuteAsync(
                            _api,
                            response.Prompt,
                            actionId,
                            stopAt,
                            fallbackTileId: tileId,
                            defaultBreakMinutes: _settings.Current.DefaultBreakMinutes);
                        if (!dispatch.IsResolved)
                        {
                            App.DebugLog($"[TilesWindow] Unknown prompt action: {actionId}");
                        }
                        else if (!string.IsNullOrWhiteSpace(dispatch.Error))
                        {
                            App.DebugLog($"[TilesWindow] Prompt action failed: {dispatch.ResolvedActionId}, error: {dispatch.Error}");
                        }
                        await RefreshTilesAsync();
                    });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TilesWindow] RequestPromptForTileAsync error: {ex.Message}");
        }
    }

    private async void OnTileEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not string tileId) return;

        var freshTile = await _api.GetEditableTileByIdAsync(tileId);
        if (freshTile == null) return;

        var editTileId = freshTile.Id;
        var createWindow = new CreateTileWindow(editTileId, freshTile);
        createWindow.Closed += async (_, _) => await RefreshTilesAsync();
        createWindow.Activate();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _liveUpdateBridge.Dispose();
        Closed -= OnWindowClosed;
    }
}