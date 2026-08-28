using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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
    private readonly IShellProcessService _shellProcessService;
    private readonly ITabStatePersistenceService _persistenceService;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localizationService;
    private readonly IFontSizeService _fontSizeService;
    private int _tabCounter;
    private bool _isDisposed;
    private bool _isInitialized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private TerminalTabViewModel? _selectedTab;

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

    [ObservableProperty]
    private ShellType _defaultShellType = ShellType.PowerShell;

    [ObservableProperty]
    private bool _isProfilesModalOpen;

    [ObservableProperty]
    private bool _isEditingProfile;

    [ObservableProperty]
    private bool _isCreatingNewProfile;

    [ObservableProperty]
    private Guid? _editingProfileId;

    [ObservableProperty]
    private string _editingProfileName = string.Empty;

    [ObservableProperty]
    private string _editingExecutablePath = string.Empty;

    [ObservableProperty]
    private string? _editingArguments;

    [ObservableProperty]
    private string _editingIconTag = "PS";

    [ObservableProperty]
    private ShellType _editingShellType = ShellType.PowerShell;

    [ObservableProperty]
    private TerminalProfileItemViewModel? _selectedProfile;

    public ObservableCollection<TerminalProfileItemViewModel> Profiles { get; } = new();

    public string NewTabTooltip
    {
        get
        {
            var shellKey = DefaultShellType switch
            {
                ShellType.PowerShell => "Shell_PowerShell",
                ShellType.NuShell => "Shell_NuShell",
                ShellType.WSL => "Shell_WSL",
                ShellType.CMD => "Shell_CMD",
                _ => "Shell_PowerShell"
            };
            var shellName = _localizationService[shellKey];
            return string.Format(_localizationService["Btn_New_Tab_With_Shell_Tooltip"], shellName);
        }
    }

    partial void OnDefaultShellTypeChanged(ShellType value)
    {
        OnPropertyChanged(nameof(NewTabTooltip));
        TriggerSaveState();
    }

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

    private readonly IShellDiscoveryService _shellDiscoveryService;
    private readonly ITerminalProfileService _terminalProfileService;

    /// <summary>
    /// Gets all detected shells and their availability on this machine.
    /// </summary>
    public IReadOnlyList<ShellOptionInfo> AvailableShells => _shellDiscoveryService.GetAvailableShells();

    public MainViewModel()
        : this(new ShellProcessService(), new TabStatePersistenceService(), new ThemeService(), new LocalizationService(), new FontSizeService(), new ShellDiscoveryService(), new TerminalProfileService())
    {
    }
    public MainViewModel(IShellProcessService shellProcessService)
        : this(shellProcessService, new TabStatePersistenceService(), new ThemeService(), new LocalizationService(), new FontSizeService(), new ShellDiscoveryService(), new TerminalProfileService())
    {
    }

    public MainViewModel(IShellProcessService shellProcessService, ITabStatePersistenceService persistenceService, IThemeService themeService)
        : this(shellProcessService, persistenceService, themeService, new LocalizationService(), new FontSizeService(), new ShellDiscoveryService(), new TerminalProfileService())
    {
    }

    public MainViewModel(
        IShellProcessService shellProcessService,
        ITabStatePersistenceService persistenceService,
        IThemeService themeService,
        ILocalizationService localizationService,
        IFontSizeService fontSizeService,
        IShellDiscoveryService? shellDiscoveryService = null,
        ITerminalProfileService? terminalProfileService = null)
    {
        _shellProcessService = shellProcessService ?? throw new ArgumentNullException(nameof(shellProcessService));
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        _themeService = themeService ?? new ThemeService();
        _localizationService = localizationService ?? new LocalizationService();
        _fontSizeService = fontSizeService ?? new FontSizeService();
        _shellDiscoveryService = shellDiscoveryService ?? new ShellDiscoveryService(_localizationService);
        _terminalProfileService = terminalProfileService ?? new TerminalProfileService(localizationService: _localizationService);
        _terminalProfileService.ProfilesChanged += ReloadProfiles;
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

    private void ReloadProfiles()
    {
        void Update()
        {
            var loaded = _terminalProfileService.GetProfiles();
            Profiles.Clear();
            foreach (var p in loaded)
            {
                Profiles.Add(new TerminalProfileItemViewModel(p));
            }
        }

        if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Update);
        }
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
                    var title = string.IsNullOrWhiteSpace(tabState.Title) ? GetDefaultTitle(tabState.ShellType, _tabCounter) : tabState.Title;
                    var session = _shellProcessService.CreateSession(title, tabState.WorkingDirectory, tabState.ShellType);
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
    public void OpenProfilesModal()
    {
        IsProfilesModalOpen = true;
        CancelEditProfile();
    }

    [RelayCommand]
    public void CloseProfilesModal()
    {
        IsProfilesModalOpen = false;
        CancelEditProfile();
    }

    [RelayCommand]
    public void StartNewProfile()
    {
        IsCreatingNewProfile = true;
        IsEditingProfile = true;
        EditingProfileId = null;
        EditingProfileName = "Custom Terminal";
        EditingExecutablePath = "pwsh.exe";
        EditingArguments = string.Empty;
        EditingIconTag = "SH";
        EditingShellType = ShellType.PowerShell;
    }

    [RelayCommand]
    public void StartEditProfile(TerminalProfileItemViewModel? item)
    {
        if (item == null) return;
        IsCreatingNewProfile = false;
        IsEditingProfile = true;
        EditingProfileId = item.Id;
        EditingProfileName = item.Name;
        EditingExecutablePath = item.ExecutablePath;
        EditingArguments = item.Arguments;
        EditingIconTag = item.IconTag;
        EditingShellType = item.ShellType;
    }

    [RelayCommand]
    public void CancelEditProfile()
    {
        IsEditingProfile = false;
        IsCreatingNewProfile = false;
        EditingProfileId = null;
    }

    [RelayCommand]
    public async Task SaveProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(EditingProfileName) || string.IsNullOrWhiteSpace(EditingExecutablePath))
        {
            return;
        }

        var profile = new TerminalProfile(
            EditingProfileId ?? Guid.NewGuid(),
            EditingProfileName.Trim(),
            EditingExecutablePath.Trim(),
            string.IsNullOrWhiteSpace(EditingArguments) ? null : EditingArguments.Trim(),
            null,
            string.IsNullOrWhiteSpace(EditingIconTag) ? "PS" : EditingIconTag.Trim().ToUpperInvariant(),
            EditingShellType,
            IsBuiltIn: false);

        if (IsCreatingNewProfile)
        {
            await _terminalProfileService.AddProfileAsync(profile);
        }
        else
        {
            await _terminalProfileService.UpdateProfileAsync(profile);
        }

        CancelEditProfile();
    }

    [RelayCommand]
    public async Task DeleteProfileAsync(TerminalProfileItemViewModel? item)
    {
        if (item == null) return;
        await _terminalProfileService.DeleteProfileAsync(item.Id);
        if (EditingProfileId == item.Id)
        {
            CancelEditProfile();
        }
    }

    [RelayCommand]
    public async Task ResetProfilesToDefaultAsync()
    {
        await _terminalProfileService.ResetToDefaultsAsync();
        CancelEditProfile();
    }

    [RelayCommand]
    public void AddNewTabWithProfile(TerminalProfileItemViewModel? profileVm)
    {
        if (profileVm == null) return;
        DefaultShellType = profileVm.ShellType;
        int nextId = Tabs.Count + 1;
        var title = $"{profileVm.IconTag} {nextId}";
        var session = _shellProcessService.CreateSession(title, null, profileVm.ShellType, profileVm.ExecutablePath, profileVm.Arguments);
        var tab = new TerminalTabViewModel(session);
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

    public void AddNewTabWithDirectory(string? workingDirectory, ShellType shellType = ShellType.PowerShell)
    {
        var newTab = CreateNewTab(workingDirectory, shellType);
        RegisterTabEvents(newTab);
        Tabs.Add(newTab);
        SelectedTab = newTab;
        TriggerSaveState();
    }

    private TerminalTabViewModel CreateNewTab(string? workingDirectory = null, ShellType shellType = ShellType.PowerShell)
    {
        int nextId = Tabs.Count + 1;
        var title = GetDefaultTitle(shellType, nextId);
        var session = _shellProcessService.CreateSession(title, workingDirectory, shellType);
        
        var tab = new TerminalTabViewModel(session);
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

        AddNewTabWithDirectory(targetTab.WorkingDirectory, targetTab.ShellType);
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
            t.DirectoryHistory.ToList(),
            t.ShellType)).ToList();
        var selectedIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0;
        var savedLanguage = _localizationService.IsCustomLanguageSelected ? _localizationService.CurrentLanguage : null;
        var workspaceState = new WorkspaceState(tabStates, selectedIndex, savedLanguage, AppFontSizeLevel, TerminalFontSizeLevel, DefaultShellType);

        _ = _persistenceService.SaveStateAsync(workspaceState);
    }

    public void SaveCurrentStateSynchronously()
    {
        if (_isDisposed || !_isInitialized) return;

        var tabStates = Tabs.Select(t => new TabState(
            t.Title,
            t.WorkingDirectory,
            t.CommandHistory.ToList(),
            t.DirectoryHistory.ToList(),
            t.ShellType)).ToList();
        var selectedIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0;
        var savedLanguage = _localizationService.IsCustomLanguageSelected ? _localizationService.CurrentLanguage : null;
        var workspaceState = new WorkspaceState(tabStates, selectedIndex, savedLanguage, AppFontSizeLevel, TerminalFontSizeLevel, DefaultShellType);

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
