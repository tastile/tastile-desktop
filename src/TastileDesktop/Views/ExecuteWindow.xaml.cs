using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;

namespace TastileDesktop.Views;

public sealed partial class ExecuteWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public ExecuteWindow()
    {
        InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 520, 720);
        _ = ViewModel.InitializeAsync();
    }

    private async void OnPromptActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string actionId)
        {
            await ViewModel.RespondToPromptAsync(actionId);
        }
    }
}
