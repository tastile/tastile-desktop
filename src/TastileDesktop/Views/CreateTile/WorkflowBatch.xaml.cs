using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using TastileDesktop.Models;
using TastileDesktop.Resources;
using TastileDesktop.Services;

namespace TastileDesktop.Views.CreateTile;

public sealed partial class WorkflowBatch : UserControl
{
    public static readonly DependencyProperty ActiveKindProperty =
        DependencyProperty.Register(nameof(ActiveKind), typeof(CreateTileWorkflowKind), typeof(WorkflowBatch),
            new PropertyMetadata(CreateTileWorkflowKind.Task, OnActiveKindChanged));

    public event EventHandler<CreateTileWorkflowKind>? SelectionChanged;

    public CreateTileWorkflowKind ActiveKind
    {
        get => (CreateTileWorkflowKind)GetValue(ActiveKindProperty);
        set => SetValue(ActiveKindProperty, value);
    }

    private static readonly (CreateTileWorkflowKind Kind, string LabelKey, string DescKey)[] Entries =
    {
        (CreateTileWorkflowKind.Event, "CreateTile.WorkflowEvent", "CreateTile.WorkflowEventDescription"),
        (CreateTileWorkflowKind.Task, "CreateTile.WorkflowTask", "CreateTile.WorkflowTaskDescription"),
        (CreateTileWorkflowKind.Recurring, "CreateTile.WorkflowRecurring", "CreateTile.WorkflowRecurringDescription"),
        (CreateTileWorkflowKind.Detailed, "CreateTile.WorkflowDetailed", "CreateTile.WorkflowDetailedDescription"),
    };

    public WorkflowBatch()
    {
        InitializeComponent();
        Loaded += (_, _) => Rebuild();
    }

    private static void OnActiveKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WorkflowBatch batch) batch.ApplyActive();
    }

    public IReadOnlyList<CreateTileWorkflowKind> AvailableKinds { get; set; } = Entries.Select(static e => e.Kind).ToList();

    private void Rebuild()
    {
        ItemsHost.Children.Clear();
        foreach (var (kind, labelKey, _) in Entries)
        {
            if (!AvailableKinds.Contains(kind)) continue;
            var pill = new ToggleButton
            {
                Content = Strings.Get(labelKey),
                Tag = kind,
                Style = Application.Current?.Resources["SelectorButtonStyle"] as Style,
                Padding = new Thickness(12, 4, 12, 4),
                MinHeight = 28,
                CornerRadius = new CornerRadius(14),
            };
            pill.Click += OnPillClick;
            ItemsHost.Children.Add(pill);
        }
        ApplyActive();
    }

    private void OnPillClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn || btn.Tag is not CreateTileWorkflowKind kind) return;
        if (ActiveKind == kind) return;
        ActiveKind = kind;
        SelectionChanged?.Invoke(this, kind);
    }

    private void ApplyActive()
    {
        for (var i = 0; i < ItemsHost.Children.Count; i++)
        {
            if (ItemsHost.Children[i] is ToggleButton btn && btn.Tag is CreateTileWorkflowKind kind)
            {
                btn.IsChecked = kind == ActiveKind;
            }
        }
    }
}
