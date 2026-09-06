using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MultiShell.ViewModels;

public partial class MainViewModel
{
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
        OnPropertyChanged(nameof(TabSwitcherCountText));
        OnPropertyChanged(nameof(TabSwitcherHintText));
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

    [RelayCommand]
    public void ResetFontSizeLevels()
    {
        _fontSizeService.ResetLevels();
    }

    [RelayCommand]
    public void ResetTerminalFontSize()
    {
        _fontSizeService.ResetTerminalFontSizeLevel();
    }
}
