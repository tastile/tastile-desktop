using System.Collections;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace TastileDesktop.Controls;

public sealed class TabSelector : UserControl
{
    private readonly ObservableCollection<string> _items = [];
    private readonly Grid _root;
    private int _currentIndex = -1;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(TabSelector),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(TabSelector),
            new PropertyMetadata(-1, OnSelectedIndexChanged));

    public event EventHandler<int>? SelectionChanged;

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public TabSelector()
    {
        _root = new Grid();
        _root.ColumnSpacing = 4;
        Content = _root;
        _items.CollectionChanged += (_, _) => RebuildColumns();
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TabSelector ts) return;
        ts._items.Clear();
        if (e.NewValue is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
                ts._items.Add(item?.ToString() ?? "");
        }
    }

    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TabSelector ts) return;
        ts._currentIndex = (int)e.NewValue;
        ts.ApplyAllStates();
    }

    private void RebuildColumns()
    {
        _root.ColumnDefinitions.Clear();
        _root.Children.Clear();

        for (var i = 0; i < _items.Count; i++)
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (var i = 0; i < _items.Count; i++)
        {
            var btn = MakeButton(i, _items[i]);
            Grid.SetColumn(btn, i);
            _root.Children.Add(btn);
        }

        ApplyAllStates();
    }

    private ToggleButton MakeButton(int index, string label)
    {
        var btn = new ToggleButton
        {
            Content = label,
            Style = Application.Current?.Resources["SelectorButtonStyle"] as Style,
        };

        btn.Tag = index;
        btn.Click += OnClick;

        return btn;
    }

    private void OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn || btn.Tag is not int idx) return;
        if (idx == _currentIndex) return;
        _currentIndex = idx;
        SelectedIndex = idx;
        ApplyAllStates();
        SelectionChanged?.Invoke(this, idx);
    }

    private void ApplyAllStates()
    {
        for (var i = 0; i < _root.Children.Count; i++)
        {
            if (_root.Children[i] is ToggleButton btn)
            {
                btn.IsChecked = i == _currentIndex;
            }
        }
    }

    public void ApplyAccentToIndex(int index, bool active)
    {
        if (index < 0 || index >= _root.Children.Count) return;
        if (_root.Children[index] is not ToggleButton btn) return;
        btn.IsChecked = active;
    }
}
