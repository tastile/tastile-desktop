using Microsoft.UI.Xaml;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;

namespace TastileDesktop.Views;

public sealed partial class TimelineWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public TimelineWindow()
    {
        InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 520, 700);
        _ = ViewModel.InitializeAsync();
    }
}
