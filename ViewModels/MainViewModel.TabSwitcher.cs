using System;
using CommunityToolkit.Mvvm.Input;

namespace MultiShell.ViewModels;

public partial class MainViewModel
{
    private bool _isTabSwitcherOpen;
    public bool IsTabSwitcherOpen
    {
        get => _isTabSwitcherOpen;
        set => SetProperty(ref _isTabSwitcherOpen, value);
    }

    private int _tabSwitcherSelectedIndex;
    public int TabSwitcherSelectedIndex
    {
        get => _tabSwitcherSelectedIndex;
        set
        {
            if (SetProperty(ref _tabSwitcherSelectedIndex, value))
            {
                OnPropertyChanged(nameof(TabSwitcherCountText));
            }
        }
    }

    private bool _tabSwitcherIsKeyboardTriggered;
    public bool TabSwitcherIsKeyboardTriggered
    {
        get => _tabSwitcherIsKeyboardTriggered;
        set
        {
            if (SetProperty(ref _tabSwitcherIsKeyboardTriggered, value))
            {
                OnPropertyChanged(nameof(TabSwitcherHintText));
            }
        }
    }

    private TerminalTabViewModel? _tabSwitcherOriginalTab;

    public string TabSwitcherCountText => string.Format(_localizationService["Tab_Switcher_Count"], Tabs.Count > 0 ? TabSwitcherSelectedIndex + 1 : 0, Tabs.Count);
    public string TabSwitcherHintText => TabSwitcherIsKeyboardTriggered ? _localizationService["Tab_Switcher_Hint_Keyboard"] : _localizationService["Tab_Switcher_Hint_Click"];

    public void ShowTabSwitcher(bool forward = true, bool isKeyboardTriggered = true)
    {
        if (Tabs.Count == 0) return;

        _tabSwitcherOriginalTab = SelectedTab;
        TabSwitcherIsKeyboardTriggered = isKeyboardTriggered;

        var currentIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0;
        if (currentIndex < 0) currentIndex = 0;

        if (isKeyboardTriggered && Tabs.Count > 1)
        {
            TabSwitcherSelectedIndex = forward
                ? (currentIndex + 1) % Tabs.Count
                : (currentIndex - 1 + Tabs.Count) % Tabs.Count;
        }
        else
        {
            TabSwitcherSelectedIndex = currentIndex;
        }

        IsTabSwitcherOpen = true;
        OnPropertyChanged(nameof(TabSwitcherCountText));
        OnPropertyChanged(nameof(TabSwitcherHintText));
    }

    /// <summary>
    /// Moves the highlighted item in the Tab Switcher forward or backward with cyclic wrap-around.
    /// </summary>
    public void AdvanceTabSwitcher(bool forward = true)
    {
        if (!IsTabSwitcherOpen || Tabs.Count == 0) return;

        if (forward)
        {
            TabSwitcherSelectedIndex = (TabSwitcherSelectedIndex + 1) % Tabs.Count;
        }
        else
        {
            TabSwitcherSelectedIndex = (TabSwitcherSelectedIndex - 1 + Tabs.Count) % Tabs.Count;
        }
    }

    /// <summary>
    /// Commits the currently highlighted tab in the Tab Switcher and closes the overlay.
    /// </summary>
    [RelayCommand]
    public void CommitTabSwitcher()
    {
        if (!IsTabSwitcherOpen) return;

        if (TabSwitcherSelectedIndex >= 0 && TabSwitcherSelectedIndex < Tabs.Count)
        {
            SelectedTab = Tabs[TabSwitcherSelectedIndex];
        }

        IsTabSwitcherOpen = false;
        _tabSwitcherOriginalTab = null;
    }

    /// <summary>
    /// Cancels the Tab Switcher without changing the selected tab.
    /// </summary>
    [RelayCommand]
    public void CancelTabSwitcher()
    {
        if (!IsTabSwitcherOpen) return;

        if (_tabSwitcherOriginalTab != null && Tabs.Contains(_tabSwitcherOriginalTab))
        {
            SelectedTab = _tabSwitcherOriginalTab;
        }

        IsTabSwitcherOpen = false;
        _tabSwitcherOriginalTab = null;
    }

    /// <summary>
    /// Toggles the Tab Switcher overlay (e.g. from the tab strip menu button).
    /// </summary>
    [RelayCommand]
    public void ToggleTabSwitcher()
    {
        if (IsTabSwitcherOpen)
        {
            CancelTabSwitcher();
        }
        else
        {
            ShowTabSwitcher(forward: false, isKeyboardTriggered: false);
        }
    }

    /// <summary>
    /// Directly selects a clicked tab from the Tab Switcher overlay and closes it.
    /// </summary>
    [RelayCommand]
    public void SelectTabFromSwitcher(TerminalTabViewModel? tab)
    {
        if (tab != null && Tabs.Contains(tab))
        {
            SelectedTab = tab;
        }
        IsTabSwitcherOpen = false;
        _tabSwitcherOriginalTab = null;
    }

    /// <summary>
    /// Closes a tab directly from the Tab Switcher overlay, keeping the overlay open if remaining tabs exist (REQ-TAB-023).
    /// </summary>
    [RelayCommand]
    public void CloseTabFromSwitcher(TerminalTabViewModel? tab)
    {
        if (tab == null || !Tabs.Contains(tab)) return;

        CloseTab(tab);

        if (Tabs.Count == 0)
        {
            IsTabSwitcherOpen = false;
            _tabSwitcherOriginalTab = null;
        }
        else
        {
            if (TabSwitcherSelectedIndex >= Tabs.Count)
            {
                TabSwitcherSelectedIndex = Tabs.Count - 1;
            }
            OnPropertyChanged(nameof(TabSwitcherCountText));
        }
    }
}
