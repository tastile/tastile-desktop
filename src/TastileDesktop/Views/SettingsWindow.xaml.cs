using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
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

        // Set window size
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(520, 700));
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // Close the window after saving
        this.Close();
    }
}
