using Microsoft.UI.Xaml;
using TastileDesktop.Services;

namespace TastileDesktop;

public sealed partial class MainWindow : Window
{
    private readonly CoreApiClient _apiClient = new();

    public MainWindow()
    {
        this.InitializeComponent();
    }

    private async void OnCheckHealth(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Checking...";
        var healthy = await _apiClient.CheckHealthAsync();
        StatusText.Text = healthy ? "API: Connected" : "API: Unreachable";
    }
}
