using System;
using System.Collections.Generic;
using System.ComponentModel;
using MultiShell.Models;

namespace MultiShell.Services;

/// <summary>
/// Service contract for managing application localization and dynamic UI language switching.
/// </summary>
public interface ILocalizationService : INotifyPropertyChanged
{
    /// <summary>
    /// Gets the current active language code ("de", "en", "fr", "es").
    /// </summary>
    string CurrentLanguage { get; }

    /// <summary>
    /// Gets a value indicating whether German is active.
    /// </summary>
    bool IsGerman { get; }

    /// <summary>
    /// Gets a value indicating whether a custom language was explicitly selected by the user.
    /// </summary>
    bool IsCustomLanguageSelected { get; }

    /// <summary>
    /// Gets the list of all supported language options.
    /// </summary>
    IReadOnlyList<LanguageOption> SupportedLanguages { get; }

    /// <summary>
    /// Gets the localized string for the specified translation key.
    /// </summary>
    string this[string key] { get; }

    /// <summary>
    /// Sets the application language.
    /// </summary>
    /// <param name="cultureCode">The culture code, e.g. "de", "en", "fr", "es".</param>
    /// <param name="isUserSelection">Whether this change was explicitly chosen by the user.</param>
    void SetLanguage(string cultureCode, bool isUserSelection = true);

    /// <summary>
    /// Cycles through the supported languages.
    /// </summary>
    void ToggleLanguage();

    /// <summary>
    /// Event triggered when the active language changes.
    /// </summary>
    event Action<string>? LanguageChanged;
}