using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Models;

namespace MultiShell.ViewModels;

/// <summary>
/// Partial class managing main-window close confirmation when tabs still have active processes (REQ-UI-011).
/// </summary>
public partial class MainViewModel
{
    private bool _isExitConfirmed;

    [ObservableProperty]
    private bool _isCloseConfirmationOpen;

    [ObservableProperty]
    private ActiveProcessTabItem? _selectedActiveProcessTab;

    /// <summary>
    /// Gets the collection of open tabs that currently have an active running process.
    /// </summary>
    public ObservableCollection<ActiveProcessTabItem> ActiveProcessTabs { get; } = new();

    /// <summary>
    /// Gets the localized confirmation prompt reflecting the number of tabs with active processes.
    /// </summary>
    public string CloseConfirmationMessage => ActiveProcessTabs.Count == 1
        ? _localizationService["CloseConfirm_Message_Single"]
        : string.Format(_localizationService["CloseConfirm_Message_Multiple"], ActiveProcessTabs.Count);

    /// <summary>
    /// Raised when the user confirms exiting MultiShell from the close confirmation dialog.
    /// </summary>
    public event Action? ExitConfirmedRequested;

    /// <summary>
    /// Raised when the close confirmation dialog closes without exiting so the active terminal can regain focus.
    /// </summary>
    public event Action? CloseConfirmationDismissed;

    /// <summary>
    /// Raised when switching to an active-process tab hosted in a secondary window so the view can activate that window.
    /// </summary>
    public event Action? WindowFocusRequested;

    /// <summary>
    /// Evaluates whether closing the primary main window should be intercepted to prompt the user
    /// about active processes in open tabs.
    /// </summary>
    /// <returns><c>true</c> if active processes exist and the confirmation dialog was opened; otherwise <c>false</c>.</returns>
    public bool TryRequestCloseConfirmation()
    {
        if (!IsPrimary || _isExitConfirmed)
        {
            return false;
        }

        var activeItems = CollectActiveProcessTabs();
        if (activeItems.Count == 0)
        {
            return false;
        }

        ActiveProcessTabs.Clear();
        foreach (var item in activeItems)
        {
            ActiveProcessTabs.Add(item);
        }

        SelectedActiveProcessTab = ActiveProcessTabs[0];
        OnPropertyChanged(nameof(CloseConfirmationMessage));
        IsCloseConfirmationOpen = true;
        return true;
    }

    private List<ActiveProcessTabItem> CollectActiveProcessTabs()
    {
        var items = new List<ActiveProcessTabItem>();
        var windows = new List<MainViewModel> { this };

        if (_windowCoordinator != null)
        {
            foreach (var coordinated in _windowCoordinator.Windows)
            {
                if (coordinated is MainViewModel vm && !ReferenceEquals(vm, this))
                {
                    windows.Add(vm);
                }
            }
        }

        foreach (var win in windows)
        {
            var badge = win.IsPrimary
                ? string.Empty
                : string.Format(_localizationService["Window_Secondary_Title_Prefix"], win.WindowNumber).Trim();

            foreach (var tab in win.Tabs)
            {
                if (tab == null || !tab.HasActiveProcess) continue;
                var cmd = ResolveActiveCommandSummary(tab);
                items.Add(new ActiveProcessTabItem(win, tab, cmd, badge));
            }
        }

        return items;
    }

    private static string ResolveActiveCommandSummary(TerminalTabViewModel tab)
    {
        var runningPane = tab.FindActiveProcessPane();
        if (!string.IsNullOrWhiteSpace(runningPane?.PaneLastExecutedCommand))
        {
            return runningPane.PaneLastExecutedCommand;
        }
        return tab.LastExecutedCommand ?? string.Empty;
    }

    /// <summary>
    /// Confirms exiting MultiShell despite active running processes.
    /// </summary>
    [RelayCommand]
    public void ConfirmExitFromCloseDialog()
    {
        _isExitConfirmed = true;
        CloseAndClearConfirmationState();
        ExitConfirmedRequested?.Invoke();
    }

    /// <summary>
    /// Closes the confirmation dialog and activates the selected tab with an active process.
    /// </summary>
    [RelayCommand]
    public void SwitchToActiveProcessTab(ActiveProcessTabItem? item = null)
    {
        var target = item ?? SelectedActiveProcessTab ?? ActiveProcessTabs.FirstOrDefault();
        CloseAndClearConfirmationState();

        if (target != null)
        {
            target.OwnerWindow.SelectedTab = target.Tab;
            var runningPane = target.Tab.FindActiveProcessPane();
            if (runningPane != null && !ReferenceEquals(target.Tab.ActivePane, runningPane))
            {
                target.Tab.SetActivePane(runningPane);
            }

            if (!ReferenceEquals(target.OwnerWindow, this))
            {
                target.OwnerWindow.WindowFocusRequested?.Invoke();
            }
        }

        CloseConfirmationDismissed?.Invoke();
    }

    /// <summary>
    /// Cancels closing MultiShell and returns focus to the active terminal.
    /// </summary>
    [RelayCommand]
    public void CancelCloseConfirmation()
    {
        CloseAndClearConfirmationState();
        CloseConfirmationDismissed?.Invoke();
    }

    private void CloseAndClearConfirmationState()
    {
        IsCloseConfirmationOpen = false;
        ActiveProcessTabs.Clear();
        SelectedActiveProcessTab = null;
    }
}
