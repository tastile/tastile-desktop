using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using TastileDesktop.Models;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;

namespace TastileDesktop.Views;

/// <summary>
/// Settings window for Tastile.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; } = new();
    private readonly SystemAppearanceService _appearanceService = SystemAppearanceService.Instance;

    public SettingsWindow()
    {
        this.InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 520, 700);
        ViewModel.UpdateSystemAppearance(_appearanceService.GetCurrentSnapshot());
        _appearanceService.AppearanceChanged += OnAppearanceChanged;
        Closed += OnClosed;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // Close the window after saving
        this.Close();
    }

    private async void OnTestPromptOverlayClick(object sender, RoutedEventArgs e)
    {
        var seconds = Math.Clamp(ViewModel.PromptOverlayDurationSeconds, 1, 15);
        await PromptAttentionOverlayService.Current.ShowTestOverlayAsync(TimeSpan.FromSeconds(seconds));
    }

    private void OnTestPromptToastClick(object sender, RoutedEventArgs e)
    {
        var testPrompt = new Models.PromptView(
            Guid.NewGuid().ToString(),
            "test",
            null,
            null,
            "Test Prompt",
            "This is a test toast notification",
            "",
            null,
            new List<Models.PromptActionView>
            {
                new("start", "開始"),
                new("defer", "先送り"),
                new("complete", "完了"),
            },
            null,
            false
        );

        PromptToastDisplayService.Instance.ShowPrompt(
            testPrompt,
            Math.Clamp(ViewModel.PromptToastMaxVisible, 1, 5),
            async actionId =>
            {
                System.Diagnostics.Debug.WriteLine($"Toast action: {actionId}");
            });
    }

    private void OnAppearanceChanged(object? sender, SystemAppearanceSnapshot snapshot)
    {
        DispatcherQueue.TryEnqueue(() => ViewModel.UpdateSystemAppearance(snapshot));
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _appearanceService.AppearanceChanged -= OnAppearanceChanged;
        Closed -= OnClosed;
    }
}
