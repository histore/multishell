using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
