using System;
using Avalonia.Controls;
using Avalonia.Input;
using MultiShell.ViewModels;

namespace MultiShell.Views.Dialogs;

public partial class HistoryDrawerView
{
    public bool HandleKeyDown(KeyEventArgs e)
    {
        if (!IsDrawerOpen) return false;

        if (e.Key == Key.Escape)
        {
            return HandleEscapeKey(e);
        }

        if (e.Key == Key.Enter)
        {
            return HandleEnterKey(e);
        }

        if (e.Key == Key.Up)
        {
            NavigateHistorySelection(-1);
            ClearSearchBoxSelection();
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Down)
        {
            NavigateHistorySelection(1);
            ClearSearchBoxSelection();
            e.Handled = true;
            return true;
        }

        if (e.Key is Key.Left or Key.Right or Key.Tab)
        {
            return HandleTabCycleKey(e);
        }

        return false;
    }

    private bool HandleEscapeKey(KeyEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            if (HistoryTabControl?.SelectedIndex == 2 && !string.IsNullOrEmpty(vm.SelectedTab.GlobalFilterQuery))
            {
                vm.SelectedTab.GlobalFilterQuery = string.Empty;
                e.Handled = true;
                return true;
            }
            if (HistoryTabControl?.SelectedIndex == 1 && !string.IsNullOrEmpty(vm.SelectedTab.DirectoryFilterQuery))
            {
                vm.SelectedTab.DirectoryFilterQuery = string.Empty;
                e.Handled = true;
                return true;
            }
            if (HistoryTabControl?.SelectedIndex == 0 && !string.IsNullOrEmpty(vm.SelectedTab.CommandFilterQuery))
            {
                vm.SelectedTab.CommandFilterQuery = string.Empty;
                e.Handled = true;
                return true;
            }
        }
        HideHistoryDrawer();
        e.Handled = true;
        return true;
    }

    private bool HandleEnterKey(KeyEventArgs e)
    {
        var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        if (HistoryTabControl?.SelectedIndex == 2)
        {
            if (isShift) PasteSelectedGlobalItem();
            else ExecuteSelectedGlobalItem();
        }
        else if (HistoryTabControl?.SelectedIndex == 1)
        {
            if (isShift) PasteSelectedDirectory();
            else ExecuteSelectedDirectory();
        }
        else
        {
            if (isShift) PasteSelectedCommand();
            else ExecuteSelectedCommand();
        }
        e.Handled = true;
        return true;
    }

    private bool HandleTabCycleKey(KeyEventArgs e)
    {
        if (e.Source is TextBox && e.Key != Key.Tab) return false;
        if (HistoryTabControl == null) return false;

        int delta = e.Key switch
        {
            Key.Left => -1,
            Key.Right => 1,
            Key.Tab => (e.KeyModifiers & KeyModifiers.Shift) != 0 ? -1 : 1,
            _ => 0
        };

        if (delta == 0) return false;

        var next = (HistoryTabControl.SelectedIndex + delta + 3) % 3;
        HistoryTabControl.SelectedIndex = next;

        var hasFilter = false;
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            hasFilter = next switch
            {
                1 => !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery),
                2 => !string.IsNullOrWhiteSpace(vm.SelectedTab.GlobalFilterQuery),
                _ => !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery)
            };
        }
        FocusActiveHistoryList(selectLastItem: !hasFilter);
        e.Handled = true;
        return true;
    }

    private void OnHistoryListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
            if (sender == GlobalHistoryListBox || HistoryTabControl?.SelectedIndex == 2)
            {
                if (isShift) PasteSelectedGlobalItem();
                else ExecuteSelectedGlobalItem();
            }
            else if (sender == CommandHistoryListBox || HistoryTabControl?.SelectedIndex == 0)
            {
                if (isShift) PasteSelectedCommand();
                else ExecuteSelectedCommand();
            }
            else
            {
                if (isShift) PasteSelectedDirectory();
                else ExecuteSelectedDirectory();
            }
            e.Handled = true;
        }
        else if (e.Key is Key.Left or Key.Right && HistoryTabControl != null)
        {
            int delta = e.Key == Key.Left ? -1 : 1;
            var next = (HistoryTabControl.SelectedIndex + delta + 3) % 3;
            HistoryTabControl.SelectedIndex = next;
            var hasFilter = false;
            if (DataContext is MainViewModel vm && vm.SelectedTab != null)
            {
                hasFilter = next switch
                {
                    1 => !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery),
                    2 => !string.IsNullOrWhiteSpace(vm.SelectedTab.GlobalFilterQuery),
                    _ => !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery)
                };
            }
            FocusActiveHistoryList(selectLastItem: !hasFilter);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HideHistoryDrawer();
            e.Handled = true;
        }
    }
}
