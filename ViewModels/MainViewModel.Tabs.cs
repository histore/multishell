using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Models;
using MultiShell.Services;

namespace MultiShell.ViewModels;

public partial class MainViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private TerminalTabViewModel? _selectedTab;

    partial void OnSelectedTabChanged(TerminalTabViewModel? oldValue, TerminalTabViewModel? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
        TriggerSaveState();
    }

    /// <summary>
    /// Gets the formatted window title showing the full working directory path, formatting with middle-ellipsis only if excessively long (> 65 chars).
    /// </summary>
    public string WindowTitle
    {
        get
        {
            if (SelectedTab == null) return "MultiShell";

            var rawTitle = !string.IsNullOrWhiteSpace(SelectedTab.WorkingDirectory)
                ? SelectedTab.WorkingDirectory
                : SelectedTab.Title;

            if (string.IsNullOrWhiteSpace(rawTitle)) return "MultiShell";

            var formatted = TerminalTabViewModel.FormatMiddleEllipsis(rawTitle, maxLength: 65);
            return $"MultiShell - {formatted}";
        }
    }

    public ObservableCollection<TerminalTabViewModel> Tabs { get; } = new();

    /// <summary>
    /// Gets the collection of recently closed tabs (max 10 entries FIFO).
    /// </summary>
    public ObservableCollection<ClosedTabItemViewModel> ClosedTabs { get; } = new();

    /// <summary>
    /// Maximum number of closed tabs retained in history.
    /// </summary>
    public const int MaxClosedTabsCount = 10;

    /// <summary>
    /// Gets a value indicating whether there are any open terminal tabs.
    /// </summary>
    public bool HasOpenTabs => Tabs.Count > 0;

    /// <summary>
    /// Gets a value indicating whether all tabs are closed (empty workspace state).
    /// </summary>
    public bool HasNoTabs => Tabs.Count == 0;

    /// <summary>
    /// Gets a value indicating whether there are any recently closed tabs in history.
    /// </summary>
    public bool HasClosedTabs => ClosedTabs.Count > 0;

    private static string GetDefaultTitle(ShellType shellType, int id) => shellType switch
    {
        ShellType.PowerShell => $"PS {id}",
        ShellType.NuShell => $"NU {id}",
        ShellType.WSL => $"WSL {id}",
        ShellType.CMD => $"CMD {id}",
        _ => $"Shell {id}"
    };

    [RelayCommand]
    public void AddNewTab()
    {
        AddNewTabWithDirectory(null, DefaultShellType);
    }

    [RelayCommand]
    public void AddNewTabWithProfile(TerminalProfileItemViewModel? profileVm)
    {
        if (profileVm == null) return;
        DefaultShellType = profileVm.ShellType;
        int nextId = Tabs.Count + 1;
        var title = $"{profileVm.IconTag} {nextId}";
        var workingDir = profileVm.WorkingDirectory;
        var session = _shellProcessService.CreateSession(title, workingDir, profileVm.ShellType, profileVm.ExecutablePath, profileVm.Arguments);
        var tab = new TerminalTabViewModel(session, pathCommandHistoryService: _pathCommandHistoryService);
        tab.Title = title;
        tab.UpdateTheme(_themeService.IsDarkTerminalTheme);
        RegisterTabEvents(tab);
        Tabs.Add(tab);
        SelectedTab = tab;
        TriggerSaveState();
    }

    [RelayCommand]
    public void AddNewTabWithShell(ShellType shellType)
    {
        DefaultShellType = shellType;
        AddNewTabWithDirectory(null, shellType);
    }

    public void AddNewTabWithDirectory(string? workingDirectory, ShellType shellType = ShellType.PowerShell, int? insertIndex = null)
    {
        var newTab = CreateNewTab(workingDirectory, shellType);
        RegisterTabEvents(newTab);
        if (insertIndex.HasValue && insertIndex.Value >= 0 && insertIndex.Value <= Tabs.Count)
        {
            Tabs.Insert(insertIndex.Value, newTab);
        }
        else
        {
            Tabs.Add(newTab);
        }
        SelectedTab = newTab;
        TriggerSaveState();
    }

    private TerminalTabViewModel CreateNewTab(string? workingDirectory = null, ShellType shellType = ShellType.PowerShell)
    {
        int nextId = Tabs.Count + 1;
        var title = GetDefaultTitle(shellType, nextId);

        string? targetDir = workingDirectory;
        string? customExe = null;
        string? customArgs = null;

        var profileVm = Profiles.FirstOrDefault(p => p.ShellType == shellType);
        if (profileVm != null)
        {
            if (string.IsNullOrWhiteSpace(targetDir) && !string.IsNullOrWhiteSpace(profileVm.WorkingDirectory))
            {
                targetDir = profileVm.WorkingDirectory;
            }
            if (!profileVm.IsBuiltIn)
            {
                customExe = profileVm.ExecutablePath;
                customArgs = profileVm.Arguments;
            }
        }
        else
        {
            var profileModel = _terminalProfileService.GetProfiles().FirstOrDefault(p => p.ShellType == shellType);
            if (profileModel != null)
            {
                if (string.IsNullOrWhiteSpace(targetDir) && !string.IsNullOrWhiteSpace(profileModel.WorkingDirectory))
                {
                    targetDir = profileModel.WorkingDirectory;
                }
                if (!profileModel.IsBuiltIn)
                {
                    customExe = profileModel.ExecutablePath;
                    customArgs = profileModel.Arguments;
                }
            }
        }

        var session = _shellProcessService.CreateSession(title, targetDir, shellType, customExe, customArgs);

        var tab = new TerminalTabViewModel(session, pathCommandHistoryService: _pathCommandHistoryService);
        if (workingDirectory == null)
        {
            tab.Title = title;
        }
        tab.UpdateTheme(_themeService.IsDarkTerminalTheme);
        return tab;
    }

    [RelayCommand]
    public void DuplicateTab(TerminalTabViewModel? tab = null)
    {
        var targetTab = tab ?? SelectedTab;
        if (targetTab == null)
        {
            return;
        }

        int targetIndex = Tabs.IndexOf(targetTab);
        int insertIndex = targetIndex >= 0 ? targetIndex + 1 : Tabs.Count;

        AddNewTabWithDirectory(targetTab.WorkingDirectory, targetTab.ShellType, insertIndex);
    }

    [RelayCommand]
    public void CloseTab(TerminalTabViewModel? tab)
    {
        if (tab == null)
        {
            return;
        }

        // Capture closed tab state before disposing
        var closedItem = new ClosedTabItemViewModel(
            tab.Title,
            tab.WorkingDirectory,
            tab.ShellType,
            tab.CommandHistory,
            tab.DirectoryHistory,
            DateTime.Now);

        try
        {
            tab.Dispose();
        }
        catch
        {
            return;
        }

        if (tab.IsRunning)
        {
            return;
        }

        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        tab.CloseRequested -= CloseTab;
        tab.DirectoryChanged -= OnTabDirectoryChanged;
        tab.HistoryChanged -= OnTabHistoryChanged;

        // Push to recently closed history (newest at index 0, capped at MaxClosedTabsCount)
        ClosedTabs.Insert(0, closedItem);
        while (ClosedTabs.Count > MaxClosedTabsCount)
        {
            ClosedTabs.RemoveAt(ClosedTabs.Count - 1);
        }

        if (SelectedTab == tab)
        {
            SelectedTab = Tabs.Count > 0
                ? Tabs[Math.Min(index, Tabs.Count - 1)]
                : null;
        }

        TriggerSaveState();
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
        var tabVm = new TerminalTabViewModel(session, pathCommandHistoryService: _pathCommandHistoryService);
        tabVm.RestoreHistory(closedItem.CommandHistory, closedItem.DirectoryHistory);
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

    [RelayCommand]
    public void SelectTab(TerminalTabViewModel? tab)
    {
        if (tab != null && Tabs.Contains(tab))
        {
            SelectedTab = tab;
        }
    }

    /// <summary>
    /// Selects the next tab in the tab strip, with optional cyclic wrap-around.
    /// </summary>
    [RelayCommand]
    public void SelectNextTab()
    {
        SelectNextTab(wrapAround: false);
    }

    /// <summary>
    /// Selects the next tab with cyclic wrap-around (Ctrl+Tab).
    /// </summary>
    [RelayCommand]
    public void CycleNextTab()
    {
        SelectNextTab(wrapAround: true);
    }

    public void SelectNextTab(bool wrapAround)
    {
        if (Tabs.Count <= 1 || SelectedTab == null) return;
        var currentIndex = Tabs.IndexOf(SelectedTab);
        if (currentIndex >= 0 && currentIndex < Tabs.Count - 1)
        {
            SelectedTab = Tabs[currentIndex + 1];
        }
        else if (wrapAround && currentIndex == Tabs.Count - 1)
        {
            SelectedTab = Tabs[0];
        }
    }

    /// <summary>
    /// Selects the previous tab in the tab strip, with optional cyclic wrap-around.
    /// </summary>
    [RelayCommand]
    public void SelectPreviousTab()
    {
        SelectPreviousTab(wrapAround: false);
    }

    /// <summary>
    /// Selects the previous tab with cyclic wrap-around (Ctrl+Shift+Tab).
    /// </summary>
    [RelayCommand]
    public void CyclePreviousTab()
    {
        SelectPreviousTab(wrapAround: true);
    }

    public void SelectPreviousTab(bool wrapAround)
    {
        if (Tabs.Count <= 1 || SelectedTab == null) return;
        var currentIndex = Tabs.IndexOf(SelectedTab);
        if (currentIndex > 0)
        {
            SelectedTab = Tabs[currentIndex - 1];
        }
        else if (wrapAround && currentIndex == 0)
        {
            SelectedTab = Tabs[Tabs.Count - 1];
        }
    }

    /// <summary>
    /// Selects a tab by its 0-based index, or the last tab if index is -1 (Ctrl+1..8, Ctrl+9).
    /// </summary>
    [RelayCommand]
    public void SelectTabByIndex(int index)
    {
        if (Tabs.Count == 0) return;
        if (index == -1)
        {
            SelectedTab = Tabs[Tabs.Count - 1];
        }
        else if (index >= 0 && index < Tabs.Count)
        {
            SelectedTab = Tabs[index];
        }
    }

    /// <summary>
    /// Moves the currently selected tab left (-1) or right (+1) in the tab list (Ctrl+Shift+PageUp/PageDown).
    /// </summary>
    [RelayCommand]
    public void MoveSelectedTab(int direction)
    {
        if (Tabs.Count <= 1 || SelectedTab == null) return;
        var currentIndex = Tabs.IndexOf(SelectedTab);
        if (currentIndex < 0) return;

        var targetIndex = currentIndex + direction;
        if (targetIndex >= 0 && targetIndex < Tabs.Count)
        {
            Tabs.Move(currentIndex, targetIndex);
            TriggerSaveState();
        }
    }

    /// <summary>
    /// Closes the currently selected tab (Ctrl+Shift+W).
    /// </summary>
    [RelayCommand]
    public void CloseSelectedTab()
    {
        if (SelectedTab != null)
        {
            CloseTab(SelectedTab);
        }
    }

    private void RegisterTabEvents(TerminalTabViewModel tab)
    {
        tab.CloseRequested += CloseTab;
        tab.DirectoryChanged += OnTabDirectoryChanged;
        tab.HistoryChanged += OnTabHistoryChanged;
        tab.UpdateTheme(IsDarkTerminalTheme);
        tab.UpdateFontSize(_fontSizeService.TerminalFontSize);
    }

    private void OnTabDirectoryChanged(TerminalTabViewModel tab, string newDirectory)
    {
        if (tab == SelectedTab)
        {
            OnPropertyChanged(nameof(WindowTitle));
        }
        TriggerSaveState();
    }

    private void OnTabHistoryChanged(TerminalTabViewModel tab)
    {
        TriggerSaveState();
    }

    public void MoveTab(TerminalTabViewModel source, TerminalTabViewModel target)
    {
        if (source == null || target == null || source == target) return;
        var oldIndex = Tabs.IndexOf(source);
        var newIndex = Tabs.IndexOf(target);
        if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
        {
            Tabs.Move(oldIndex, newIndex);
            SelectedTab = source;
            TriggerSaveState();
        }
    }

    public void MoveTab(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Tabs.Count || newIndex < 0 || newIndex >= Tabs.Count || oldIndex == newIndex)
        {
            return;
        }

        Tabs.Move(oldIndex, newIndex);
        TriggerSaveState();
    }
}
