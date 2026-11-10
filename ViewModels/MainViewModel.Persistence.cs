using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using MultiShell.Models;

namespace MultiShell.ViewModels;

/// <summary>
/// Partial class managing workspace initialization, multi-window state restoration, and persistence.
/// </summary>
public partial class MainViewModel
{
    private Task? _initTask;

    /// <summary>
    /// Loads persisted workspace state once and restores tabs, settings, history, and secondary windows.
    /// </summary>
    public Task InitializeWorkspaceAsync()
    {
        if (_initTask != null) return _initTask;
        _initTask = DoInitializeWorkspaceAsync();
        return _initTask;
    }

    private async Task DoInitializeWorkspaceAsync()
    {
        var state = await _persistenceService.LoadStateAsync();
        ApplyRestoredSettingsAndHistories(state);

        await RunOnUiThreadAsync(() => ApplyLoadedTabs(state));

        if (state?.SecondaryWindows is { Count: > 0 } && _windowCoordinator != null)
        {
            await RunOnUiThreadAsync(() => RestoreSecondaryWindows(state.SecondaryWindows));
        }
    }

    private void ApplyRestoredSettingsAndHistories(WorkspaceState? state)
    {
        if (!string.IsNullOrWhiteSpace(state?.SavedLanguage))
        {
            _localizationService.SetLanguage(state.SavedLanguage, isUserSelection: true);
        }

        if (state == null) return;

        if (state.IsDarkAppTheme.HasValue)
        {
            SetAppTheme(state.IsDarkAppTheme.Value);
        }
        if (state.IsDarkTerminalTheme.HasValue)
        {
            SetTerminalTheme(state.IsDarkTerminalTheme.Value);
        }

        _fontSizeService.SetAppFontSizeLevel(state.AppFontSizeLevel);
        _fontSizeService.SetTerminalFontSizeLevel(state.TerminalFontSizeLevel);
        DefaultShellType = state.DefaultShellType;

        ImportRestoredHistories(state);
    }

    private void ImportRestoredHistories(WorkspaceState state)
    {
        if (state.PathCommandHistory is { Count: > 0 })
        {
            _pathCommandHistoryService.ImportAll(state.PathCommandHistory);
        }
        else
        {
            // Backwards compatibility migration from legacy tab CommandHistory
            foreach (var tabState in state.Tabs)
            {
                if (tabState.CommandHistory == null || string.IsNullOrWhiteSpace(tabState.WorkingDirectory)) continue;
                foreach (var cmd in tabState.CommandHistory)
                {
                    _pathCommandHistoryService.RecordCommand(tabState.WorkingDirectory, cmd);
                }
            }
        }

        if (state.SharedDirectoryHistory is { Count: > 0 })
        {
            _directoryHistoryService.ImportAll(state.SharedDirectoryHistory);
        }
        else
        {
            // Backwards compatibility migration from legacy tab DirectoryHistory
            foreach (var tabState in state.Tabs)
            {
                if (tabState.DirectoryHistory != null)
                {
                    _directoryHistoryService.ImportAll(tabState.DirectoryHistory);
                }
            }
        }
    }

    private void ApplyLoadedTabs(WorkspaceState? state)
    {
        _isApplyingLoadedTabs = true;
        try
        {
            foreach (var tab in Tabs.ToList())
            {
                if (tab == null) continue;
                UnregisterTabEvents(tab);
                tab.Dispose();
            }
            Tabs.Clear();
            _tabCounter = 0;

            if (state != null && state.Tabs.Count > 0)
            {
                foreach (var tabState in state.Tabs.Take(MaxRestoreTabsLimit))
                {
                    _tabCounter++;
                    var tabVm = CreateRestoredTabViewModel(tabState, _tabCounter);
                    RegisterTabEvents(tabVm);
                    Tabs.Add(tabVm);
                }

                int selectIndex = Math.Clamp(state.SelectedIndex, 0, Tabs.Count - 1);
                SelectedTab = Tabs[selectIndex];

                if (!string.IsNullOrWhiteSpace(_initialDirectory))
                {
                    AddNewTabWithDirectory(_initialDirectory, DefaultShellType);
                }
            }
            else if (!string.IsNullOrWhiteSpace(_initialDirectory))
            {
                AddNewTabWithDirectory(_initialDirectory, DefaultShellType);
            }
            else
            {
                AddNewTab();
            }

            if (state?.ClosedTabs is { Count: > 0 })
            {
                ClosedTabs.Clear();
                foreach (var closedState in state.ClosedTabs.Take(MaxClosedTabsCount))
                {
                    ClosedTabs.Add(ClosedTabItemViewModel.FromTabState(closedState));
                }
            }

            _isInitialized = true;
        }
        finally
        {
            _isApplyingLoadedTabs = false;
        }
    }

