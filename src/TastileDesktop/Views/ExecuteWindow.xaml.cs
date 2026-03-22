using Microsoft.UI.Xaml;
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
}
