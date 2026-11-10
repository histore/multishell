using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Models;
using MultiShell.Services;

namespace MultiShell.ViewModels;

/// <summary>
/// Partial class handling multi-window coordination, tab undocking/redocking, and <see cref="ICoordinatedWindow"/> implementation.
/// </summary>
public partial class MainViewModel
{
    public int TabCount => Tabs.Count;

    /// <summary>
    /// Detaches a tab from this window's collection without disposing its process/session (Zero PTY Restart).
    /// </summary>
    public void DetachTab(TerminalTabViewModel tab)
    {
        if (tab == null) return;

        if (tab.IsRenaming)
        {
            tab.CommitRenaming();
        }

        var index = Tabs.IndexOf(tab);
        UnregisterTabEvents(tab);
        Tabs.Remove(tab);

        if (SelectedTab == tab)
        {
            SelectedTab = Tabs.Count > 0
                ? Tabs[Math.Min(index, Tabs.Count - 1)]
                : null;
        }

        TriggerSaveState();

        if (!IsPrimary && Tabs.Count == 0)
        {
            _ = _windowCoordinator.CloseWindowAsync(this);
        }
    }

    /// <summary>
    /// Attaches an existing tab (e.g. from an undock, dock-back, or drag-and-drop transfer) to this window.
    /// </summary>
    public void AttachTab(TerminalTabViewModel tab, int? insertIndex = null)
    {
        if (tab == null) return;

        RegisterTabEvents(tab);
        tab.UpdateFontSize(_fontSizeService.TerminalFontSize);
        tab.UpdateTheme(_themeService.IsDarkTerminalTheme);
        if (insertIndex.HasValue && insertIndex.Value >= 0 && insertIndex.Value <= Tabs.Count)
        {
            Tabs.Insert(insertIndex.Value, tab);
        }
        else
        {
            Tabs.Add(tab);
        }
        SelectedTab = tab;

        TriggerSaveState();
    }

    void ICoordinatedWindow.DetachTab(ICoordinatedTab tab)
    {
        if (tab is TerminalTabViewModel tabVm)
        {
            DetachTab(tabVm);
        }
    }

    void ICoordinatedWindow.AttachTab(ICoordinatedTab tab, int? insertIndex)
    {
        if (tab is TerminalTabViewModel tabVm)
        {
            AttachTab(tabVm, insertIndex);
        }
    }

    ICoordinatedWindow ICoordinatedWindow.CreateSatelliteWindow() => CreateSatelliteViewModel();

    public void ArchiveTabsTo(ICoordinatedWindow targetWindow)
    {
        if (targetWindow == null) return;

        foreach (var tab in Tabs.ToList())
        {
            if (tab != null)
            {
                targetWindow.PushClosedTabState(tab.ToTabState());
            }
        }
    }

    public void PushClosedTabState(TabState closedTabState)
    {
        PushClosedTab(ClosedTabItemViewModel.FromTabState(closedTabState));
    }

    public WindowWorkspaceState ExportWorkspaceState()
    {
        var tabStates = Tabs
            .Where(t => t != null)
            .Select(t => t.ToTabState())
            .ToList();

        int selectedIndex = SelectedTab != null ? Math.Max(0, Tabs.IndexOf(SelectedTab)) : 0;
        return new WindowWorkspaceState(WindowNumber, tabStates, selectedIndex);
    }

    /// <summary>
    /// Creates a secondary satellite ViewModel sharing all application services and the coordinator.
    /// </summary>
    public MainViewModel CreateSatelliteViewModel()
    {
        return new MainViewModel(
            _shellProcessService,
            _persistenceService,
            _themeService,
            _localizationService,
            _fontSizeService,
            _shellDiscoveryService,
            _terminalProfileService,
            _pathCommandHistoryService,
            _directoryHistoryService,
            _pathColorCodingService,
            _toastNotificationService,
            windowCoordinator: _windowCoordinator,
            isSatellite: true);
    }

    /// <summary>
    /// Undocks the specified tab (or the currently selected tab) into a new secondary satellite window.
    /// </summary>
    [RelayCommand]
    public async Task UndockTabAsync(TerminalTabViewModel? tab = null)
    {
        var targetTab = tab ?? SelectedTab;
        if (targetTab == null) return;

        await _windowCoordinator.UndockTabAsync(this, targetTab);
    }

    /// <summary>
    /// Docks a tab back from a secondary satellite window into the primary window.
    /// </summary>
    [RelayCommand]
    public async Task DockBackTabAsync(TerminalTabViewModel? tab = null)
    {
        var targetTab = tab ?? SelectedTab;
        if (targetTab == null) return;

        await _windowCoordinator.DockBackTabAsync(this, targetTab);
    }

    /// <summary>
    /// Requests closing this window through the window coordinator.
    /// </summary>
    [RelayCommand]
    public async Task CloseWindowAsync()
    {
        await _windowCoordinator.CloseWindowAsync(this);
    }
}
