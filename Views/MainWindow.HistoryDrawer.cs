using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MultiShell.ViewModels;

namespace MultiShell.Views;

public partial class MainWindow
{
    public void ToggleHistoryDrawer()
    {
        if (HistoryDrawer == null) return;

        if (HistoryDrawer.IsVisible)
        {
            HideHistoryDrawerAndFocusTerminal();
        }
        else
        {
            ShowHistoryDrawer();
        }
    }

    public void ShowHistoryDrawer()
    {
        _historyHoverTimer?.Stop();
        _historyHoverTimer = null;
        if (HistoryDrawer == null) return;
        HistoryDrawer.IsVisible = true;
        var hasFilter = false;
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            hasFilter = HistoryTabControl?.SelectedIndex == 1
                ? !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery)
                : !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery);
        }
        FocusActiveHistoryList(selectLastItem: !hasFilter);
    }

    private void FocusActiveHistoryList(bool selectLastItem = true)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var isDirTab = HistoryTabControl?.SelectedIndex == 1;
            var activeListBox = isDirTab ? DirectoryHistoryListBox : CommandHistoryListBox;
            var activeSearchBox = isDirTab ? DirectoryHistorySearchBox : CommandHistorySearchBox;

            if (activeListBox != null && activeListBox.ItemCount > 0)
            {
                if (selectLastItem)
                {
                    activeListBox.SelectedIndex = activeListBox.ItemCount - 1;
                }
                else
                {
                    activeListBox.SelectedIndex = 0;
                }

                if (activeListBox.SelectedItem != null)
                {
                    activeListBox.ScrollIntoView(activeListBox.SelectedItem);
                }
            }

            activeSearchBox?.Focus();
        }, DispatcherPriority.Input);
    }

    private void NavigateHistorySelection(int delta)
    {
        var activeListBox = HistoryTabControl?.SelectedIndex == 1
            ? DirectoryHistoryListBox
            : CommandHistoryListBox;

        if (activeListBox == null || activeListBox.ItemCount == 0) return;

        int currentIndex = activeListBox.SelectedIndex;
        if (currentIndex < 0)
        {
            currentIndex = delta < 0 ? activeListBox.ItemCount - 1 : 0;
        }
        else
        {
            currentIndex = Math.Clamp(currentIndex + delta, 0, activeListBox.ItemCount - 1);
        }

        activeListBox.SelectedIndex = currentIndex;
        if (activeListBox.SelectedItem != null)
        {
            activeListBox.ScrollIntoView(activeListBox.SelectedItem);
        }
    }

    private void OnHistoryListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsLeftButtonPressed)
        {
            // Left click: paste into prompt without executing
            var clickedItem = GetHistoryItemFromPointerSource(e.Source as Visual);
            if (string.IsNullOrWhiteSpace(clickedItem)) return;

            e.Handled = true;
            if (sender == CommandHistoryListBox)
                PasteSelectedCommand(clickedItem);
            else if (sender == DirectoryHistoryListBox)
                PasteSelectedDirectory(clickedItem);
        }
        else if (props.IsRightButtonPressed)
        {
            // Right click: execute directly
            var clickedItem = GetHistoryItemFromPointerSource(e.Source as Visual);
            if (string.IsNullOrWhiteSpace(clickedItem)) return;

            e.Handled = true;
            if (sender == CommandHistoryListBox)
                ExecuteSelectedCommand(clickedItem);
            else if (sender == DirectoryHistoryListBox)
                ExecuteSelectedDirectory(clickedItem);
        }
    }

    private static string? GetHistoryItemFromPointerSource(Visual? visual)
    {
        while (visual != null)
        {
            if (visual.DataContext is string str && !string.IsNullOrWhiteSpace(str))
            {
                return str;
            }
            visual = visual.GetVisualParent();
        }
        return null;
    }

    private void OnTabSwitcherListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed)
        {
            var clickedTab = GetTabItemFromPointerSource(e.Source as Visual);
            if (clickedTab != null && DataContext is MainViewModel vm)
            {
                if (IsTabSwitcherCloseButton(e.Source as Visual))
                {
                    e.Handled = true;
                    vm.CloseTabFromSwitcher(clickedTab);
                    return;
                }

                e.Handled = true;
                vm.SelectTabFromSwitcher(clickedTab);
                FocusActiveTerminal();
            }
        }
    }

    private static bool IsTabSwitcherCloseButton(Visual? visual)
    {
        while (visual != null && visual is not ListBoxItem)
        {
            if (visual is Button btn && btn.Classes.Contains("tabSwitcherCloseBtn"))
            {
                return true;
            }
            visual = visual.GetVisualParent();
        }
        return false;
    }

    private static TerminalTabViewModel? GetTabItemFromPointerSource(Visual? visual)
    {
        while (visual != null)
        {
            if (visual.DataContext is TerminalTabViewModel tab)
            {
                return tab;
            }
            visual = visual.GetVisualParent();
        }
        return null;
    }

    private void OnHistoryListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (sender == CommandHistoryListBox || HistoryTabControl?.SelectedIndex == 0)
            {
                PasteSelectedCommand();
            }
            else
            {
                PasteSelectedDirectory();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Left && HistoryTabControl != null)
        {
            HistoryTabControl.SelectedIndex = 0;
            var hasFilter = DataContext is MainViewModel vm && vm.SelectedTab != null && !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery);
            FocusActiveHistoryList(selectLastItem: !hasFilter);
            e.Handled = true;
        }
        else if (e.Key == Key.Right && HistoryTabControl != null)
        {
            HistoryTabControl.SelectedIndex = 1;
            var hasFilter = DataContext is MainViewModel vm && vm.SelectedTab != null && !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery);
            FocusActiveHistoryList(selectLastItem: !hasFilter);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HideHistoryDrawerAndFocusTerminal();
            e.Handled = true;
        }
    }

    private void PasteSelectedCommand(string? explicitCommand = null)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            var cmd = explicitCommand ?? CommandHistoryListBox?.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(cmd))
            {
                if (!string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery) && vm.SelectedTab.FilteredCommandHistory.Count > 0)
                {
                    cmd = vm.SelectedTab.FilteredCommandHistory[0];
                }
                else if (vm.SelectedTab.CommandHistory.Count > 0)
                {
                    cmd = vm.SelectedTab.CommandHistory[^1];
                }
            }
            if (!string.IsNullOrWhiteSpace(cmd))
            {
                vm.SelectedTab.PasteHistoryCommand(cmd);
            }
            HideHistoryDrawerAndFocusTerminal();
        }
    }

    private void PasteSelectedDirectory(string? explicitDirectory = null)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            var dir = explicitDirectory ?? DirectoryHistoryListBox?.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(dir))
            {
                if (!string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery) && vm.SelectedTab.FilteredDirectoryHistory.Count > 0)
                {
                    dir = vm.SelectedTab.FilteredDirectoryHistory[0];
                }
                else if (vm.SelectedTab.DirectoryHistory.Count > 0)
                {
                    dir = vm.SelectedTab.DirectoryHistory[^1];
                }
            }
            if (!string.IsNullOrWhiteSpace(dir))
            {
                vm.SelectedTab.PasteHistoryDirectory(dir);
            }
            HideHistoryDrawerAndFocusTerminal();
        }
    }

    private void ExecuteSelectedCommand(string? explicitCommand = null)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            var cmd = explicitCommand ?? CommandHistoryListBox?.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(cmd))
            {
                if (!string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery) && vm.SelectedTab.FilteredCommandHistory.Count > 0)
                {
                    cmd = vm.SelectedTab.FilteredCommandHistory[0];
                }
                else if (vm.SelectedTab.CommandHistory.Count > 0)
                {
                    cmd = vm.SelectedTab.CommandHistory[^1];
                }
            }
            if (!string.IsNullOrWhiteSpace(cmd))
            {
                vm.SelectedTab.ExecuteHistoryCommand(cmd);
            }
            HideHistoryDrawerAndFocusTerminal();
        }
    }

    private void ExecuteSelectedDirectory(string? explicitDirectory = null)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            var dir = explicitDirectory ?? DirectoryHistoryListBox?.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(dir))
            {
                if (!string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery) && vm.SelectedTab.FilteredDirectoryHistory.Count > 0)
                {
                    dir = vm.SelectedTab.FilteredDirectoryHistory[0];
                }
                else if (vm.SelectedTab.DirectoryHistory.Count > 0)
                {
                    dir = vm.SelectedTab.DirectoryHistory[^1];
                }
            }
            if (!string.IsNullOrWhiteSpace(dir))
            {
                vm.SelectedTab.NavigateToHistoryDirectory(dir);
            }
            HideHistoryDrawerAndFocusTerminal();
        }
    }

    private void HideHistoryDrawerAndFocusTerminal()
    {
        _historyHoverTimer?.Stop();
        _historyHoverTimer = null;
        if (HistoryDrawer != null)
        {
            HistoryDrawer.IsVisible = false;
        }
        FocusActiveTerminal();
    }
}
