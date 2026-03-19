using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using TastileDesktop.Services;
using TastileDesktop.ViewModels;

namespace TastileDesktop.Views;

/// <summary>
/// Settings window for Tastile.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; } = new();

    public SettingsWindow()
    {
        this.InitializeComponent();
        FloatingWindowHelper.Configure(this, TitleBarArea, 520, 700);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // Close the window after saving
        this.Close();
    }
}
