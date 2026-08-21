using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TastileDesktop.Views.CreateTile.Sections;

public sealed partial class DateTimeRow : UserControl
{
    public static readonly DependencyProperty DateValueProperty =
        DependencyProperty.Register(nameof(DateValue), typeof(DateTimeOffset?), typeof(DateTimeRow),
            new PropertyMetadata(null, OnDateValueChanged));

    public static readonly DependencyProperty TimeValueProperty =
        DependencyProperty.Register(nameof(TimeValue), typeof(TimeSpan?), typeof(DateTimeRow),
            new PropertyMetadata(null, OnTimeValueChanged));

    public static readonly DependencyProperty TimeVisibleProperty =
        DependencyProperty.Register(nameof(TimeVisible), typeof(bool), typeof(DateTimeRow),
            new PropertyMetadata(true, OnTimeVisibleChanged));

    public event EventHandler<DateTimeOffset?>? DateChanged;
    public event EventHandler<TimeSpan?>? TimeChanged;

    public DateTimeOffset? DateValue
    {
        get => (DateTimeOffset?)GetValue(DateValueProperty);
        set => SetValue(DateValueProperty, value);
    }

    public TimeSpan? TimeValue
    {
        get => (TimeSpan?)GetValue(TimeValueProperty);
        set => SetValue(TimeValueProperty, value);
    }

    public bool TimeVisible
    {
        get => (bool)GetValue(TimeVisibleProperty);
        set => SetValue(TimeVisibleProperty, value);
    }

    public DateTimeRow()
    {
        InitializeComponent();
        DatePicker.DateChanged += (_, e) => DateChanged?.Invoke(this, e.NewDate);
        TimePicker.TimeChanged += (_, e) => TimeChanged?.Invoke(this, e.NewTime);
        TimePicker.Visibility = TimeVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void OnDateValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DateTimeRow row && e.NewValue is DateTimeOffset date)
        {
            if (row.DatePicker.Date != date) row.DatePicker.Date = date;
        }
    }

    private static void OnTimeValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DateTimeRow row && e.NewValue is TimeSpan time)
        {
            if (row.TimePicker.Time != time) row.TimePicker.Time = time;
        }
    }

    private static void OnTimeVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DateTimeRow row)
        {
            row.TimePicker.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
