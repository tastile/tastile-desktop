using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TastileDesktop.Resources;
using Windows.System;

namespace TastileDesktop.Views.CreateTile.Sections;

public sealed partial class ProjectColorRow : UserControl
{
    public static readonly DependencyProperty ProjectProperty =
        DependencyProperty.Register(nameof(Project), typeof(string), typeof(ProjectColorRow),
            new PropertyMetadata(string.Empty, OnProjectChanged));

    public static readonly DependencyProperty TagsProperty =
        DependencyProperty.Register(nameof(Tags), typeof(IList<string>), typeof(ProjectColorRow),
            new PropertyMetadata(null, OnTagsChanged));

    public static readonly DependencyProperty SwatchesProperty =
        DependencyProperty.Register(nameof(Swatches), typeof(IList<string>), typeof(ProjectColorRow),
            new PropertyMetadata(null, OnSwatchesChanged));

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(nameof(SelectedColor), typeof(string), typeof(ProjectColorRow),
            new PropertyMetadata(null, OnSelectedColorChanged));

    public event EventHandler<string>? ProjectChanged;
    public event EventHandler<IList<string>>? TagsChanged;
    public event EventHandler<string>? ColorChanged;

    public string Project
    {
        get => (string)GetValue(ProjectProperty);
        set => SetValue(ProjectProperty, value);
    }

    public IList<string>? Tags
    {
        get => (IList<string>?)GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    public IList<string>? Swatches
    {
        get => (IList<string>?)GetValue(SwatchesProperty);
        set => SetValue(SwatchesProperty, value);
    }

    public string? SelectedColor
    {
        get => (string?)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public ProjectColorRow()
    {
        InitializeComponent();
        ProjectInput.PlaceholderText = Strings.Get("CreateTile.ProjectPlaceholder");
        TagInput.PlaceholderText = Strings.Get("CreateTile.TagPlaceholder");
        ProjectInput.TextChanged += (_, _) =>
        {
            if (Project != ProjectInput.Text)
            {
                Project = ProjectInput.Text;
                ProjectChanged?.Invoke(this, ProjectInput.Text);
            }
        };
        RenderSwatches();
    }

    private static void OnProjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProjectColorRow row && row.ProjectInput.Text != (string)e.NewValue)
        {
            row.ProjectInput.Text = (string)e.NewValue;
        }
    }

    private static void OnTagsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProjectColorRow row) row.RenderTags();
    }

    private static void OnSwatchesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProjectColorRow row) row.RenderSwatches();
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProjectColorRow row) row.ApplySelectedSwatch();
    }

    private void RenderTags()
    {
        TagsHost.Children.Clear();
        foreach (var tag in Tags ?? Array.Empty<string>())
        {
            var chip = new Button
            {
                Content = string.Format(Strings.Get("CreateTile.TagChipLabel"), tag),
                Padding = new Thickness(8, 2, 8, 2),
                MinHeight = 28,
                Tag = tag,
            };
            chip.Click += (_, _) => RemoveTag(tag);
            TagsHost.Children.Add(chip);
        }
    }

    private void RemoveTag(string tag)
    {
        var next = new List<string>(Tags ?? Array.Empty<string>());
        next.Remove(tag);
        Tags = next;
        TagsChanged?.Invoke(this, next);
    }

    private void OnTagInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        var raw = TagInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(raw)) return;
        var next = new List<string>(Tags ?? Array.Empty<string>());
        if (!next.Contains(raw, StringComparer.CurrentCultureIgnoreCase)) next.Add(raw);
        Tags = next;
        TagsChanged?.Invoke(this, next);
        TagInput.Text = string.Empty;
    }

    private void RenderSwatches()
    {
        SwatchesHost.Children.Clear();
        foreach (var hex in Swatches ?? Array.Empty<string>())
        {
            var ellipse = new Ellipse
            {
                Width = 18,
                Height = 18,
                Fill = new SolidColorBrush(ParseColor(hex)),
            };
            var border = new Border
            {
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                Tag = hex,
                Child = ellipse,
            };
            var button = new Button
            {
                Content = border,
                Padding = new Thickness(2),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Tag = hex,
            };
            button.Click += OnSwatchClick;
            SwatchesHost.Children.Add(button);
        }
        ApplySelectedSwatch();
    }

    private void ApplySelectedSwatch()
    {
        foreach (var child in SwatchesHost.Children)
        {
            if (child is Button btn && btn.Tag is string hex && btn.Content is Border border)
            {
                border.BorderBrush = new SolidColorBrush(
                    SelectedColor is not null && string.Equals(SelectedColor, hex, StringComparison.OrdinalIgnoreCase)
                        ? Colors.White
                        : Colors.Transparent);
            }
        }
    }

    private void OnSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            SelectedColor = hex;
            ColorChanged?.Invoke(this, hex);
        }
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        byte a = System.Convert.ToByte(hex.Substring(0, 2), 16);
        byte r = System.Convert.ToByte(hex.Substring(2, 2), 16);
        byte g = System.Convert.ToByte(hex.Substring(4, 2), 16);
        byte b = System.Convert.ToByte(hex.Substring(6, 2), 16);
        return Windows.UI.Color.FromArgb(a, r, g, b);
    }
}
