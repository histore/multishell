using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MultiShell.Models;
using MultiShell.ViewModels;

namespace MultiShell.Views;

/// <summary>
/// Avalonia control that dynamically renders a binary tree of split panes (REQ-UI-006).
/// Caches TerminalPaneView instances to prevent UI teardown and preserve terminal scroll/selection buffers during layout operations.
/// </summary>
public class SplitPaneContainerControl : ContentControl
{
    public static readonly StyledProperty<IPaneNodeViewModel?> RootNodeProperty =
        AvaloniaProperty.Register<SplitPaneContainerControl, IPaneNodeViewModel?>(nameof(RootNode));

    private readonly Dictionary<TerminalPaneViewModel, TerminalPaneView> _viewCache = new();
    private readonly List<(SplitPaneNodeViewModel Node, System.ComponentModel.PropertyChangedEventHandler Handler)> _splitRatioSubscriptions = new();
    private TerminalTabViewModel? _currentTabVm;
    private bool _rebuildQueued;

    public IPaneNodeViewModel? RootNode
    {
        get => GetValue(RootNodeProperty);
        set => SetValue(RootNodeProperty, value);
    }

    static SplitPaneContainerControl()
    {
        RootNodeProperty.Changed.AddClassHandler<SplitPaneContainerControl>((control, e) =>
        {
            control.RebuildVisualTree();
        });
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        SubscribeCurrentTabVm();
        RebuildVisualTree();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeCurrentTabVm();
        RebuildVisualTree();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _rebuildQueued = false;
        UnsubscribeCurrentTabVm();
        UnsubscribeSplitNodes();
        Content = null;
        foreach (var view in _viewCache.Values)
        {
            view.DataContext = null;
        }
        _viewCache.Clear();
    }

    private void SubscribeCurrentTabVm()
    {
        var nextVm = DataContext as TerminalTabViewModel;
        if (ReferenceEquals(_currentTabVm, nextVm)) return;

        UnsubscribeCurrentTabVm();

        if (nextVm != null)
        {
            _currentTabVm = nextVm;
            _currentTabVm.LayoutChanged += OnTabViewModelLayoutChanged;
        }
    }

    private void UnsubscribeCurrentTabVm()
    {
        if (_currentTabVm != null)
        {
            _currentTabVm.LayoutChanged -= OnTabViewModelLayoutChanged;
            _currentTabVm = null;
        }
    }

    private void UnsubscribeSplitNodes()
    {
        foreach (var (node, handler) in _splitRatioSubscriptions)
        {
            node.PropertyChanged -= handler;
        }
        _splitRatioSubscriptions.Clear();
    }

    internal int ActiveSplitSubscriptionCount => _splitRatioSubscriptions.Count;
    internal bool IsSubscribedToTabLayout => _currentTabVm != null;
    internal int CachedViewCount => _viewCache.Count;

