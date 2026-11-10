using CommunityToolkit.Mvvm.Input;

namespace MultiShell.ViewModels;

/// <summary>
/// Partial class managing tab selection, cycling, indexed switching, and reordering.
/// </summary>
public partial class MainViewModel
{
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
    public void SelectNextTab() => SelectNextTab(wrapAround: false);

    /// <summary>
    /// Selects the next tab with cyclic wrap-around (Ctrl+Tab).
    /// </summary>
    [RelayCommand]
    public void CycleNextTab() => SelectNextTab(wrapAround: true);

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
    public void SelectPreviousTab() => SelectPreviousTab(wrapAround: false);

    /// <summary>
    /// Selects the previous tab with cyclic wrap-around (Ctrl+Shift+Tab).
    /// </summary>
    [RelayCommand]
    public void CyclePreviousTab() => SelectPreviousTab(wrapAround: true);

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