    private void RestoreSecondaryWindows(IReadOnlyList<WindowWorkspaceState> secondaryWindows)
    {
        foreach (var secState in secondaryWindows)
        {
            if (secState.Tabs == null || secState.Tabs.Count == 0) continue;

            var satelliteVm = CreateSatelliteViewModel();
            satelliteVm.WindowNumber = secState.WindowNumber;
            _windowCoordinator.RegisterWindow(satelliteVm, isPrimary: false);

            foreach (var tabState in secState.Tabs.Take(MaxRestoreTabsLimit))
            {
                var tabVm = CreateRestoredTabViewModel(tabState, satelliteVm.Tabs.Count + 1);
                satelliteVm.AttachTab(tabVm);
            }

            if (satelliteVm.Tabs.Count > 0)
            {
                int secSelect = Math.Clamp(secState.SelectedIndex, 0, satelliteVm.Tabs.Count - 1);
                satelliteVm.SelectedTab = satelliteVm.Tabs[secSelect];
            }

            _windowCoordinator.TriggerWindowOpenRequested(satelliteVm);
        }
    }

    private TerminalTabViewModel CreateRestoredTabViewModel(TabState tabState, int fallbackIndex)
    {
        var title = string.IsNullOrWhiteSpace(tabState.Title)
            ? GetDefaultTitle(tabState.ShellType, fallbackIndex)
            : tabState.Title;

        var session = _shellProcessService.CreateSession(title, tabState.WorkingDirectory, tabState.ShellType);
        var tabVm = new TerminalTabViewModel(
            session,
            pathCommandHistoryService: _pathCommandHistoryService,
            directoryHistoryService: _directoryHistoryService,
            localizationService: _localizationService);

        tabVm.RestoreHistory(tabState.CommandHistory, tabState.DirectoryHistory);
        tabVm.CustomTitle = tabState.CustomTitle;
        tabVm.TabColor = tabState.TabColor;
        tabVm.UpdateTheme(IsDarkTerminalTheme);
        return tabVm;
    }

    private static Task RunOnUiThreadAsync(Action action)
    {
        if (Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return Dispatcher.UIThread.InvokeAsync(action).GetTask();
    }

    private WorkspaceState BuildWorkspaceState()
    {
        var tabStates = Tabs
            .Where(t => t != null)
            .Select(t => t.ToTabState())
            .ToList();
        var closedTabStates = ClosedTabs.Select(c => c.ToTabState()).ToList();
        var selectedIndex = SelectedTab != null ? Math.Max(0, Tabs.IndexOf(SelectedTab)) : 0;
        var savedLanguage = _localizationService.IsCustomLanguageSelected ? _localizationService.CurrentLanguage : null;
        var pathHistories = _pathCommandHistoryService.ExportAll();
        var sharedDirectories = _directoryHistoryService.ExportAll();

        WindowWorkspaceState primaryWindowState = new(1, tabStates, selectedIndex);
        var secondaryStates = _windowCoordinator != null
            ? _windowCoordinator.Windows.Where(w => !w.IsPrimary).Select(sec => sec.ExportWorkspaceState()).ToList()
            : new List<WindowWorkspaceState>();

        return new WorkspaceState(
            tabStates,
            selectedIndex,
            savedLanguage,
            AppFontSizeLevel,
            TerminalFontSizeLevel,
            DefaultShellType,
            closedTabStates,
            pathHistories,
            sharedDirectories,
            IsDarkAppTheme,
            IsDarkTerminalTheme,
            Version: 2,
            PrimaryWindow: primaryWindowState,
            SecondaryWindows: secondaryStates.Count > 0 ? secondaryStates : null);
    }

    /// <summary>
    /// Persists the current workspace state asynchronously via the primary window anchor.
    /// </summary>
    public void TriggerSaveState()
    {
        if (_isDisposed || !_isInitialized || _isApplyingLoadedTabs) return;

        // If this is a secondary window, delegate save to primary window coordinator
        if (!IsPrimary)
        {
            _windowCoordinator?.PrimaryWindow?.TriggerSaveState();
            return;
        }

        var workspaceState = BuildWorkspaceState();
        _ = _persistenceService.SaveStateAsync(workspaceState);
    }

    /// <summary>
    /// Flushes workspace persistence synchronously on application shutdown.
    /// </summary>
    public void SaveCurrentStateSynchronously()
    {
        if (_isDisposed || !_isInitialized || !IsPrimary) return;

        // Prune non-existent paths for command history on application exit per REQ-HIST-003
        _pathCommandHistoryService.PruneNonExistentPaths();

        var workspaceState = BuildWorkspaceState();

        try
        {
            Task.Run(async () => await _persistenceService.SaveStateAsync(workspaceState).ConfigureAwait(false))
                .Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }
    }
}
