using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;
using MultiShell.ViewModels;

namespace MultiShell.Views.Dialogs;

public partial class HistoryDrawerView
{
    private void OnHistoryListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;

        if (IsDeleteButton(e.Source as Visual))
        {
            e.Handled = true;
            if (props.IsLeftButtonPressed)
            {
                DeleteHistoryItemAtSource(sender, e.Source as Visual);
            }
            return;
        }

        if (props.IsLeftButtonPressed)
        {
            e.Handled = true;
            if (sender == CommandHistoryListBox)
            {
                var clickedItem = GetHistoryItemFromPointerSource(e.Source as Visual);
                if (!string.IsNullOrWhiteSpace(clickedItem)) PasteSelectedCommand(clickedItem);
            }
            else if (sender == DirectoryHistoryListBox)
            {
                var clickedItem = GetHistoryItemFromPointerSource(e.Source as Visual);
                if (!string.IsNullOrWhiteSpace(clickedItem)) PasteSelectedDirectory(clickedItem);
            }
            else if (sender == GlobalHistoryListBox)
            {
                var clickedItem = GetGlobalHistoryItemFromPointerSource(e.Source as Visual);
                if (clickedItem != null) PasteSelectedGlobalItem(clickedItem);
            }
        }
        else if (props.IsRightButtonPressed)
        {
            e.Handled = true;
            if (sender == CommandHistoryListBox)
            {
                var clickedItem = GetHistoryItemFromPointerSource(e.Source as Visual);
                if (!string.IsNullOrWhiteSpace(clickedItem)) ExecuteSelectedCommand(clickedItem);
            }
            else if (sender == DirectoryHistoryListBox)
            {
                var clickedItem = GetHistoryItemFromPointerSource(e.Source as Visual);
                if (!string.IsNullOrWhiteSpace(clickedItem)) ExecuteSelectedDirectory(clickedItem);
            }
            else if (sender == GlobalHistoryListBox)
            {
                var clickedItem = GetGlobalHistoryItemFromPointerSource(e.Source as Visual);
                if (clickedItem != null) ExecuteSelectedGlobalItem(clickedItem);
            }
        }
    }

    private static Avalonia.Controls.ListBoxItem? GetParentListBoxItem(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Avalonia.Controls.ListBoxItem lbi)
            {
                return lbi;
            }
            visual = visual.GetVisualParent();
        }
        return null;
    }

    private static Avalonia.Controls.ListBox? GetParentListBox(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Avalonia.Controls.ListBox lb)
            {
                return lb;
            }
            visual = visual.GetVisualParent();
        }
        return null;
    }

    private static GlobalHistoryItem? GetGlobalHistoryItemFromPointerSource(Visual? visual)
    {
        var lbi = GetParentListBoxItem(visual);
        if (lbi?.DataContext is GlobalHistoryItem lbiItem)
        {
            return lbiItem;
        }

        while (visual != null)
        {
            if (visual.DataContext is GlobalHistoryItem item)
            {
                return item;
            }
            visual = visual.GetVisualParent();
        }
        return null;
    }

    private static string? GetHistoryItemFromPointerSource(Visual? visual)
    {
        var lbi = GetParentListBoxItem(visual);
        if (lbi?.DataContext is string lbiStr && !string.IsNullOrWhiteSpace(lbiStr) && lbiStr != "✕")
        {
            return lbiStr;
        }

        while (visual != null)
        {
            if (visual is not Avalonia.Controls.Button && visual.DataContext is string str && !string.IsNullOrWhiteSpace(str) && str != "✕")
            {
                return str;
            }
            visual = visual.GetVisualParent();
        }
        return null;
    }

    private static bool IsDeleteButton(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Avalonia.Controls.Button btn && btn.Classes.Contains("deleteHistoryItemBtn"))
            {
                return true;
            }
            visual = visual.GetVisualParent();
        }
        return false;
    }

    private static int GetItemIndex(Avalonia.Controls.ListBox? listBox, object? item)
    {
        if (listBox?.ItemsSource is System.Collections.IList list && item != null)
        {
            var idx = list.IndexOf(item);
            if (idx >= 0) return idx;
        }
        return listBox?.SelectedIndex ?? 0;
    }

    private static void AdjustSelectionAfterDelete(Avalonia.Controls.ListBox? listBox, int previousIndex)
    {
        if (listBox == null) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (listBox.ItemCount > 0)
            {
                var newIndex = Math.Clamp(previousIndex, 0, listBox.ItemCount - 1);
                listBox.SelectedIndex = newIndex;
                if (listBox.SelectedItem != null)
                {
                    listBox.ScrollIntoView(listBox.SelectedItem);
                }
            }
        }, Avalonia.Threading.DispatcherPriority.Input);
    }

    private void DeleteHistoryItemAtSource(object? sender, Visual? visual)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedTab is null) return;

        var parentListBox = GetParentListBox(visual) ?? sender as Avalonia.Controls.ListBox;

        if (parentListBox == DirectoryHistoryListBox || (parentListBox == null && HistoryTabControl?.SelectedIndex == 1))
        {
            var dir = GetHistoryItemFromPointerSource(visual);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                var previousIndex = GetItemIndex(DirectoryHistoryListBox, dir);
                vm.SelectedTab.DeleteDirectory(dir);
                AdjustSelectionAfterDelete(DirectoryHistoryListBox, previousIndex);
            }
        }
        else if (parentListBox == GlobalHistoryListBox || (parentListBox == null && HistoryTabControl?.SelectedIndex == 2))
        {
            var item = GetGlobalHistoryItemFromPointerSource(visual);
            if (item != null)
            {
                var previousIndex = GetItemIndex(GlobalHistoryListBox, item);
                vm.SelectedTab.DeleteGlobalItem(item);
                AdjustSelectionAfterDelete(GlobalHistoryListBox, previousIndex);
            }
        }
        else
        {
            var cmd = GetHistoryItemFromPointerSource(visual);
            if (!string.IsNullOrWhiteSpace(cmd))
            {
                var previousIndex = GetItemIndex(CommandHistoryListBox, cmd);
                vm.SelectedTab.DeleteCommand(cmd);
                AdjustSelectionAfterDelete(CommandHistoryListBox, previousIndex);
            }
        }
    }

    internal void DeleteSelectedHistoryItem()
    {
        if (DataContext is not MainViewModel vm || vm.SelectedTab is null) return;

        var tabIndex = HistoryTabControl?.SelectedIndex ?? 0;
        if (tabIndex == 2)
        {
            var item = GlobalHistoryListBox?.SelectedItem as GlobalHistoryItem;
            if (item != null)
            {
                var previousIndex = GlobalHistoryListBox?.SelectedIndex ?? 0;
                vm.SelectedTab.DeleteGlobalItem(item);
                AdjustSelectionAfterDelete(GlobalHistoryListBox, previousIndex);
            }
        }
        else if (tabIndex == 1)
        {
            var dir = DirectoryHistoryListBox?.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(dir))
            {
                var previousIndex = DirectoryHistoryListBox?.SelectedIndex ?? 0;
                vm.SelectedTab.DeleteDirectory(dir);
                AdjustSelectionAfterDelete(DirectoryHistoryListBox, previousIndex);
            }
        }
        else
        {
            var cmd = CommandHistoryListBox?.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(cmd))
            {
                var previousIndex = CommandHistoryListBox?.SelectedIndex ?? 0;
                vm.SelectedTab.DeleteCommand(cmd);
                AdjustSelectionAfterDelete(CommandHistoryListBox, previousIndex);
            }
        }
    }

    private void PasteSelectedCommand(string? explicitCommand = null)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedTab is null) return;

        var cmd = explicitCommand ?? CommandHistoryListBox?.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(cmd))
        {
            if (!string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery) && vm.SelectedTab.FilteredCommandHistory.Count > 0)
            {
                cmd = vm.SelectedTab.FilteredCommandHistory[0];
            }
            else if (vm.SelectedTab.CommandHistory.Count > 0)
            {
                cmd = vm.SelectedTab.CommandHistory[0];
            }
        }
        if (!string.IsNullOrWhiteSpace(cmd))
        {
            vm.SelectedTab.PasteHistoryCommand(cmd);
        }
        HideHistoryDrawer();
    }

    private void PasteSelectedDirectory(string? explicitDirectory = null)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedTab is null) return;

        var dir = explicitDirectory ?? DirectoryHistoryListBox?.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(dir))
        {
            if (!string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery) && vm.SelectedTab.FilteredDirectoryHistory.Count > 0)
            {
                dir = vm.SelectedTab.FilteredDirectoryHistory[0];
            }
            else if (vm.SelectedTab.DirectoryHistory.Count > 0)
            {
                dir = vm.SelectedTab.DirectoryHistory[0];
            }
        }
        if (!string.IsNullOrWhiteSpace(dir))
        {
            vm.SelectedTab.PasteHistoryDirectory(dir);
        }
        HideHistoryDrawer();
    }

    private void ExecuteSelectedCommand(string? explicitCommand = null)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedTab is null) return;

        var cmd = explicitCommand ?? CommandHistoryListBox?.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(cmd))
        {
            if (!string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery) && vm.SelectedTab.FilteredCommandHistory.Count > 0)
            {
                cmd = vm.SelectedTab.FilteredCommandHistory[0];
            }
            else if (vm.SelectedTab.CommandHistory.Count > 0)
            {
                cmd = vm.SelectedTab.CommandHistory[0];
            }
        }
        if (!string.IsNullOrWhiteSpace(cmd))
        {
            vm.SelectedTab.ExecuteHistoryCommand(cmd);
        }
        HideHistoryDrawer();
    }

    private void ExecuteSelectedDirectory(string? explicitDirectory = null)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedTab is null) return;

        var dir = explicitDirectory ?? DirectoryHistoryListBox?.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(dir))
        {
            if (!string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery) && vm.SelectedTab.FilteredDirectoryHistory.Count > 0)
            {
                dir = vm.SelectedTab.FilteredDirectoryHistory[0];
            }
            else if (vm.SelectedTab.DirectoryHistory.Count > 0)
            {
                dir = vm.SelectedTab.DirectoryHistory[0];
            }
        }
        if (!string.IsNullOrWhiteSpace(dir))
        {
            vm.SelectedTab.NavigateToHistoryDirectory(dir);
        }
        HideHistoryDrawer();
    }

    private void ExecuteSelectedGlobalItem(GlobalHistoryItem? explicitItem = null)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedTab is null) return;

        var item = explicitItem ?? GlobalHistoryListBox?.SelectedItem as GlobalHistoryItem;
        if (item == null)
        {
            if (!string.IsNullOrWhiteSpace(vm.SelectedTab.GlobalFilterQuery) && vm.SelectedTab.FilteredGlobalHistory.Count > 0)
            {
                item = vm.SelectedTab.FilteredGlobalHistory[0];
            }
            else if (vm.SelectedTab.GlobalHistory.Count > 0)
            {
                item = vm.SelectedTab.GlobalHistory[0];
            }
        }
        if (item != null)
        {
            vm.SelectedTab.ExecuteGlobalItem(item);
        }
        HideHistoryDrawer();
    }

    private void PasteSelectedGlobalItem(GlobalHistoryItem? explicitItem = null)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedTab is null) return;

        var item = explicitItem ?? GlobalHistoryListBox?.SelectedItem as GlobalHistoryItem;
        if (item == null)
        {
            if (!string.IsNullOrWhiteSpace(vm.SelectedTab.GlobalFilterQuery) && vm.SelectedTab.FilteredGlobalHistory.Count > 0)
            {
                item = vm.SelectedTab.FilteredGlobalHistory[0];
            }
            else if (vm.SelectedTab.GlobalHistory.Count > 0)
            {
                item = vm.SelectedTab.GlobalHistory[0];
            }
        }
        if (item != null)
        {
            vm.SelectedTab.PasteGlobalItem(item);
        }
        HideHistoryDrawer();
    }
}