    private void OnTabViewModelLayoutChanged()
    {
        _rebuildQueued = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_rebuildQueued)
            {
                _rebuildQueued = false;
                RebuildVisualTree();
            }
        });
    }

    public void RebuildVisualTree()
    {
        _rebuildQueued = false;
        UnsubscribeSplitNodes();
        var root = _currentTabVm?.DisplayRootNode ?? RootNode;
        CleanupViewCache();
        Content = BuildNode(root);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var activeView = System.Linq.Enumerable.FirstOrDefault(_viewCache.Values, v => (v.DataContext as TerminalPaneViewModel)?.IsActive == true);
            activeView?.FocusTerminal();
        });
    }

    private void CleanupViewCache()
    {
        IReadOnlyCollection<TerminalPaneViewModel>? activePanes = null;
        if (_currentTabVm?.Panes != null)
        {
            activePanes = _currentTabVm.Panes;
        }
        else if (RootNode != null)
        {
            activePanes = new HashSet<TerminalPaneViewModel>(RootNode.GetLeaves());
        }

        if (activePanes == null)
        {
            foreach (var view in _viewCache.Values)
            {
                view.DataContext = null;
            }
            _viewCache.Clear();
            return;
        }

        var activeSet = new HashSet<TerminalPaneViewModel>(activePanes);
        var toRemove = new List<TerminalPaneViewModel>();

        foreach (var key in _viewCache.Keys)
        {
            if (!activeSet.Contains(key))
            {
                toRemove.Add(key);
            }
        }

        foreach (var key in toRemove)
        {
            if (_viewCache.Remove(key, out var evictedView))
            {
                evictedView.DataContext = null;
            }
        }
    }

    private Control BuildNode(IPaneNodeViewModel? node)
    {
        if (node is LeafPaneNodeViewModel leaf)
        {
            return GetOrCreatePaneView(leaf.Pane);
        }

        if (node is SplitPaneNodeViewModel split)
        {
            return BuildSplitGrid(split);
        }

        return new Panel();
    }

    private TerminalPaneView GetOrCreatePaneView(TerminalPaneViewModel pane)
    {
        if (!_viewCache.TryGetValue(pane, out var paneView))
        {
            paneView = new TerminalPaneView { DataContext = pane };
            _viewCache[pane] = paneView;
            return paneView;
        }

        if (paneView.Parent is Panel parentPanel)
        {
            parentPanel.Children.Remove(paneView);
        }
        else if (paneView.Parent is ContentControl parentCc)
        {
            parentCc.Content = null;
        }

        return paneView;
    }

    private Grid BuildSplitGrid(SplitPaneNodeViewModel split)
    {
        var grid = new Grid();
        var first = BuildNode(split.FirstChild);
        var second = BuildNode(split.SecondChild);
        bool isVertical = split.Orientation == PaneSplitDirection.Vertical;
        var splitter = CreateSplitSplitter(isVertical);

        if (isVertical)
        {
            ConfigureVerticalSplitGrid(grid, split, first, splitter, second);
        }
        else
        {
            ConfigureHorizontalSplitGrid(grid, split, first, splitter, second);
        }

        grid.Children.Add(first);
        grid.Children.Add(splitter);
        grid.Children.Add(second);
        return grid;
    }

    private static GridSplitter CreateSplitSplitter(bool isVertical)
    {
        var splitter = new GridSplitter
        {
            Width = isVertical ? 4 : double.NaN,
            Height = isVertical ? double.NaN : 4,
            HorizontalAlignment = isVertical ? Avalonia.Layout.HorizontalAlignment.Center : Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = isVertical ? Avalonia.Layout.VerticalAlignment.Stretch : Avalonia.Layout.VerticalAlignment.Center,
            Cursor = new Cursor(isVertical ? StandardCursorType.SizeWestEast : StandardCursorType.SizeNorthSouth)
        };
        splitter.Bind(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty, splitter.GetResourceObservable("AppBorder"));
        return splitter;
    }

    private void ConfigureVerticalSplitGrid(Grid grid, SplitPaneNodeViewModel split, Control first, GridSplitter splitter, Control second)
    {
        var col1 = new ColumnDefinition(new GridLength(split.SplitRatio, GridUnitType.Star));
        var colSplitter = new ColumnDefinition(GridLength.Auto);
        var col2 = new ColumnDefinition(new GridLength(1.0 - split.SplitRatio, GridUnitType.Star));

        grid.ColumnDefinitions.Add(col1);
        grid.ColumnDefinitions.Add(colSplitter);
        grid.ColumnDefinitions.Add(col2);

        Grid.SetColumn(first, 0);
        Grid.SetColumn(splitter, 1);
        Grid.SetColumn(second, 2);

        bool isSyncingFromDrag = false;
        splitter.DragCompleted += (_, _) =>
        {
            if (TryComputeUpdatedSplitRatio(col1.Width, col1.ActualWidth, col2.Width, col2.ActualWidth, out double newRatio))
            {
                isSyncingFromDrag = true;
                try
                {
                    split.SplitRatio = newRatio;
                }
                finally
                {
                    isSyncingFromDrag = false;
                }
            }
        };

        System.ComponentModel.PropertyChangedEventHandler handler = (_, args) =>
        {
            if (isSyncingFromDrag || args.PropertyName != nameof(SplitPaneNodeViewModel.SplitRatio)) return;
            col1.Width = new GridLength(split.SplitRatio, GridUnitType.Star);
            col2.Width = new GridLength(1.0 - split.SplitRatio, GridUnitType.Star);
        };
        split.PropertyChanged += handler;
        _splitRatioSubscriptions.Add((split, handler));
    }

    private void ConfigureHorizontalSplitGrid(Grid grid, SplitPaneNodeViewModel split, Control first, GridSplitter splitter, Control second)
    {
        var row1 = new RowDefinition(new GridLength(split.SplitRatio, GridUnitType.Star));
        var rowSplitter = new RowDefinition(GridLength.Auto);
        var row2 = new RowDefinition(new GridLength(1.0 - split.SplitRatio, GridUnitType.Star));

        grid.RowDefinitions.Add(row1);
        grid.RowDefinitions.Add(rowSplitter);
        grid.RowDefinitions.Add(row2);

        Grid.SetRow(first, 0);
        Grid.SetRow(splitter, 1);
        Grid.SetRow(second, 2);

        bool isSyncingFromDrag = false;
        splitter.DragCompleted += (_, _) =>
        {
            if (TryComputeUpdatedSplitRatio(row1.Height, row1.ActualHeight, row2.Height, row2.ActualHeight, out double newRatio))
            {
                isSyncingFromDrag = true;
                try
                {
                    split.SplitRatio = newRatio;
                }
                finally
                {
                    isSyncingFromDrag = false;
                }
            }
        };

        System.ComponentModel.PropertyChangedEventHandler handler = (_, args) =>
        {
            if (isSyncingFromDrag || args.PropertyName != nameof(SplitPaneNodeViewModel.SplitRatio)) return;
            row1.Height = new GridLength(split.SplitRatio, GridUnitType.Star);
            row2.Height = new GridLength(1.0 - split.SplitRatio, GridUnitType.Star);
        };
        split.PropertyChanged += handler;
        _splitRatioSubscriptions.Add((split, handler));
    }

    internal static bool TryComputeUpdatedSplitRatio(
        GridLength firstLength,
        double firstActual,
        GridLength secondLength,
        double secondActual,
        out double updatedRatio)
    {
        double totalActual = firstActual + secondActual;
        if (firstActual > 0 && secondActual > 0 && totalActual > 0)
        {
            updatedRatio = Math.Clamp(firstActual / totalActual, 0.1, 0.9);
            return true;
        }

        if (firstLength.IsStar && secondLength.IsStar)
        {
            double totalStars = firstLength.Value + secondLength.Value;
            if (totalStars > 0)
            {
                updatedRatio = Math.Clamp(firstLength.Value / totalStars, 0.1, 0.9);
                return true;
            }
        }

        updatedRatio = 0.5;
        return false;
    }
}
