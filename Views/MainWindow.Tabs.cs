using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MultiShell.ViewModels;

namespace MultiShell.Views;

public partial class MainWindow
{
    private TerminalTabViewModel? _draggedTab;
    private Point _dragStartPos;
    private bool _isDragging;
    private double _tabWheelAccumulator;

    private void OnTabMenuButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button btn && btn.Classes.Contains("tabMenuItemBtn"))
        {
            if (TabListMenuButton?.Flyout is Flyout flyout)
            {
                flyout.Hide();
            }
            ScrollSelectedTabIntoView();
        }
    }

    private void UpdateTabOverflowIndicators()
    {
        if (TabsScrollViewer == null) return;

        var extent = TabsScrollViewer.Extent.Width;
        var viewport = TabsScrollViewer.Viewport.Width;
        var offset = TabsScrollViewer.Offset.X;

        bool hasOverflow = extent > viewport + 1;
        bool canScrollLeft = offset > 1;
        bool canScrollRight = extent - (offset + viewport) > 1;

        if (TabScrollLeftBtn != null)
        {
            TabScrollLeftBtn.IsVisible = hasOverflow;
            TabScrollLeftBtn.IsEnabled = canScrollLeft;
            TabScrollLeftBtn.Opacity = canScrollLeft ? 1.0 : 0.4;
        }

        if (TabScrollRightBtn != null)
        {
            TabScrollRightBtn.IsVisible = hasOverflow;
            TabScrollRightBtn.IsEnabled = canScrollRight;
            TabScrollRightBtn.Opacity = canScrollRight ? 1.0 : 0.4;
        }

        if (LeftEdgeFade != null)
        {
            LeftEdgeFade.IsVisible = canScrollLeft;
        }

        if (RightEdgeFade != null)
        {
            RightEdgeFade.IsVisible = canScrollRight;
        }
    }

    private void ScrollTabsBy(double delta)
    {
        if (TabsScrollViewer == null) return;
        var maxOffset = Math.Max(0, TabsScrollViewer.Extent.Width - TabsScrollViewer.Viewport.Width);
        var newX = Math.Clamp(TabsScrollViewer.Offset.X + delta, 0, maxOffset);
        TabsScrollViewer.Offset = new Vector(newX, TabsScrollViewer.Offset.Y);
        UpdateTabOverflowIndicators();
    }

    private void ScrollSelectedTabIntoView()
    {
        if (TabsScrollViewer == null || TabsItemsControl == null || DataContext is not MainViewModel vm || vm.SelectedTab == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            var containers = TabsItemsControl.GetRealizedContainers();
            foreach (var container in containers)
            {
                if (container.DataContext == vm.SelectedTab)
                {
                    var transform = container.TransformToVisual(TabsScrollViewer);
                    if (transform.HasValue)
                    {
                        var rect = new Rect(0, 0, container.Bounds.Width, container.Bounds.Height);
                        var boundsInViewer = rect.TransformToAABB(transform.Value);

                        if (boundsInViewer.Left < 0)
                        {
                            var newX = Math.Max(0, TabsScrollViewer.Offset.X + boundsInViewer.Left - 10);
                            TabsScrollViewer.Offset = new Vector(newX, TabsScrollViewer.Offset.Y);
                        }
                        else if (boundsInViewer.Right > TabsScrollViewer.Viewport.Width)
                        {
                            var maxOffset = Math.Max(0, TabsScrollViewer.Extent.Width - TabsScrollViewer.Viewport.Width);
                            var delta = boundsInViewer.Right - TabsScrollViewer.Viewport.Width + 10;
                            var newX = Math.Min(maxOffset, TabsScrollViewer.Offset.X + delta);
                            TabsScrollViewer.Offset = new Vector(newX, TabsScrollViewer.Offset.Y);
                        }
                    }
                    break;
                }
            }
            UpdateTabOverflowIndicators();
        }, DispatcherPriority.Loaded);
    }

    private void OnTabsPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var visual = e.Source as Visual;
        if (IsCloseButtonClicked(visual)) return;

        var tabVm = FindTabViewModel(visual);
        if (tabVm != null)
        {
            _draggedTab = tabVm;
            _dragStartPos = e.GetPosition(this);
            _isDragging = false;
        }
    }

    private void OnTabsPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedTab == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _draggedTab = null;
            _isDragging = false;
            return;
        }

        var currentPos = e.GetPosition(this);
        var delta = currentPos - _dragStartPos;

        if (!_isDragging && (Math.Abs(delta.X) > 6 || Math.Abs(delta.Y) > 6))
        {
            _isDragging = true;
        }

        if (_isDragging && DataContext is MainViewModel mainVm)
        {
            var hitVisual = this.InputHitTest(currentPos) as Visual;
            var hoverTab = FindTabViewModel(hitVisual);

            if (hoverTab != null && hoverTab != _draggedTab)
            {
                mainVm.MoveTab(_draggedTab, hoverTab);
            }
        }
    }

    private void OnTabsPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _draggedTab = null;
        _isDragging = false;
    }

    private void OnTabsPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _draggedTab = null;
        _isDragging = false;
    }

    private void OnTabsPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.Tabs.Count <= 1) return;

        // Extract wheel movement (Delta.Y for vertical wheel, Delta.X for horizontal wheel/tilt)
        var delta = e.Delta.Y != 0 ? e.Delta.Y : -e.Delta.X;
        if (Math.Abs(delta) < 0.001) return;

        // Reset accumulator if scrolling direction changed
        if ((_tabWheelAccumulator > 0 && delta < 0) || (_tabWheelAccumulator < 0 && delta > 0))
        {
            _tabWheelAccumulator = 0;
        }

        _tabWheelAccumulator += delta;

        // Process discrete notches: each step consumes ~1.0 notch
        while (_tabWheelAccumulator >= 0.90)
        {
            vm.SelectPreviousTab();
            _tabWheelAccumulator -= 1.0;
            if (_tabWheelAccumulator < 0) _tabWheelAccumulator = 0;
        }

        while (_tabWheelAccumulator <= -0.90)
        {
            vm.SelectNextTab();
            _tabWheelAccumulator += 1.0;
            if (_tabWheelAccumulator > 0) _tabWheelAccumulator = 0;
        }

        e.Handled = true;
    }

    private static TerminalTabViewModel? FindTabViewModel(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Button btn && btn.Classes.Contains("tabBtn") && btn.DataContext is TerminalTabViewModel vm)
            {
                return vm;
            }
            visual = visual.GetVisualParent();
        }
        return null;
    }

    private static bool IsCloseButtonClicked(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Button btn && btn.Content is string s && s == "✕")
            {
                return true;
            }
            if (visual is Button btnTab && btnTab.Classes.Contains("tabBtn"))
            {
                return false;
            }
            visual = visual.GetVisualParent();
        }
        return false;
    }

    internal void OnTabBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed || e.ClickCount != 2) return;

        var visual = e.Source as Visual;
        if (IsInteractiveTabControl(visual)) return;

        if (DataContext is MainViewModel vm)
        {
            e.Handled = true;
            Dispatcher.UIThread.Post(() => vm.AddNewTabCommand.Execute(null));
        }
    }

    internal static bool IsInteractiveTabControl(Visual? visual)
    {
        int depth = 0;
        var visited = new HashSet<Visual>();
        while (visual != null && depth++ < 50 && visited.Add(visual))
        {
            if (visual is Button)
            {
                return true;
            }
            visual = visual.GetVisualParent() ?? (visual as Avalonia.LogicalTree.ILogical)?.LogicalParent as Visual;
        }
        return false;
    }
}
