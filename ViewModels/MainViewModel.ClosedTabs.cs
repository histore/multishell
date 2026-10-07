using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;

namespace MultiShell.ViewModels;

public partial class MainViewModel
{
    /// <summary>
    /// Gets the collection of recently closed tabs (max 10 entries FIFO).
    /// </summary>
    public ObservableCollection<ClosedTabItemViewModel> ClosedTabs { get; } = new();

    /// <summary>
    /// Maximum number of closed tabs retained in history.
    /// </summary>
    public const int MaxClosedTabsCount = 10;

    /// <summary>
    /// Gets a value indicating whether there are any recently closed tabs in history.
    /// </summary>
    public bool HasClosedTabs => ClosedTabs.Count > 0;

    /// <summary>
    /// Pushes a closed tab item to the history, enforcing FIFO eviction past MaxClosedTabsCount.
    /// </summary>
    internal void PushClosedTab(ClosedTabItemViewModel closedItem)
    {
        ClosedTabs.Insert(0, closedItem);
        while (ClosedTabs.Count > MaxClosedTabsCount)
        {
            ClosedTabs.RemoveAt(ClosedTabs.Count - 1);
        }
    }

    /// <summary>
    /// Restores a previously closed tab with its full command and directory histories.
    /// </summary>
    [RelayCommand]
    public void RestoreClosedTab(ClosedTabItemViewModel? closedItem)
    {
        if (closedItem == null) return;

        ClosedTabs.Remove(closedItem);

        _tabCounter++;
        var session = _shellProcessService.CreateSession(closedItem.Title, closedItem.WorkingDirectory, closedItem.ShellType);
        var tabVm = new TerminalTabViewModel(session, pathCommandHistoryService: _pathCommandHistoryService, directoryHistoryService: _directoryHistoryService, localizationService: _localizationService);
        tabVm.RestoreHistory(closedItem.CommandHistory, closedItem.DirectoryHistory);
        tabVm.CustomTitle = closedItem.CustomTitle;
        tabVm.TabColor = closedItem.TabColor;
        RegisterTabEvents(tabVm);
        Tabs.Add(tabVm);
        SelectedTab = tabVm;

        TriggerSaveState();
    }

    /// <summary>
    /// Removes a closed tab item from history permanently.
    /// </summary>
    [RelayCommand]
    public void RemoveClosedTab(ClosedTabItemViewModel? closedItem)
    {
        if (closedItem == null) return;

        ClosedTabs.Remove(closedItem);
        TriggerSaveState();
    }

    /// <summary>
    /// Clears all closed tab items from history permanently.
    /// </summary>
    [RelayCommand]
    public void ClearClosedTabs()
    {
        ClosedTabs.Clear();
        TriggerSaveState();
    }

    /// <summary>
    /// Closes the currently selected tab (Ctrl+Shift+W / Ctrl+Shift+F4).
    /// </summary>
    [RelayCommand]
    public void CloseSelectedTab()
    {
        if (SelectedTab != null)
        {
            CloseTab(SelectedTab);
        }
    }

    [RelayCommand]
    public void CloseOtherTabs(TerminalTabViewModel? tab)
    {
        var target = tab ?? SelectedTab;
        if (target == null) return;

        var toClose = Tabs.Where(t => t != target).ToList();
        foreach (var t in toClose)
        {
            CloseTab(t);
        }
    }

    [RelayCommand]
    public void CloseTabsToRight(TerminalTabViewModel? tab)
    {
        var target = tab ?? SelectedTab;
        if (target == null) return;

        var targetIndex = Tabs.IndexOf(target);
        if (targetIndex < 0) return;

        var toClose = Tabs.Skip(targetIndex + 1).ToList();
        foreach (var t in toClose)
        {
            CloseTab(t);
        }
    }
}
