using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TastileDesktop.Views.CreateTile.Sections;

public sealed partial class PanelErrorBanner : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PanelErrorBanner),
            new PropertyMetadata(string.Empty, OnTitleChanged));

    public static readonly DependencyProperty BodyProperty =
        DependencyProperty.Register(nameof(Body), typeof(string), typeof(PanelErrorBanner),
            new PropertyMetadata(string.Empty, OnBodyChanged));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Body
    {
        get => (string)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public PanelErrorBanner()
    {
        InitializeComponent();
        TitleTextBlock.Text = Title;
        BodyTextBlock.Text = Body;
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PanelErrorBanner b) b.TitleTextBlock.Text = (string)e.NewValue;
    }

    private static void OnBodyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PanelErrorBanner b) b.BodyTextBlock.Text = (string)e.NewValue;
    }
}
