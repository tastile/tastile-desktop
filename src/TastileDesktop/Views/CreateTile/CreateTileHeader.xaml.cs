using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TastileDesktop.Resources;

namespace TastileDesktop.Views.CreateTile;

public sealed partial class CreateTileHeader : UserControl
{
    public static readonly DependencyProperty TitleTextProperty =
        DependencyProperty.Register(nameof(TitleText), typeof(string), typeof(CreateTileHeader),
            new PropertyMetadata(string.Empty, OnTitleTextChanged));

    public static readonly DependencyProperty TitlePlaceholderProperty =
        DependencyProperty.Register(nameof(TitlePlaceholder), typeof(string), typeof(CreateTileHeader),
            new PropertyMetadata(Strings.Get("CreateTile.TitlePlaceholder"), OnTitlePlaceholderChanged));

    public static readonly DependencyProperty SubmitTextProperty =
        DependencyProperty.Register(nameof(SubmitText), typeof(string), typeof(CreateTileHeader),
            new PropertyMetadata(Strings.Get("CreateTile.PrimaryButton"), OnSubmitTextChanged));

    public static readonly DependencyProperty IsSubmitEnabledProperty =
        DependencyProperty.Register(nameof(IsSubmitEnabled), typeof(bool), typeof(CreateTileHeader),
            new PropertyMetadata(true, OnIsSubmitEnabledChanged));

    public event EventHandler<string>? TitleChanged;
    public event EventHandler? CloseRequested;
    public event EventHandler? SubmitRequested;

    /// <summary>
    /// Submit button rendered in the title row. Exposed as `CreateButton`
    /// so the window's existing test contract (CreateButton.Content = ...)
    /// keeps working unchanged.
    /// </summary>
    public Button CreateButton => SubmitButton;

    public string TitleText
    {
        get => (string)GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
    }

    public string TitlePlaceholder
    {
        get => (string)GetValue(TitlePlaceholderProperty);
        set => SetValue(TitlePlaceholderProperty, value);
    }

    public string SubmitText
    {
        get => (string)GetValue(SubmitTextProperty);
        set => SetValue(SubmitTextProperty, value);
    }

    public bool IsSubmitEnabled
    {
        get => (bool)GetValue(IsSubmitEnabledProperty);
        set => SetValue(IsSubmitEnabledProperty, value);
    }

    public CreateTileHeader()
    {
        InitializeComponent();
        SubmitButton.Content = SubmitText;
        TitleInput.PlaceholderText = TitlePlaceholder;
        SubmitButton.IsEnabled = IsSubmitEnabled;
    }

    private static void OnTitleTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CreateTileHeader header) return;
        if (header.TitleInput.Text != (string)e.NewValue)
        {
            header.TitleInput.Text = (string)e.NewValue ?? string.Empty;
        }
    }

    private static void OnTitlePlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CreateTileHeader header)
        {
            header.TitleInput.PlaceholderText = (string)e.NewValue;
        }
    }

    private static void OnSubmitTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CreateTileHeader header)
        {
            header.SubmitButton.Content = (string)e.NewValue;
        }
    }

    private static void OnIsSubmitEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CreateTileHeader header)
        {
            header.SubmitButton.IsEnabled = (bool)e.NewValue;
        }
    }

    private void OnTitleTextChanged(object sender, TextChangedEventArgs e)
    {
        var text = TitleInput.Text ?? string.Empty;
        if (TitleText != text)
        {
            TitleText = text;
            TitleChanged?.Invoke(this, text);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnSubmitClick(object sender, RoutedEventArgs e) => SubmitRequested?.Invoke(this, EventArgs.Empty);

    public void FocusTitle() => TitleInput.Focus(FocusState.Programmatic);
}
