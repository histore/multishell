using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IPowerShellProcessService _powerShellProcessService;
    private readonly ITabStatePersistenceService _persistenceService;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localizationService;
    private readonly IFontSizeService _fontSizeService;
    private int _tabCounter;
    private bool _isDisposed;
    private bool _isInitialized;

    [ObservableProperty]
    private TerminalTabViewModel? _selectedTab;

    [ObservableProperty]
    private bool _isDarkAppTheme = true;

    [ObservableProperty]
    private bool _isDarkTerminalTheme = true;

    [ObservableProperty]
    private string _currentLanguage = "de";

    [ObservableProperty]
    private int _appFontSizeLevel = 3;

    [ObservableProperty]
    private int _terminalFontSizeLevel = 3;

    [ObservableProperty]
    private double _appFontScale = 1.0;

    [ObservableProperty]
    private double _terminalFontSize = 12.0;

    public bool IsGerman => string.Equals(CurrentLanguage, "de", StringComparison.OrdinalIgnoreCase);
    public bool IsEnglish => string.Equals(CurrentLanguage, "en", StringComparison.OrdinalIgnoreCase);
    public bool IsFrench => string.Equals(CurrentLanguage, "fr", StringComparison.OrdinalIgnoreCase);
    public bool IsSpanish => string.Equals(CurrentLanguage, "es", StringComparison.OrdinalIgnoreCase);

    public bool IsAppFontSizeLevel1 => AppFontSizeLevel == 1;
    public bool IsAppFontSizeLevel2 => AppFontSizeLevel == 2;
    public bool IsAppFontSizeLevel3 => AppFontSizeLevel == 3;
    public bool IsAppFontSizeLevel4 => AppFontSizeLevel == 4;
    public bool IsAppFontSizeLevel5 => AppFontSizeLevel == 5;

    public bool IsTerminalFontSizeLevel1 => TerminalFontSizeLevel == 1;
    public bool IsTerminalFontSizeLevel2 => TerminalFontSizeLevel == 2;
    public bool IsTerminalFontSizeLevel3 => TerminalFontSizeLevel == 3;
    public bool IsTerminalFontSizeLevel4 => TerminalFontSizeLevel == 4;
    public bool IsTerminalFontSizeLevel5 => TerminalFontSizeLevel == 5;

    public string CurrentLanguageUpper => CurrentLanguage.ToUpperInvariant();

    partial void OnCurrentLanguageChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentLanguageUpper));
        OnPropertyChanged(nameof(IsGerman));
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(IsFrench));
        OnPropertyChanged(nameof(IsSpanish));
    }

    partial void OnAppFontSizeLevelChanged(int value)
    {
        OnPropertyChanged(nameof(IsAppFontSizeLevel1));
        OnPropertyChanged(nameof(IsAppFontSizeLevel2));
        OnPropertyChanged(nameof(IsAppFontSizeLevel3));
        OnPropertyChanged(nameof(IsAppFontSizeLevel4));
        OnPropertyChanged(nameof(IsAppFontSizeLevel5));
    }

    partial void OnTerminalFontSizeLevelChanged(int value)
    {
        OnPropertyChanged(nameof(IsTerminalFontSizeLevel1));
        OnPropertyChanged(nameof(IsTerminalFontSizeLevel2));
        OnPropertyChanged(nameof(IsTerminalFontSizeLevel3));
        OnPropertyChanged(nameof(IsTerminalFontSizeLevel4));
        OnPropertyChanged(nameof(IsTerminalFontSizeLevel5));
    }

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

    public ObservableCollection<TerminalTabViewModel> Tabs { get; } = new();

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

    public MainViewModel()
        : this(new PowerShellProcessService(), new TabStatePersistenceService(), new ThemeService(), new LocalizationService(), new FontSizeService())
    {
    }

    public MainViewModel(IPowerShellProcessService powerShellProcessService)
        : this(powerShellProcessService, new TabStatePersistenceService(), new ThemeService(), new LocalizationService(), new FontSizeService())
    {
    }

    public MainViewModel(
        IPowerShellProcessService powerShellProcessService,
        ITabStatePersistenceService persistenceService,
        IThemeService? themeService = null,
        ILocalizationService? localizationService = null,
        IFontSizeService? fontSizeService = null)
    {
        _powerShellProcessService = powerShellProcessService ?? throw new ArgumentNullException(nameof(powerShellProcessService));
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        _themeService = themeService ?? new ThemeService();
        _localizationService = localizationService ?? new LocalizationService();
        _fontSizeService = fontSizeService ?? new FontSizeService();
        _isDarkAppTheme = _themeService.IsDarkAppTheme;
        _isDarkTerminalTheme = _themeService.IsDarkTerminalTheme;
        _currentLanguage = _localizationService.CurrentLanguage;
        _appFontSizeLevel = _fontSizeService.AppFontSizeLevel;
        _terminalFontSizeLevel = _fontSizeService.TerminalFontSizeLevel;
        _appFontScale = _fontSizeService.AppFontScale;
        _terminalFontSize = _fontSizeService.TerminalFontSize;

        _localizationService.LanguageChanged += lang =>
        {
            CurrentLanguage = lang;
            OnPropertyChanged(nameof(Loc));
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

        _ = InitializeWorkspaceAsync();
    }

    public async Task InitializeWorkspaceAsync()
    {
        var state = await _persistenceService.LoadStateAsync().ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(state?.SavedLanguage))
        {
            _localizationService.SetLanguage(state.SavedLanguage, isUserSelection: true);
        }

        if (state != null)
        {
            _fontSizeService.SetAppFontSizeLevel(state.AppFontSizeLevel);
            _fontSizeService.SetTerminalFontSizeLevel(state.TerminalFontSizeLevel);
        }

        void ApplyLoadedTabs()
        {
            foreach (var tab in Tabs.ToList())
            {
                tab.CloseRequested -= CloseTab;
                tab.DirectoryChanged -= OnTabDirectoryChanged;
                tab.HistoryChanged -= OnTabHistoryChanged;
                tab.Dispose();
            }
            Tabs.Clear();
            _tabCounter = 0;

            if (state != null && state.Tabs.Count > 0)
            {
                foreach (var tabState in state.Tabs)
                {
                    _tabCounter++;
                    var title = string.IsNullOrWhiteSpace(tabState.Title) ? $"PS {_tabCounter}" : tabState.Title;
                    var session = _powerShellProcessService.CreateSession(title, tabState.WorkingDirectory);
                    var tabVm = new TerminalTabViewModel(session);
                    tabVm.RestoreHistory(tabState.CommandHistory, tabState.DirectoryHistory);
                    RegisterTabEvents(tabVm);
                    Tabs.Add(tabVm);
                }

                int selectIndex = Math.Clamp(state.SelectedIndex, 0, Tabs.Count - 1);
                SelectedTab = Tabs[selectIndex];
            }
            else
            {
                AddNewTab();
            }

            _isInitialized = true;
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

    partial void OnSelectedTabChanged(TerminalTabViewModel? oldValue, TerminalTabViewModel? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
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

    [RelayCommand]
    public void AddNewTab()
    {
        AddNewTabWithDirectory(null);
    }

    public void AddNewTabWithDirectory(string? workingDirectory)
    {
        _tabCounter++;
        var title = $"PS {_tabCounter}";
        var session = _powerShellProcessService.CreateSession(title, workingDirectory);
        var newTab = new TerminalTabViewModel(session);
        RegisterTabEvents(newTab);
        Tabs.Add(newTab);
        SelectedTab = newTab;
        TriggerSaveState();
    }

    [RelayCommand]
    public void DuplicateTab(TerminalTabViewModel? tab = null)
    {
        var targetTab = tab ?? SelectedTab;
        if (targetTab == null)
        {
            return;
        }

        AddNewTabWithDirectory(targetTab.WorkingDirectory);
    }

    [RelayCommand]
    public void CloseTab(TerminalTabViewModel? tab)
    {
        if (tab == null)
        {
            return;
        }

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

        if (SelectedTab == tab)
        {
            SelectedTab = Tabs.Count > 0
                ? Tabs[Math.Min(index, Tabs.Count - 1)]
                : null;
        }

        TriggerSaveState();
    }

    [RelayCommand]
    public void ToggleAppTheme()
    {
        IsDarkAppTheme = !IsDarkAppTheme;
        _themeService.SetAppTheme(IsDarkAppTheme);
    }

    [RelayCommand]
    public void ToggleTerminalTheme()
    {
        IsDarkTerminalTheme = !IsDarkTerminalTheme;
        _themeService.SetTerminalTheme(IsDarkTerminalTheme);
        foreach (var tab in Tabs)
        {
            tab.UpdateTheme(IsDarkTerminalTheme);
        }
    }

    [RelayCommand]
    public void SetAppTheme(bool isDark)
    {
        IsDarkAppTheme = isDark;
        _themeService.SetAppTheme(isDark);
    }

    [RelayCommand]
    public void SetTerminalTheme(bool isDark)
    {
        IsDarkTerminalTheme = isDark;
        _themeService.SetTerminalTheme(isDark);
        foreach (var tab in Tabs)
        {
            tab.UpdateTheme(isDark);
        }
    }

    [RelayCommand]
    public void ToggleLanguage()
    {
        _localizationService.ToggleLanguage();
        TriggerSaveState();
    }

    [RelayCommand]
    public void SetLanguage(string cultureCode)
    {
        _localizationService.SetLanguage(cultureCode, isUserSelection: true);
        TriggerSaveState();
    }

    [RelayCommand]
    public void SelectLanguage(string cultureCode)
    {
        _localizationService.SetLanguage(cultureCode, isUserSelection: true);
        TriggerSaveState();
    }

    [RelayCommand]
    public void SetAppFontSizeLevel(object? level)
    {
        if (level is int intVal)
        {
            _fontSizeService.SetAppFontSizeLevel(intVal);
        }
        else if (level != null && int.TryParse(level.ToString(), out var parsed))
        {
            _fontSizeService.SetAppFontSizeLevel(parsed);
        }
    }

    [RelayCommand]
    public void SetTerminalFontSizeLevel(object? level)
    {
        if (level is int intVal)
        {
            _fontSizeService.SetTerminalFontSizeLevel(intVal);
        }
        else if (level != null && int.TryParse(level.ToString(), out var parsed))
        {
            _fontSizeService.SetTerminalFontSizeLevel(parsed);
        }
    }

    [RelayCommand]
    public void IncreaseAppFontSize()
    {
        _fontSizeService.SetAppFontSizeLevel(AppFontSizeLevel + 1);
    }

    [RelayCommand]
    public void DecreaseAppFontSize()
    {
        _fontSizeService.SetAppFontSizeLevel(AppFontSizeLevel - 1);
    }

    [RelayCommand]
    public void IncreaseTerminalFontSize()
    {
        _fontSizeService.SetTerminalFontSizeLevel(TerminalFontSizeLevel + 1);
    }

    [RelayCommand]
    public void DecreaseTerminalFontSize()
    {
        _fontSizeService.SetTerminalFontSizeLevel(TerminalFontSizeLevel - 1);
    }

    public void TriggerSaveState()
    {
        if (_isDisposed || !_isInitialized) return;

        var tabStates = Tabs.Select(t => new TabState(
            t.Title,
            t.WorkingDirectory,
            t.CommandHistory.ToList(),
            t.DirectoryHistory.ToList())).ToList();
        var selectedIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0;
        var savedLanguage = _localizationService.IsCustomLanguageSelected ? _localizationService.CurrentLanguage : null;
        var workspaceState = new WorkspaceState(tabStates, selectedIndex, savedLanguage, AppFontSizeLevel, TerminalFontSizeLevel);

        _ = _persistenceService.SaveStateAsync(workspaceState);
    }

    public void SaveCurrentStateSynchronously()
    {
        if (_isDisposed || !_isInitialized) return;

        var tabStates = Tabs.Select(t => new TabState(
            t.Title,
            t.WorkingDirectory,
            t.CommandHistory.ToList(),
            t.DirectoryHistory.ToList())).ToList();
        var selectedIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0;
        var savedLanguage = _localizationService.IsCustomLanguageSelected ? _localizationService.CurrentLanguage : null;
        var workspaceState = new WorkspaceState(tabStates, selectedIndex, savedLanguage, AppFontSizeLevel, TerminalFontSizeLevel);

        try
        {
            _persistenceService.SaveStateAsync(workspaceState).GetAwaiter().GetResult();
        }
        catch
        {
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

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        foreach (var tab in Tabs)
        {
            tab.CloseRequested -= CloseTab;
            tab.DirectoryChanged -= OnTabDirectoryChanged;
            tab.HistoryChanged -= OnTabHistoryChanged;
            tab.Dispose();
        }
        Tabs.Clear();
    }
}
