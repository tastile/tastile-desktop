using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TastileDesktop.Resources;

namespace TastileDesktop.Views.CreateTile.Sections;

public sealed partial class MemoSection : UserControl
{
    public static readonly DependencyProperty MemoProperty =
        DependencyProperty.Register(nameof(Memo), typeof(string), typeof(MemoSection),
            new PropertyMetadata(string.Empty, OnMemoChanged));

    public event EventHandler<string>? MemoChanged;

    public string Memo
    {
        get => (string)GetValue(MemoProperty);
        set => SetValue(MemoProperty, value);
    }

    public MemoSection()
    {
        InitializeComponent();
        MemoInput.PlaceholderText = Strings.Get("CreateTile.MemoPlaceholder");
        MemoInput.TextChanged += (_, _) => MemoChanged?.Invoke(this, MemoInput.Text);
    }

    private static void OnMemoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MemoSection section && section.MemoInput.Text != (string)e.NewValue)
        {
            section.MemoInput.Text = (string)e.NewValue;
        }
    }
}
