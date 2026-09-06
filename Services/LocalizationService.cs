using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using MultiShell.Models;

namespace MultiShell.Services;

/// <summary>
/// Implementation of <see cref="ILocalizationService"/> supporting DE, EN, FR, ES with system language detection and persistence.
/// </summary>
public partial class LocalizationService : ILocalizationService
{
    private string _currentLanguage = "en";
    private bool _isCustomLanguageSelected;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? LanguageChanged;

    public static readonly IReadOnlyList<LanguageOption> AllSupportedLanguages = new List<LanguageOption>
    {
        new("de", "Deutsch", "German"),
        new("en", "English", "English"),
        new("fr", "Français", "French"),
        new("es", "Español", "Spanish")
    };

    public IReadOnlyList<LanguageOption> SupportedLanguages => AllSupportedLanguages;

    public bool IsGerman => string.Equals(_currentLanguage, "de", StringComparison.OrdinalIgnoreCase);

    public LocalizationService()
    {
        _currentLanguage = DetectSystemLanguage();
    }

    public LocalizationService(string initialLanguage, bool isUserSelection = false)
    {
        _isCustomLanguageSelected = isUserSelection;
        _currentLanguage = NormalizeLanguage(initialLanguage);
    }

    public static string DetectSystemLanguage()
    {
        var sysCode = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        return sysCode switch
        {
            "de" => "de",
            "fr" => "fr",
            "es" => "es",
            _ => "en" // Standard Fallback is English
        };
    }

    private static string NormalizeLanguage(string cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode)) return "en";
        var norm = cultureCode.Trim().ToLowerInvariant();
        if (norm.StartsWith("de")) return "de";
        if (norm.StartsWith("fr")) return "fr";
        if (norm.StartsWith("es")) return "es";
        return "en";
    }

    public string CurrentLanguage
    {
        get => _currentLanguage;
        private set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsGerman)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCustomLanguageSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                LanguageChanged?.Invoke(_currentLanguage);
            }
        }
    }

    public bool IsCustomLanguageSelected => _isCustomLanguageSelected;

    public string this[string key]
    {
        get
        {
            var dict = _currentLanguage switch
            {
                "de" => GermanStrings,
                "fr" => FrenchStrings,
                "es" => SpanishStrings,
                _ => EnglishStrings
            };

            if (dict.TryGetValue(key, out var val))
            {
                return val;
            }

            // Fallback chain: English -> German -> French -> Spanish -> key itself
            if (EnglishStrings.TryGetValue(key, out var enVal)) return enVal;
            if (GermanStrings.TryGetValue(key, out var deVal)) return deVal;
            if (FrenchStrings.TryGetValue(key, out var frVal)) return frVal;
            if (SpanishStrings.TryGetValue(key, out var esVal)) return esVal;

            return key;
        }
    }

    public void SetLanguage(string cultureCode, bool isUserSelection = true)
    {
        if (string.IsNullOrWhiteSpace(cultureCode)) return;

        if (isUserSelection)
        {
            _isCustomLanguageSelected = true;
        }

        CurrentLanguage = NormalizeLanguage(cultureCode);
    }

    public void ToggleLanguage()
    {
        var currentIndex = 0;
        for (var i = 0; i < AllSupportedLanguages.Count; i++)
        {
            if (string.Equals(AllSupportedLanguages[i].Code, _currentLanguage, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }

        var nextIndex = (currentIndex + 1) % AllSupportedLanguages.Count;
        SetLanguage(AllSupportedLanguages[nextIndex].Code, isUserSelection: true);
    }
}
