using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Models;
using MultiShell.Services;

namespace MultiShell.ViewModels;

/// <summary>
/// Main application ViewModel managing terminal tabs, dual-theme switching, and workspace persistence.
/// </summary>
public partial class MainViewModel : ViewModelBase, IDisposable, ICoordinatedWindow
{
    private readonly IShellProcessService _shellProcessService;
    private readonly ITabStatePersistenceService _persistenceService;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localizationService;
    private readonly IFontSizeService _fontSizeService;
    private readonly IPathCommandHistoryService _pathCommandHistoryService;
    private readonly IDirectoryHistoryService _directoryHistoryService;
    private readonly IPathColorCodingService _pathColorCodingService;
    private readonly IToastNotificationService _toastNotificationService;
    private readonly IWindowCoordinatorService _windowCoordinator;
    private readonly IDispatcherService _dispatcherService;
    private readonly ITerminalModelFactory _terminalModelFactory;
    private int _tabCounter;
    private bool _isDisposed;
    private bool _isInitialized;
    private bool _isApplyingLoadedTabs;
    private readonly string? _initialDirectory;
    private readonly Action<string> _languageChangedHandler;
    private readonly Action<int> _appFontSizeLevelChangedHandler;
    private readonly Action<int> _terminalFontSizeLevelChangedHandler;

    /// <summary>
    /// Gets or sets whether the application window is currently inactive (unfocused) in the OS.
    /// </summary>
    public bool IsWindowInactive { get; set; }

    [ObservableProperty]
    private bool _isCapsLockActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(CanDockBack))]
    private bool _isPrimary = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private int _windowNumber = 1;

    /// <summary>
    /// Gets whether this window is a secondary satellite window that can dock back into the primary window.
    /// </summary>
    public bool CanDockBack => !IsPrimary;

    /// <summary>
    /// Gets the window coordinator service managing multi-window lifecycle.
    /// </summary>
    public IWindowCoordinatorService WindowCoordinator => _windowCoordinator;

    /// <summary>
    /// Queries the native operating system keyboard state and updates the Caps Lock indicator.
    /// </summary>
    public void UpdateCapsLockState()
    {
        IsCapsLockActive = KeyboardStateHelper.QueryCapsLockState();
    }

    private Func<string?, Task<string?>>? _folderPickerHandler;

    /// <summary>
    /// Gets or sets the UI folder picker handler used by terminal status bar breadcrumbs (REQ-TERM-013).
    /// </summary>
    public Func<string?, Task<string?>>? FolderPickerHandler
    {
        get => _folderPickerHandler;
        set
        {
            _folderPickerHandler = value;
            foreach (var tab in Tabs)
            {
                tab.RequestFolderPickerAsync = value;
            }
        }
    }

    /// <summary>
    /// Gets the shared directory history service.
    /// </summary>
    public IDirectoryHistoryService DirectoryHistoryService => _directoryHistoryService;

    /// <summary>
    /// Gets the path color coding service for tab folder pattern visualization.
    /// </summary>
    public IPathColorCodingService PathColorCodingService => _pathColorCodingService;

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
    /// Gets the UI and terminal framework stack description displayed in the About dialog.
    /// </summary>
    public string UiTerminalStack { get; } = DetermineUiTerminalStack();

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

    private static string DetermineUiTerminalStack() => UiFrameworkInfo.GetUiTerminalStack();

    public MainViewModel(string? initialDirectory = null, IWindowCoordinatorService? windowCoordinator = null)
        : this(new ShellProcessService(), new TabStatePersistenceService(), new ThemeService(), new LocalizationService(), new FontSizeService(), new ShellDiscoveryService(), new TerminalProfileService(), windowCoordinator: windowCoordinator, initialDirectory: initialDirectory)
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
        IPathColorCodingService? pathColorCodingService = null,
        IToastNotificationService? toastNotificationService = null,
        IWindowCoordinatorService? windowCoordinator = null,
        string? initialDirectory = null,
        bool isSatellite = false,
        IDispatcherService? dispatcherService = null,
        ITerminalModelFactory? terminalModelFactory = null)
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
        _pathColorCodingService = pathColorCodingService ?? new PathColorCodingService();
        _toastNotificationService = toastNotificationService ?? new WindowsToastNotificationService(_localizationService);
        _windowCoordinator = windowCoordinator ?? WindowCoordinatorService.Instance;
        _dispatcherService = dispatcherService ?? AvaloniaDispatcherService.Instance;
        _terminalModelFactory = terminalModelFactory ?? AvaloniaTerminalModelFactory.Instance;
        _isDarkAppTheme = _themeService.IsDarkAppTheme;
        _isDarkTerminalTheme = _themeService.IsDarkTerminalTheme;
        _currentLanguage = _localizationService.CurrentLanguage;
        _appFontSizeLevel = _fontSizeService.AppFontSizeLevel;
        _terminalFontSizeLevel = _fontSizeService.TerminalFontSizeLevel;
        _appFontScale = _fontSizeService.AppFontScale;
        _terminalFontSize = _fontSizeService.TerminalFontSize;
        UpdateCapsLockState();

        Tabs.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasOpenTabs));
            OnPropertyChanged(nameof(HasNoTabs));

            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (TerminalTabViewModel tab in e.NewItems)
                {
                    UpdateTabPathColorStripes(tab);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                UpdateAllTabPathColorStripes();
            }
        };

        ClosedTabs.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasClosedTabs));
        };

        _languageChangedHandler = lang =>
        {
            CurrentLanguage = lang;
            OnPropertyChanged(nameof(Loc));
            OnPropertyChanged(nameof(AvailableShells));
            OnPropertyChanged(nameof(NewTabTooltip));
        };
        _localizationService.LanguageChanged += _languageChangedHandler;

        _appFontSizeLevelChangedHandler = lvl =>
        {
            AppFontSizeLevel = lvl;
            AppFontScale = _fontSizeService.AppFontScale;
            TriggerSaveState();
        };
        _fontSizeService.AppFontSizeLevelChanged += _appFontSizeLevelChangedHandler;

        _terminalFontSizeLevelChangedHandler = lvl =>
        {
            TerminalFontSizeLevel = lvl;
            TerminalFontSize = _fontSizeService.TerminalFontSize;
            foreach (var tab in Tabs)
            {
                tab.UpdateFontSize(_fontSizeService.TerminalFontSize);
            }
            TriggerSaveState();
        };
        _fontSizeService.TerminalFontSizeLevelChanged += _terminalFontSizeLevelChangedHandler;

        ReloadProfiles();
        if (!isSatellite)
        {
            _initTask = DoInitializeWorkspaceAsync();
        }
        else
        {
            _isInitialized = true;
            _initTask = Task.CompletedTask;
        }
    }

    /// <summary>
    /// Prunes directories that no longer exist on the local filesystem from the shared directory history.
    /// </summary>
    [RelayCommand]
    public void PruneDirectoryHistory()
    {
        _directoryHistoryService.PruneNonExistentDirectories();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _terminalProfileService.ProfilesChanged -= ReloadProfiles;
        _localizationService.LanguageChanged -= _languageChangedHandler;
        _fontSizeService.AppFontSizeLevelChanged -= _appFontSizeLevelChangedHandler;
        _fontSizeService.TerminalFontSizeLevelChanged -= _terminalFontSizeLevelChangedHandler;

        var tabsToDispose = Tabs.ToArray();
        foreach (var tab in tabsToDispose)
        {
            if (tab == null) continue;
            UnregisterTabEvents(tab);
            tab.Dispose();
        }
        Tabs.Clear();
    }
}
