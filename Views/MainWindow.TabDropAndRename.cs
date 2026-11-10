using System;
using System.Linq;
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
                var resolved = TryResolveDirectory(localPath);
                if (resolved != null) return resolved;
            }
        }

        return TryResolveDirectory(e.DataTransfer.TryGetText());
    }

    private string? TryResolveDirectory(string? candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath)) return null;
        var resolved = _startupPathResolver.ResolveWorkingDirectory(candidatePath);
        return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
    }

    internal void OnTerminalDragOver(object? sender, DragEventArgs e)
    {
        StopDragScrollTimer();
        if (WindowCoordinatorService.CurrentTabDrag != null)
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

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

        if (WindowCoordinatorService.CurrentTabDrag is { } tabDrag)
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;

            var sourceVm = tabDrag.SourceWindow;
            var targetVm = DataContext as MainViewModel;
            var tab = tabDrag.Tab;

            if (targetVm != null && sourceVm != targetVm)
            {
                targetVm.WindowCoordinator.TransferTab(sourceVm, targetVm, tab);
            }
            return;
        }

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
        bool isTabDrag = WindowCoordinatorService.CurrentTabDrag != null;
        if (!isTabDrag && !HasFiles(e))
        {
            StopDragScrollTimer();
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = isTabDrag ? DragDropEffects.Move : DragDropEffects.Copy;
        e.Handled = true;

        var hitVisual = this.InputHitTest(e.GetPosition(this)) as Visual;
        if (TryHandleDragScrollButtonHover(hitVisual))
        {
            return;
        }

        StopDragScrollTimer();
        if (!isTabDrag && DataContext is MainViewModel mainVm)
        {
            var hoverTab = FindTabViewModel(hitVisual);
            if (hoverTab != null && hoverTab != mainVm.SelectedTab)
            {
                mainVm.SelectedTab = hoverTab;
            }
        }
    }

    private bool TryHandleDragScrollButtonHover(Visual? hitVisual)
    {
        if (IsVisualOrChildOf(hitVisual, TabScrollLeftBtn))
        {
            if (TabScrollLeftBtn?.IsEnabled == true) StartDragScrollTimer(-160);
            return true;
        }

        if (IsVisualOrChildOf(hitVisual, TabScrollRightBtn))
        {
            if (TabScrollRightBtn?.IsEnabled == true) StartDragScrollTimer(160);
            return true;
        }

        return false;
    }

    internal void OnTabBarDragLeave(object? sender, DragEventArgs e)
    {
        StopDragScrollTimer();
    }

    internal void OnTabBarDrop(object? sender, DragEventArgs e)
    {
        StopDragScrollTimer();

        if (WindowCoordinatorService.CurrentTabDrag is { } tabDrag)
        {
            HandleTabBarTabDrop(tabDrag, e);
            return;
        }

        var resolvedDir = ExtractFirstResolvedDirectory(e);
        if (resolvedDir == null || DataContext is not MainViewModel mainVm) return;

        var hitVisual = this.InputHitTest(e.GetPosition(this)) as Visual;
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

    private void HandleTabBarTabDrop(TabDragTransfer tabDrag, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;

        if (DataContext is not MainViewModel targetVm) return;

        var hitVisual = this.InputHitTest(e.GetPosition(this)) as Visual;
        var hoverTab = FindTabViewModel(hitVisual);
        int? targetIndex = hoverTab != null ? targetVm.Tabs.IndexOf(hoverTab) : null;

        if (tabDrag.SourceWindow == targetVm)
        {
            if (tabDrag.Tab is TerminalTabViewModel localTab && hoverTab != null && hoverTab != localTab)
            {
                targetVm.MoveTab(localTab, hoverTab);
            }
            return;
        }

        targetVm.WindowCoordinator.TransferTab(tabDrag.SourceWindow, targetVm, tabDrag.Tab, targetIndex);
    }

    /// <summary>
    /// Locates the inline rename TextBox in the tab bar, requests focus, and selects all text (REQ-TAB-020).
    /// </summary>
    public void FocusTabRenameBox()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (TabsItemsControl == null) return;
            var targetBox = TabsItemsControl.GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Classes.Contains("tabRenameBox") && tb.IsVisible);

            if (targetBox != null)
            {
                targetBox.BringIntoView();
                targetBox.Focus();
                targetBox.SelectAll();
            }
        }, DispatcherPriority.Input);
    }

    private void OnTabsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox tb && tb.Classes.Contains("tabRenameBox") && tb.DataContext is TerminalTabViewModel tabVm && tabVm.IsRenaming)
        {
            if (e.Key == Key.Enter)
            {
                tabVm.CommitRenaming();
                FocusActiveTerminal();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                tabVm.CancelRenaming();
                FocusActiveTerminal();
                e.Handled = true;
            }
        }
    }

    private void OnTabsLostFocus(object? sender, RoutedEventArgs e)
    {
        if (e.Source is TextBox tb && tb.Classes.Contains("tabRenameBox") && tb.DataContext is TerminalTabViewModel tabVm && tabVm.IsRenaming)
        {
            tabVm.CommitRenaming();
        }
    }

    internal void AttachTabRenameListeners(MainViewModel vm)
    {
        foreach (var tab in vm.Tabs)
        {
            tab.FocusRenameBoxRequested -= FocusTabRenameBox;
            tab.FocusRenameBoxRequested += FocusTabRenameBox;
        }

        vm.Tabs.CollectionChanged += (s, e) =>
        {
            if (e.OldItems != null)
            {
                foreach (TerminalTabViewModel tab in e.OldItems)
                {
                    tab.FocusRenameBoxRequested -= FocusTabRenameBox;
                }
            }

            if (e.NewItems != null)
            {
                foreach (TerminalTabViewModel tab in e.NewItems)
                {
                    tab.FocusRenameBoxRequested -= FocusTabRenameBox;
                    tab.FocusRenameBoxRequested += FocusTabRenameBox;
                }
            }
        };
    }
}
