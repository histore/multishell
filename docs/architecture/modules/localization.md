# Internationalization & Localization (i18n / l10n)

## 1. Overview & Purpose
MultiShell mandates **0% hardcoded user-facing strings** across all XAML views, dialogs, status messages, and menus. The Localization subsystem delivers dynamic, zero-restart runtime language switching, automatic OS language detection, and typed resource resolution.

## 2. Models & Contracts

### 2.1 Models (`Models/LanguageOption.cs`)
* **`LanguageOption`**:
  * Immutable record containing:
    * `Code`: ISO 639-1 two-letter code (`en`, `de`, `fr`, `es`).
    * `NativeName`: Endonym display text (e.g. "Deutsch", "English").
    * `EnglishName`: Exonym display text.

### 2.2 Contracts (`Services/ILocalizationService.cs`)
* **`CurrentLanguage`**: Active `LanguageOption`.
* **`AvailableLanguages`**: Read-only collection of supported languages.
* **`SetLanguage(string languageCode)`**: Switches application language dynamically.
* **`this[string key]`**: Indexer returning localized text for the requested key.
* **`LanguageChanged`**: Event or `INotifyPropertyChanged` notification triggering UI binding re-evaluations.

## 3. Implementation Architecture (`Services/LocalizationService.cs`)

### 3.1 Resource Dictionary Management
* Localized strings are organized into bilingual / multilingual dictionaries in `LocalizationService.Dictionaries.cs`.
* Primary supported languages:
  * **English (`en`)**: Reference and fallback locale.
  * **German (`de`)**: Fully localized.
  * **French (`fr`)**: Supported locale.
  * **Spanish (`es`)**: Supported locale.

### 3.2 Dynamic Runtime Language Switching
* When `SetLanguage` is invoked:
  1. `CurrentLanguage` is updated.
  2. Indexer property changed notification (`PropertyChanged("Item[]")`) is raised.
  3. All Avalonia compiled bindings bound to `Localization[Key]` immediately refresh their rendered visual text without window reloads or application restarts.
  4. The selected language code is stored in local app configuration.

### 3.3 Fallback Mechanism
If a requested translation key is missing in the active language dictionary:
1. The service checks the English (`en`) dictionary.
2. If absent in English, it returns the key name itself enclosed in brackets (e.g. `[MissingKey]`) to make localization gaps immediately apparent during development.
