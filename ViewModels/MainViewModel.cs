using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Models;
using MultiShell.Services;

namespace MultiShell.ViewModels;

/// <summary>
/// Main application ViewModel managing terminal tabs, dual-theme switching, and workspace persistence.
/// </summary>
public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IShellProcessService _shellProcessService;
    private readonly ITabStatePersistenceService _persistenceService;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localizationService;
    private readonly IFontSizeService _fontSizeService;
    private readonly IPathCommandHistoryService _pathCommandHistoryService;
    private readonly IDirectoryHistoryService _directoryHistoryService;
    private int _tabCounter;
    private bool _isDisposed;
    private bool _isInitialized;
    private bool _isApplyingLoadedTabs;
    private readonly string? _initialDirectory;

    /// <summary>
    /// Gets the shared directory history service.
    /// </summary>
    public IDirectoryHistoryService DirectoryHistoryService => _directoryHistoryService;

    /// <summary>
    /// Gets the path command history service for path-bound command histories.
    /// </summary>
    public IPathCommandHistoryService PathCommandHistoryService => _pathCommandHistoryService;

    /// <summary>
    /// Gets the localization service for dynamic XAML string bindings.
    /// </summary>
    public ILocalizationService Loc => _localizationService;

    /// <summary>
    /// Gets the font size service.
    /// </summary>
    public IFontSizeService FontSizeService => _fontSizeService;

    /// <summary>
    /// Gets the list of available language options.
    /// </summary>
    public IReadOnlyList<LanguageOption> AvailableLanguages => LocalizationService.AllSupportedLanguages;

    /// <summary>
    /// Gets the application version dynamically determined from the assembly metadata or Git tag.
    /// </summary>
    public string AppVersion { get; } = DetermineAppVersion();

    /// <summary>
    /// Maximum number of tabs restored from saved state to prevent UI freeze on corrupted state files.
    /// </summary>
    public const int MaxRestoreTabsLimit = 50;

    /// <summary>
    /// Gets the official GitHub repository URL.
    /// </summary>
    public string GitHubUrl => "https://github.com/histore/multishell";

    /// <summary>
    /// Opens the official GitHub repository in the default web browser.
    /// </summary>
    [RelayCommand]
    public void OpenGitHubUrl()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = GitHubUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
        }
    }

    private static string DetermineAppVersion()
    {
        var informationalVersion = typeof(MainViewModel).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var cleanVersion = informationalVersion.Split('+')[0];
            if (cleanVersion.StartsWith("0.0.0", StringComparison.OrdinalIgnoreCase))
            {
                return "v0.0.1";
            }
            return cleanVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? cleanVersion
                : $"v{cleanVersion}";
        }

        var assemblyVersion = typeof(MainViewModel).Assembly.GetName().Version;
        if (assemblyVersion != null)
        {
            return $"v{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
        }

        return "v0.0.1";
    }

    public MainViewModel(string? initialDirectory = null)
        : this(new ShellProcessService(), new TabStatePersistenceService(), new ThemeService(), new LocalizationService(), new FontSizeService(), new ShellDiscoveryService(), new TerminalProfileService(), initialDirectory: initialDirectory)
    {
    }

    public MainViewModel(IShellProcessService shellProcessService, string? initialDirectory = null)
        : this(shellProcessService, new TabStatePersistenceService(), new ThemeService(), new LocalizationService(), new FontSizeService(), new ShellDiscoveryService(), new TerminalProfileService(), initialDirectory: initialDirectory)
    {
    }

    public MainViewModel(IShellProcessService shellProcessService, ITabStatePersistenceService persistenceService, IThemeService themeService, string? initialDirectory = null)
        : this(shellProcessService, persistenceService, themeService, new LocalizationService(), new FontSizeService(), new ShellDiscoveryService(), new TerminalProfileService(), initialDirectory: initialDirectory)
    {
    }

    public MainViewModel(
        IShellProcessService shellProcessService,
        ITabStatePersistenceService persistenceService,
        IThemeService themeService,
        ILocalizationService localizationService,
        IFontSizeService fontSizeService,
        IShellDiscoveryService? shellDiscoveryService = null,
        ITerminalProfileService? terminalProfileService = null,
        IPathCommandHistoryService? pathCommandHistoryService = null,
        IDirectoryHistoryService? directoryHistoryService = null,
        string? initialDirectory = null)
    {
        _initialDirectory = initialDirectory;
        _shellProcessService = shellProcessService ?? throw new ArgumentNullException(nameof(shellProcessService));
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        _themeService = themeService ?? new ThemeService();
        _localizationService = localizationService ?? new LocalizationService();
        _fontSizeService = fontSizeService ?? new FontSizeService();
        _shellDiscoveryService = shellDiscoveryService ?? new ShellDiscoveryService(_localizationService);
        _terminalProfileService = terminalProfileService ?? new TerminalProfileService(localizationService: _localizationService);
        _terminalProfileService.ProfilesChanged += ReloadProfiles;
        _pathCommandHistoryService = pathCommandHistoryService ?? new PathCommandHistoryService();
        _directoryHistoryService = directoryHistoryService ?? new DirectoryHistoryService();
        _isDarkAppTheme = _themeService.IsDarkAppTheme;
        _isDarkTerminalTheme = _themeService.IsDarkTerminalTheme;
        _currentLanguage = _localizationService.CurrentLanguage;
        _appFontSizeLevel = _fontSizeService.AppFontSizeLevel;
        _terminalFontSizeLevel = _fontSizeService.TerminalFontSizeLevel;
        _appFontScale = _fontSizeService.AppFontScale;
        _terminalFontSize = _fontSizeService.TerminalFontSize;

        Tabs.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasOpenTabs));
            OnPropertyChanged(nameof(HasNoTabs));
        };

        ClosedTabs.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasClosedTabs));
        };

        _localizationService.LanguageChanged += lang =>
        {
            CurrentLanguage = lang;
            OnPropertyChanged(nameof(Loc));
            OnPropertyChanged(nameof(AvailableShells));
            OnPropertyChanged(nameof(NewTabTooltip));
        };

        _fontSizeService.AppFontSizeLevelChanged += lvl =>
        {
            AppFontSizeLevel = lvl;
            AppFontScale = _fontSizeService.AppFontScale;
            TriggerSaveState();
        };

        _fontSizeService.TerminalFontSizeLevelChanged += lvl =>
        {
            TerminalFontSizeLevel = lvl;
            TerminalFontSize = _fontSizeService.TerminalFontSize;
            foreach (var tab in Tabs)
            {
                tab.UpdateFontSize(_fontSizeService.TerminalFontSize);
            }
            TriggerSaveState();
        };

        ReloadProfiles();
        _ = InitializeWorkspaceAsync();
    }

    public async Task InitializeWorkspaceAsync()
    {
        var state = await _persistenceService.LoadStateAsync();

        if (!string.IsNullOrWhiteSpace(state?.SavedLanguage))
        {
            _localizationService.SetLanguage(state.SavedLanguage, isUserSelection: true);
        }

        if (state != null)
        {
            _fontSizeService.SetAppFontSizeLevel(state.AppFontSizeLevel);
            _fontSizeService.SetTerminalFontSizeLevel(state.TerminalFontSizeLevel);
            DefaultShellType = state.DefaultShellType;

            if (state.PathCommandHistory != null && state.PathCommandHistory.Count > 0)
            {
                _pathCommandHistoryService.ImportAll(state.PathCommandHistory);
            }
            else
            {
                // Backwards compatibility migration from legacy tab CommandHistory
                foreach (var tabState in state.Tabs)
                {
                    if (tabState.CommandHistory != null && !string.IsNullOrWhiteSpace(tabState.WorkingDirectory))
                    {
                        foreach (var cmd in tabState.CommandHistory)
                        {
                            _pathCommandHistoryService.RecordCommand(tabState.WorkingDirectory, cmd);
                        }
                    }
                }
            }

            if (state.SharedDirectoryHistory != null && state.SharedDirectoryHistory.Count > 0)
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

        void ApplyLoadedTabs()
        {
            _isApplyingLoadedTabs = true;
            try
            {
                foreach (var tab in Tabs.ToList())
                {
                    if (tab == null) continue;
                    tab.CloseRequested -= CloseTab;
                    tab.DirectoryChanged -= OnTabDirectoryChanged;
                    tab.HistoryChanged -= OnTabHistoryChanged;
                    tab.Dispose();
                }
                Tabs.Clear();
                _tabCounter = 0;

                if (state != null && state.Tabs.Count > 0)
                {
                    var tabsToRestore = state.Tabs.Take(MaxRestoreTabsLimit).ToList();
                    foreach (var tabState in tabsToRestore)
                    {
                        _tabCounter++;
                        var title = string.IsNullOrWhiteSpace(tabState.Title) ? GetDefaultTitle(tabState.ShellType, _tabCounter) : tabState.Title;
                        var session = _shellProcessService.CreateSession(title, tabState.WorkingDirectory, tabState.ShellType);
                        var tabVm = new TerminalTabViewModel(session, pathCommandHistoryService: _pathCommandHistoryService, directoryHistoryService: _directoryHistoryService, localizationService: _localizationService);
                        tabVm.RestoreHistory(tabState.CommandHistory, tabState.DirectoryHistory);
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
                else
                {
                    if (!string.IsNullOrWhiteSpace(_initialDirectory))
                    {
                        AddNewTabWithDirectory(_initialDirectory, DefaultShellType);
                    }
                    else
                    {
                        AddNewTab();
                    }
                }

                if (state?.ClosedTabs != null && state.ClosedTabs.Count > 0)
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

        if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            ApplyLoadedTabs();
        }
        else
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(ApplyLoadedTabs);
        }
    }

    public void TriggerSaveState()
    {
        if (_isDisposed || !_isInitialized || _isApplyingLoadedTabs) return;

        var tabStates = Tabs
            .Where(t => t != null)
            .Select(t => new TabState(
                t.Title,
                t.WorkingDirectory,
                t.CommandHistory?.ToList() ?? new List<string>(),
                t.DirectoryHistory?.ToList() ?? new List<string>(),
                t.ShellType))
            .ToList();
        var closedTabStates = ClosedTabs.Select(c => c.ToTabState()).ToList();
        var selectedIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0;
        var savedLanguage = _localizationService.IsCustomLanguageSelected ? _localizationService.CurrentLanguage : null;
        var pathHistories = _pathCommandHistoryService.ExportAll();
        var sharedDirectories = _directoryHistoryService.ExportAll();
        var workspaceState = new WorkspaceState(
            tabStates,
            selectedIndex,
            savedLanguage,
            AppFontSizeLevel,
            TerminalFontSizeLevel,
            DefaultShellType,
            closedTabStates,
            pathHistories,
            sharedDirectories);

        _ = _persistenceService.SaveStateAsync(workspaceState);
    }

    public void SaveCurrentStateSynchronously()
    {
        if (_isDisposed || !_isInitialized) return;

        // Prune non-existent paths for command history on application exit per REQ-HIST-003
        _pathCommandHistoryService.PruneNonExistentPaths();

        var tabStates = Tabs.Select(t => new TabState(
            t.Title,
            t.WorkingDirectory,
            t.CommandHistory.ToList(),
            t.DirectoryHistory.ToList(),
            t.ShellType)).ToList();
        var closedTabStates = ClosedTabs.Select(c => c.ToTabState()).ToList();
        var selectedIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0;
        var savedLanguage = _localizationService.IsCustomLanguageSelected ? _localizationService.CurrentLanguage : null;
        var pathHistories = _pathCommandHistoryService.ExportAll();
        var sharedDirectories = _directoryHistoryService.ExportAll();
        var workspaceState = new WorkspaceState(
            tabStates,
            selectedIndex,
            savedLanguage,
            AppFontSizeLevel,
            TerminalFontSizeLevel,
            DefaultShellType,
            closedTabStates,
            pathHistories,
            sharedDirectories);

        try
        {
            _persistenceService.SaveStateAsync(workspaceState).GetAwaiter().GetResult();
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        var tabsToDispose = Tabs.ToArray();
        foreach (var tab in tabsToDispose)
        {
            if (tab == null) continue;
            tab.CloseRequested -= CloseTab;
            tab.DirectoryChanged -= OnTabDirectoryChanged;
            tab.HistoryChanged -= OnTabHistoryChanged;
            tab.Dispose();
        }
        Tabs.Clear();
    }
}
