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
    private TerminalTabViewModel? _currentTabVm;

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

        if (_currentTabVm != null)
        {
            _currentTabVm.LayoutChanged -= OnTabViewModelLayoutChanged;
            _currentTabVm = null;
        }

        if (DataContext is TerminalTabViewModel tabVm)
        {
            _currentTabVm = tabVm;
            _currentTabVm.LayoutChanged += OnTabViewModelLayoutChanged;
        }

        RebuildVisualTree();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_currentTabVm != null)
        {
            _currentTabVm.LayoutChanged -= OnTabViewModelLayoutChanged;
            _currentTabVm = null;
        }
    }

    private void OnTabViewModelLayoutChanged()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(RebuildVisualTree);
    }

    public void RebuildVisualTree()
    {
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
            _viewCache.Remove(key);
        }
    }

    private Control BuildNode(IPaneNodeViewModel? node)
    {
        if (node == null) return new Panel();

        if (node is LeafPaneNodeViewModel leaf)
        {
            if (!_viewCache.TryGetValue(leaf.Pane, out var paneView))
            {
                paneView = new TerminalPaneView { DataContext = leaf.Pane };
                _viewCache[leaf.Pane] = paneView;
            }
            else if (paneView.Parent is Panel parentPanel)
            {
                parentPanel.Children.Remove(paneView);
            }
            else if (paneView.Parent is ContentControl parentCc)
            {
                parentCc.Content = null;
            }

            return paneView;
        }

        if (node is SplitPaneNodeViewModel split)
        {
            var grid = new Grid();
            var first = BuildNode(split.FirstChild);
            var second = BuildNode(split.SecondChild);

            if (split.Orientation == PaneSplitDirection.Vertical)
            {
                var col1 = new ColumnDefinition(new GridLength(split.SplitRatio, GridUnitType.Star));
                var colSplitter = new ColumnDefinition(GridLength.Auto);
                var col2 = new ColumnDefinition(new GridLength(1.0 - split.SplitRatio, GridUnitType.Star));

                grid.ColumnDefinitions.Add(col1);
                grid.ColumnDefinitions.Add(colSplitter);
                grid.ColumnDefinitions.Add(col2);

                Grid.SetColumn(first, 0);

                var splitter = new GridSplitter
                {
                    Width = 4,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                    Background = new SolidColorBrush(Color.Parse("#252836")),
                    Cursor = new Cursor(StandardCursorType.SizeWestEast)
                };
                Grid.SetColumn(splitter, 1);

                Grid.SetColumn(second, 2);

                grid.Children.Add(first);
                grid.Children.Add(splitter);
                grid.Children.Add(second);

                split.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(SplitPaneNodeViewModel.SplitRatio))
                    {
                        col1.Width = new GridLength(split.SplitRatio, GridUnitType.Star);
                        col2.Width = new GridLength(1.0 - split.SplitRatio, GridUnitType.Star);
                    }
                };
            }
            else // Horizontal
            {
                var row1 = new RowDefinition(new GridLength(split.SplitRatio, GridUnitType.Star));
                var rowSplitter = new RowDefinition(GridLength.Auto);
                var row2 = new RowDefinition(new GridLength(1.0 - split.SplitRatio, GridUnitType.Star));

                grid.RowDefinitions.Add(row1);
                grid.RowDefinitions.Add(rowSplitter);
                grid.RowDefinitions.Add(row2);

                Grid.SetRow(first, 0);

                var splitter = new GridSplitter
                {
                    Height = 4,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Background = new SolidColorBrush(Color.Parse("#252836")),
                    Cursor = new Cursor(StandardCursorType.SizeNorthSouth)
                };
                Grid.SetRow(splitter, 1);

                Grid.SetRow(second, 2);

                grid.Children.Add(first);
                grid.Children.Add(splitter);
                grid.Children.Add(second);

                split.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(SplitPaneNodeViewModel.SplitRatio))
                    {
                        row1.Height = new GridLength(split.SplitRatio, GridUnitType.Star);
                        row2.Height = new GridLength(1.0 - split.SplitRatio, GridUnitType.Star);
                    }
                };
            }

            return grid;
        }

        return new Panel();
    }
}
