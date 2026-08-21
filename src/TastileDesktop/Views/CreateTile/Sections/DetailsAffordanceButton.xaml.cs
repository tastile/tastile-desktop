using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TastileDesktop.Resources;

namespace TastileDesktop.Views.CreateTile.Sections;

public sealed partial class DetailsAffordanceButton : UserControl
{
    public static readonly DependencyProperty LabelKeyProperty =
        DependencyProperty.Register(nameof(LabelKey), typeof(string), typeof(DetailsAffordanceButton),
            new PropertyMetadata("CreateTile.DetailsTaskTitle", OnLabelKeyChanged));

    public event EventHandler? Clicked;

    public string LabelKey
    {
        get => (string)GetValue(LabelKeyProperty);
        set => SetValue(LabelKeyProperty, value);
    }

    public DetailsAffordanceButton()
    {
        InitializeComponent();
        LabelTextBlock.Text = Strings.Get(LabelKey);
    }

    private static void OnLabelKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DetailsAffordanceButton b) b.LabelTextBlock.Text = Strings.Get((string)e.NewValue);
    }

    private void OnClick(object sender, RoutedEventArgs e) => Clicked?.Invoke(this, EventArgs.Empty);
}
