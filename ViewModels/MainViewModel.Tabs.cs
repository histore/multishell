using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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
        if (oldValue != null)
        {
            if (oldValue.IsRenaming)
            {
                oldValue.CommitRenaming();
            }
            oldValue.IsSelected = false;
        }
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
            var prefix = IsPrimary ? "" : $"[Fenster {WindowNumber} ⧉] ";
            if (SelectedTab == null) return $"{prefix}MultiShell";

            var rawTitle = SelectedTab.HasCustomTitle
                ? SelectedTab.CustomTitle!
                : (!string.IsNullOrWhiteSpace(SelectedTab.WorkingDirectory)
                    ? SelectedTab.WorkingDirectory
                    : SelectedTab.Title);

            if (string.IsNullOrWhiteSpace(rawTitle)) return $"{prefix}MultiShell";

            var formatted = TerminalTabViewModel.FormatMiddleEllipsis(rawTitle, maxLength: 65);
            return $"{prefix}MultiShell - {formatted}";
        }
    }

    public ObservableCollection<TerminalTabViewModel> Tabs { get; } = new();

    /// <summary>
    /// Gets a value indicating whether there are any open terminal tabs.
    /// </summary>
    public bool HasOpenTabs => Tabs.Count > 0;

    /// <summary>
    /// Gets a value indicating whether all tabs are closed (empty workspace state).
    /// </summary>
    public bool HasNoTabs => Tabs.Count == 0;

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
        var tab = new TerminalTabViewModel(session, pathCommandHistoryService: _pathCommandHistoryService, directoryHistoryService: _directoryHistoryService, localizationService: _localizationService);
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

    private (string? WorkingDirectory, string? ExecutablePath, string? Arguments) ResolveProfileLaunchParameters(ShellType shellType, string? requestedDirectory)
    {
        string? targetDir = requestedDirectory;
        string? customExe = null;
        string? customArgs = null;

        void ApplyProfile(string? profileWorkingDir, bool isBuiltIn, string? exe, string? args)
        {
            if (string.IsNullOrWhiteSpace(targetDir) && !string.IsNullOrWhiteSpace(profileWorkingDir))
            {
                targetDir = profileWorkingDir;
            }
            if (!isBuiltIn)
            {
                customExe = exe;
                customArgs = args;
            }
        }

        var profileVm = Profiles.FirstOrDefault(p => p.ShellType == shellType);
        if (profileVm != null)
        {
            ApplyProfile(profileVm.WorkingDirectory, profileVm.IsBuiltIn, profileVm.ExecutablePath, profileVm.Arguments);
        }
        else
        {
            var profileModel = _terminalProfileService.GetProfiles().FirstOrDefault(p => p.ShellType == shellType);
            if (profileModel != null)
            {
                ApplyProfile(profileModel.WorkingDirectory, profileModel.IsBuiltIn, profileModel.ExecutablePath, profileModel.Arguments);
            }
        }

        return (targetDir, customExe, customArgs);
    }

    private TerminalTabViewModel CreateNewTab(string? workingDirectory = null, ShellType shellType = ShellType.PowerShell)
    {
        int nextId = Tabs.Count + 1;
        var title = GetDefaultTitle(shellType, nextId);

        var (targetDir, customExe, customArgs) = ResolveProfileLaunchParameters(shellType, workingDirectory);
        var session = _shellProcessService.CreateSession(title, targetDir, shellType, customExe, customArgs);

        var tab = new TerminalTabViewModel(session, pathCommandHistoryService: _pathCommandHistoryService, directoryHistoryService: _directoryHistoryService, localizationService: _localizationService);
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

        if (tab.IsRenaming)
        {
            tab.CommitRenaming();
        }

        // Capture closed tab state before disposing
        var closedItem = new ClosedTabItemViewModel(
            tab.Title,
            tab.WorkingDirectory,
            tab.ShellType,
            tab.CommandHistory,
            tab.DirectoryHistory,
            DateTime.Now,
            customTitle: tab.CustomTitle,
            tabColor: tab.TabColor);

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
        UnregisterTabEvents(tab);

        // Push to recently closed history (newest at index 0, capped at MaxClosedTabsCount)
        PushClosedTab(closedItem);

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

    private void RegisterTabEvents(TerminalTabViewModel tab)
    {
        tab.CloseRequested += CloseTab;
        tab.UndockRequested += OnTabUndockRequested;
        tab.DirectoryChanged += OnTabDirectoryChanged;
        tab.HistoryChanged += OnTabHistoryChanged;
        tab.PropertyChanged += OnTabPropertyChanged;
        tab.CommandExecutionCompleted += OnTabCommandExecutionCompleted;
        tab.RequestFolderPickerAsync = FolderPickerHandler;
        tab.SessionFactory = (dir, shell) => _shellProcessService.CreateSession(tab.Title, dir, shell);
        tab.UpdateTheme(IsDarkTerminalTheme);
        tab.UpdateFontSize(_fontSizeService.TerminalFontSize);
    }

    internal void UnregisterTabEvents(TerminalTabViewModel tab)
    {
        tab.CloseRequested -= CloseTab;
        tab.UndockRequested -= OnTabUndockRequested;
        tab.DirectoryChanged -= OnTabDirectoryChanged;
        tab.HistoryChanged -= OnTabHistoryChanged;
        tab.PropertyChanged -= OnTabPropertyChanged;
        tab.CommandExecutionCompleted -= OnTabCommandExecutionCompleted;
        tab.RequestFolderPickerAsync = null;
        tab.SessionFactory = null;
    }

    private void OnTabUndockRequested(TerminalTabViewModel tab)
    {
        _ = UndockTabAsync(tab);
    }

    /// <summary>
    /// Handles user interaction on a status bar path breadcrumb (REQ-TERM-013).
    /// </summary>
    [RelayCommand]
    public async Task HandleBreadcrumbClickAsync(PathBreadcrumbSegment? segment)
    {
        if (SelectedTab != null && segment != null)
        {
            await SelectedTab.HandleBreadcrumbClickAsync(segment);
        }
    }

    private void OnTabCommandExecutionCompleted(TerminalTabViewModel tab, CommandExecutionResult result)
    {
        // Only notify if tab is in the background or window is inactive (REQ-TERM-012)
        if (tab != SelectedTab || IsWindowInactive)
        {
            _toastNotificationService.ShowProcessCompletionNotification(
                tab.DisplayTitle,
                result.Command,
                result.Duration,
                result.ExitCode,
                onActivated: () =>
                {
                    if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                    {
                        SelectedTab = tab;
                    }
                    else
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => SelectedTab = tab);
                    }
                });
        }
    }

    private void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not TerminalTabViewModel tab) return;

        if (e.PropertyName is nameof(TerminalTabViewModel.CustomTitle) or nameof(TerminalTabViewModel.TabColor))
        {
            if (tab == SelectedTab)
            {
                OnPropertyChanged(nameof(WindowTitle));
            }
            TriggerSaveState();
        }
        else if (e.PropertyName == nameof(TerminalTabViewModel.WorkingDirectory))
        {
            UpdateTabPathColorStripes(tab);
        }
    }

    private void OnTabDirectoryChanged(TerminalTabViewModel tab, string newDirectory)
    {
        if (tab == SelectedTab)
        {
            OnPropertyChanged(nameof(WindowTitle));
        }
        // Note: UpdateTabPathColorStripes is already triggered via OnTabPropertyChanged(WorkingDirectory).
        TriggerSaveState();
    }

    /// <summary>
    /// Recalculates and updates the path color stripes across all open tabs (REQ-TAB-025).
    /// </summary>
    public void UpdateAllTabPathColorStripes()
    {
        if (Tabs.Count == 0) return;

        foreach (var tab in Tabs)
        {
            UpdateTabPathColorStripes(tab);
        }
    }

    /// <summary>
    /// Updates the path color stripes for a specific tab based on its working directory (REQ-TAB-025).
    /// </summary>
    public void UpdateTabPathColorStripes(TerminalTabViewModel tab)
    {
        if (tab == null) return;
        tab.PathColorStripes = _pathColorCodingService.GetStripesForPath(tab.WorkingDirectory);
    }

    private void OnTabHistoryChanged(TerminalTabViewModel tab)
    {
        TriggerSaveState();
    }

    /// <summary>
    /// Handles directory navigation or tab creation from a drag-and-drop operation (REQ-TAB-024).
    /// </summary>
    /// <param name="directory">The resolved directory path to open.</param>
    /// <param name="openInNewTab">True if Shift was held or user requested a new tab; false to navigate current/target tab.</param>
    /// <param name="targetTab">Optional specific target tab dropped onto.</param>
    public void OpenDirectoryFromDrop(string? directory, bool openInNewTab, TerminalTabViewModel? targetTab = null)
    {
        if (string.IsNullOrWhiteSpace(directory)) return;

        if (openInNewTab || Tabs.Count == 0 || SelectedTab == null)
        {
            AddNewTabWithDirectory(directory, DefaultShellType);
            return;
        }

        var tabToNavigate = targetTab ?? SelectedTab;
        if (tabToNavigate != null)
        {
            SelectedTab = tabToNavigate;
            tabToNavigate.NavigateToDirectory(directory);
        }
    }
}


