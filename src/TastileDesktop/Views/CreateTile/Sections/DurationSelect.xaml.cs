using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TastileDesktop.Resources;

namespace TastileDesktop.Views.CreateTile.Sections;

public sealed partial class DurationSelect : UserControl
{
    public const int CustomSentinel = -1;
    public static readonly int[] Presets = { 15, 30, 60, 90, 120 };

    public static readonly DependencyProperty MinutesProperty =
        DependencyProperty.Register(nameof(Minutes), typeof(int), typeof(DurationSelect),
            new PropertyMetadata(30, OnMinutesChanged));

    public event EventHandler<int>? MinutesChanged;

    public int Minutes
    {
        get => (int)GetValue(MinutesProperty);
        set => SetValue(MinutesProperty, value);
    }

    public DurationSelect()
    {
        InitializeComponent();
        BuildPresets();
    }

    private void BuildPresets()
    {
        PresetCombo.Items.Clear();
        foreach (var preset in Presets)
        {
            PresetCombo.Items.Add(new ComboBoxItem
            {
                Content = preset < 60
                    ? string.Format(Strings.Get("CreateTile.DurationMinute"), preset)
                    : string.Format(Strings.Get("CreateTile.DurationHour"), preset / 60),
                Tag = preset,
            });
        }
        PresetCombo.Items.Add(new ComboBoxItem
        {
            Content = Strings.Get("CreateTile.DurationCustom"),
            Tag = CustomSentinel,
        });
        ApplySelection();
    }

    private void ApplySelection()
    {
        for (var i = 0; i < PresetCombo.Items.Count; i++)
        {
            if (PresetCombo.Items[i] is ComboBoxItem item && item.Tag is int tag)
            {
                if (tag == Minutes)
                {
                    PresetCombo.SelectedIndex = i;
                    CustomBox.Visibility = Visibility.Collapsed;
                    return;
                }
            }
        }
        PresetCombo.SelectedIndex = PresetCombo.Items.Count - 1;
        CustomBox.Visibility = Visibility.Visible;
        CustomBox.Value = Math.Max(1, Minutes);
    }

    private static void OnMinutesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DurationSelect ds) ds.ApplySelection();
    }

    private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetCombo.SelectedItem is not ComboBoxItem item || item.Tag is not int tag) return;
        if (tag == CustomSentinel)
        {
            CustomBox.Visibility = Visibility.Visible;
            CustomBox.Focus(FocusState.Programmatic);
            return;
        }
        CustomBox.Visibility = Visibility.Collapsed;
        if (Minutes != tag)
        {
            Minutes = tag;
            MinutesChanged?.Invoke(this, tag);
        }
    }

    private void OnCustomValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!double.IsNaN(args.NewValue))
        {
            var minutes = Math.Max(1, (int)args.NewValue);
            if (Minutes != minutes)
            {
                Minutes = minutes;
                MinutesChanged?.Invoke(this, minutes);
            }
        }
    }
}
