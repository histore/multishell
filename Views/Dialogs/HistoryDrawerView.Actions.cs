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

    private static GlobalHistoryItem? GetGlobalHistoryItemFromPointerSource(Visual? visual)
    {
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
