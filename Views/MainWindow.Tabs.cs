using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MultiShell.Services;
using MultiShell.ViewModels;

namespace MultiShell.Views;

public partial class MainWindow
{
    private readonly IStartupPathResolver _startupPathResolver = new StartupPathResolver();
    private TerminalTabViewModel? _draggedTab;
    private Point _dragStartPos;
    private bool _isDragging;
    private double _tabWheelAccumulator;
    private DispatcherTimer? _dragScrollTimer;
    private double _dragScrollDelta;

    private static bool IsVisualOrChildOf(Visual? visual, Visual? target)
    {
        if (target == null || visual == null) return false;
        while (visual != null)
        {
            if (visual == target) return true;
            visual = visual.GetVisualParent();
        }
        return false;
    }

    private void StartDragScrollTimer(double delta)
    {
        if (_dragScrollTimer != null && Math.Sign(_dragScrollDelta) == Math.Sign(delta) && _dragScrollTimer.IsEnabled)
        {
            return;
        }

        StopDragScrollTimer();
        _dragScrollDelta = delta;

        bool isInitialTick = true;
        _dragScrollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _dragScrollTimer.Tick += (_, _) =>
        {
            if (isInitialTick)
            {
                isInitialTick = false;
                _dragScrollTimer.Interval = TimeSpan.FromMilliseconds(320);
            }

            ScrollTabsBy(_dragScrollDelta);

            if (TabsScrollViewer != null)
            {
                var offset = TabsScrollViewer.Offset.X;
                var maxOffset = Math.Max(0, TabsScrollViewer.Extent.Width - TabsScrollViewer.Viewport.Width);
                if ((_dragScrollDelta < 0 && offset <= 0) || (_dragScrollDelta > 0 && offset >= maxOffset))
                {
                    StopDragScrollTimer();
                }
            }
        };
        _dragScrollTimer.Start();
    }

    private void StopDragScrollTimer()
    {
        if (_dragScrollTimer != null)
        {
            _dragScrollTimer.Stop();
            _dragScrollTimer = null;
        }
    }

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

            if (IsVisualOrChildOf(hitVisual, TabScrollLeftBtn))
            {
                if (TabScrollLeftBtn?.IsEnabled == true)
                {
                    StartDragScrollTimer(-160);
                }
            }
            else if (IsVisualOrChildOf(hitVisual, TabScrollRightBtn))
            {
                if (TabScrollRightBtn?.IsEnabled == true)
                {
                    StartDragScrollTimer(160);
                }
            }
            else
            {
                StopDragScrollTimer();

                var hoverTab = FindTabViewModel(hitVisual);
                if (hoverTab != null && hoverTab != _draggedTab)
                {
                    mainVm.MoveTab(_draggedTab, hoverTab);
                }
            }
        }
    }

    private void OnTabsPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        StopDragScrollTimer();
        _draggedTab = null;
        _isDragging = false;
    }

    private void OnTabsPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        StopDragScrollTimer();
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

    private static bool HasFiles(DragEventArgs e)
    {
        return e.DataTransfer.Contains(DataFormat.File) || e.DataTransfer.TryGetFiles() != null;
    }

    private string? ExtractFirstResolvedDirectory(DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files != null)
        {
            foreach (var item in files)
            {
                var localPath = item.TryGetLocalPath() ?? (item.Path.IsFile ? item.Path.LocalPath : null);
                if (!string.IsNullOrWhiteSpace(localPath))
                {
                    var resolved = _startupPathResolver.ResolveWorkingDirectory(localPath);
                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
                        return resolved;
                    }
                }
            }
        }

        var text = e.DataTransfer.TryGetText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var resolved = _startupPathResolver.ResolveWorkingDirectory(text);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    internal void OnTerminalDragOver(object? sender, DragEventArgs e)
    {
        StopDragScrollTimer();
        if (HasFiles(e))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    internal void OnTerminalDrop(object? sender, DragEventArgs e)
    {
        StopDragScrollTimer();
        var resolvedDir = ExtractFirstResolvedDirectory(e);
        if (resolvedDir != null && DataContext is MainViewModel mainVm)
        {
            bool isShiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            mainVm.OpenDirectoryFromDrop(resolvedDir, openInNewTab: isShiftPressed);
            e.Handled = true;
        }
    }

    internal void OnTabBarDragOver(object? sender, DragEventArgs e)
    {
        if (HasFiles(e))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;

            var currentPos = e.GetPosition(this);
            var hitVisual = this.InputHitTest(currentPos) as Visual;

            if (IsVisualOrChildOf(hitVisual, TabScrollLeftBtn))
            {
                if (TabScrollLeftBtn?.IsEnabled == true)
                {
                    StartDragScrollTimer(-160);
                }
            }
            else if (IsVisualOrChildOf(hitVisual, TabScrollRightBtn))
            {
                if (TabScrollRightBtn?.IsEnabled == true)
                {
                    StartDragScrollTimer(160);
                }
            }
            else
            {
                StopDragScrollTimer();

                if (DataContext is MainViewModel mainVm)
                {
                    var hoverTab = FindTabViewModel(hitVisual);
                    if (hoverTab != null && hoverTab != mainVm.SelectedTab)
                    {
                        mainVm.SelectedTab = hoverTab;
                    }
                }
            }
        }
        else
        {
            StopDragScrollTimer();
            e.DragEffects = DragDropEffects.None;
        }
    }

    internal void OnTabBarDragLeave(object? sender, DragEventArgs e)
    {
        StopDragScrollTimer();
    }

    internal void OnTabBarDrop(object? sender, DragEventArgs e)
    {
        StopDragScrollTimer();
        var resolvedDir = ExtractFirstResolvedDirectory(e);
        if (resolvedDir != null && DataContext is MainViewModel mainVm)
        {
            var currentPos = e.GetPosition(this);
            var hitVisual = this.InputHitTest(currentPos) as Visual;
            var hoverTab = FindTabViewModel(hitVisual);
            bool isShiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            if (hoverTab != null)
            {
                mainVm.OpenDirectoryFromDrop(resolvedDir, openInNewTab: isShiftPressed, targetTab: hoverTab);
            }
            else
            {
                mainVm.OpenDirectoryFromDrop(resolvedDir, openInNewTab: true);
            }
            e.Handled = true;
        }
    }
}

