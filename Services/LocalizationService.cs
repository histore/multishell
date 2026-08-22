using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using MultiShell.Models;

namespace MultiShell.Services;

/// <summary>
/// Implementation of <see cref="ILocalizationService"/> supporting DE, EN, FR, ES with system language detection and persistence.
/// </summary>
public class LocalizationService : ILocalizationService
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

    private static readonly Dictionary<string, string> GermanStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Settings Menu
        ["Settings_Menu_Title"] = "Einstellungen & Über",
        ["Settings_App_Theme"] = "App-Design",
        ["Settings_Terminal_Theme"] = "Terminal-Design",
        ["Settings_Language"] = "Sprache",
        ["Settings_Help_Title"] = "Hilfe & Tastaturkürzel",
        ["Settings_About_Title"] = "Über MultiShell",
        ["Theme_Dark"] = "Dunkel",
        ["Theme_Light"] = "Hell",
        ["Lang_German"] = "Deutsch",
        ["Lang_English"] = "English",
        ["Lang_French"] = "Français",
        ["Lang_Spanish"] = "Español",

        // Toolbar & Tabs
        ["Btn_New_Tab_Tooltip"] = "Neuer Tab (Strg+Umschalt+T)",
        ["Btn_Tab_History_Tooltip"] = "Tab-Verlauf (Strg+Umschalt+H)",
        ["Btn_Tab_History_Text"] = "Verlauf",
        ["Btn_Tab_Menu_Tooltip"] = "Alle geöffneten Tabs",
        ["Btn_Tab_Scroll_Left"] = "Tabs nach links scrollen",
        ["Btn_Tab_Scroll_Right"] = "Tabs nach rechts scrollen",
        ["Btn_Tab_Close_Tooltip"] = "Tab schließen",

        // Drawer
        ["Drawer_Title"] = "VERLAUF",
        ["Drawer_Tab_Commands"] = "Befehle",
        ["Drawer_Tab_Directories"] = "Verzeichnisse",
        ["Drawer_Search_Commands_Placeholder"] = "Befehle filtern...",
        ["Drawer_Search_Directories_Placeholder"] = "Verzeichnisse filtern...",
        ["Drawer_Empty_Commands"] = "In diesem Tab wurden noch keine Befehle ausgeführt.",
        ["Drawer_Empty_Directories"] = "In diesem Tab wurden noch keine Verzeichnisse erfasst.",
        ["Drawer_Hint"] = "[ ▲/▼ Navigieren  |  Enter Ausführen  |  Esc Schließen ]",

        // Help Modal
        ["Help_Modal_Title"] = "Tastaturkürzel & Bedienung",
        ["Help_Section_Navigation"] = "TASTATURKÜRZEL",
        ["Help_Section_Tabs"] = "TAB-VERWALTUNG",
        ["Help_Section_History"] = "VERLAUFS-DRAWER",
        ["Help_Section_Features"] = "FUNKTIONEN",
        ["Help_Close_Button"] = "Schließen",
        ["Help_Tab_New"] = "Neuen PowerShell-Tab öffnen",
        ["Help_Tab_Dup"] = "Aktiven Tab im aktuellen Verzeichnis duplizieren",
        ["Help_Hist_Toggle"] = "Verlaufs-Overlay öffnen/schließen (▲/▼, Enter)",
        ["Help_Nav_F1"] = "Diesen Hilfe-Dialog öffnen",
        ["Help_Nav_Esc"] = "Aktives Overlay/Dialog schließen und Terminal fokussieren",
        ["Help_Feature_1"] = "• Tab-Verlauf: Linke Kante berühren oder Strg+Umschalt+H drücken. Mit ▲/▼ navigieren und mit Enter ausführen.",
        ["Help_Feature_2"] = "• Drag & Drop: Tabs mit der linken Maustaste per Ziehen neu anordnen.",
        ["Help_Feature_3"] = "• Tab-Überlauf: Scrollbuttons (‹ ›) und Schnellmenü (≡ ▾) bei vielen Tabs.",
        ["Help_Feature_4"] = "• Persistenz: Alle Tabs, Pfade und Verläufe werden automatisch gespeichert.",

        // About Modal
        ["About_Modal_Title"] = "MultiShell",
        ["About_Modal_Subtitle"] = "High-Performance PowerShell Workspace",
        ["About_Version"] = "Version:",
        ["About_Framework"] = "Framework:",
        ["About_UI_Terminal"] = "UI & Terminal:",
        ["About_License"] = "Lizenz:",
        ["About_Built_With"] = "Entwickelt mit:",
        ["About_Built_With_Value"] = "Google Gemini & Antigravity",
        ["About_Footer"] = "Entwickelt nach den Prinzipien von Clean Architecture & Clean Code.",
        ["About_OK_Button"] = "OK"
    };

    private static readonly Dictionary<string, string> EnglishStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Settings Menu
        ["Settings_Menu_Title"] = "Settings & About",
        ["Settings_App_Theme"] = "App UI Theme",
        ["Settings_Terminal_Theme"] = "Terminal Theme",
        ["Settings_Language"] = "Language",
        ["Settings_Help_Title"] = "Help & Shortcuts",
        ["Settings_About_Title"] = "About MultiShell",
        ["Theme_Dark"] = "Dark",
        ["Theme_Light"] = "Light",
        ["Lang_German"] = "Deutsch",
        ["Lang_English"] = "English",
        ["Lang_French"] = "Français",
        ["Lang_Spanish"] = "Español",

        // Toolbar & Tabs
        ["Btn_New_Tab_Tooltip"] = "New Tab (Ctrl+Shift+T)",
        ["Btn_Tab_History_Tooltip"] = "Tab History (Ctrl+Shift+H)",
        ["Btn_Tab_History_Text"] = "History",
        ["Btn_Tab_Menu_Tooltip"] = "All Open Tabs",
        ["Btn_Tab_Scroll_Left"] = "Scroll Tabs Left",
        ["Btn_Tab_Scroll_Right"] = "Scroll Tabs Right",
        ["Btn_Tab_Close_Tooltip"] = "Close Tab",

        // Drawer
        ["Drawer_Title"] = "HISTORY",
        ["Drawer_Tab_Commands"] = "Commands",
        ["Drawer_Tab_Directories"] = "Directories",
        ["Drawer_Search_Commands_Placeholder"] = "Filter commands...",
        ["Drawer_Search_Directories_Placeholder"] = "Filter directories...",
        ["Drawer_Empty_Commands"] = "No commands executed in this tab yet.",
        ["Drawer_Empty_Directories"] = "No directories recorded in this tab yet.",
        ["Drawer_Hint"] = "[ ▲/▼ Navigate  |  Enter Execute  |  Esc Close ]",

        // Help Modal
        ["Help_Modal_Title"] = "Keyboard Shortcuts & Usage",
        ["Help_Section_Navigation"] = "SHORTCUTS",
        ["Help_Section_Tabs"] = "TAB MANAGEMENT",
        ["Help_Section_History"] = "HISTORY OVERLAY",
        ["Help_Section_Features"] = "FEATURES",
        ["Help_Close_Button"] = "Close",
        ["Help_Tab_New"] = "Open new PowerShell tab",
        ["Help_Tab_Dup"] = "Duplicate active tab in current directory",
        ["Help_Hist_Toggle"] = "Toggle History Overlay (Navigate with ▲/▼, Enter)",
        ["Help_Nav_F1"] = "Open this Help dialog",
        ["Help_Nav_Esc"] = "Close active overlay / dialog and focus terminal",
        ["Help_Feature_1"] = "• Tab History: Hover left edge or press Ctrl+Shift+H. Navigate with ▲/▼ and press Enter to execute.",
        ["Help_Feature_2"] = "• Drag & Drop: Reorder tabs by dragging with left mouse button.",
        ["Help_Feature_3"] = "• Tab Overflow: Scroll buttons (‹ ›) and quick menu (≡ ▾) appear when tab count exceeds window.",
        ["Help_Feature_4"] = "• Persistence: All tabs, working directories, and histories persist automatically across restarts.",

        // About Modal
        ["About_Modal_Title"] = "MultiShell",
        ["About_Modal_Subtitle"] = "High Performance PowerShell Workspace",
        ["About_Version"] = "Version:",
        ["About_Framework"] = "Framework:",
        ["About_UI_Terminal"] = "UI & Terminal:",
        ["About_License"] = "License:",
        ["About_Built_With"] = "Built With:",
        ["About_Built_With_Value"] = "Google Gemini & Antigravity",
        ["About_Footer"] = "Crafted with Clean Architecture & Clean Code principles.",
        ["About_OK_Button"] = "OK"
    };

    private static readonly Dictionary<string, string> FrenchStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Settings Menu
        ["Settings_Menu_Title"] = "Paramètres & À propos",
        ["Settings_App_Theme"] = "Thème de l'application",
        ["Settings_Terminal_Theme"] = "Thème du terminal",
        ["Settings_Language"] = "Langue",
        ["Settings_Help_Title"] = "Aide & Raccourcis",
        ["Settings_About_Title"] = "À propos de MultiShell",
        ["Theme_Dark"] = "Sombre",
        ["Theme_Light"] = "Clair",
        ["Lang_German"] = "Deutsch",
        ["Lang_English"] = "English",
        ["Lang_French"] = "Français",
        ["Lang_Spanish"] = "Español",

        // Toolbar & Tabs
        ["Btn_New_Tab_Tooltip"] = "Nouvel onglet (Ctrl+Maj+T)",
        ["Btn_Tab_History_Tooltip"] = "Historique des onglets (Ctrl+Maj+H)",
        ["Btn_Tab_History_Text"] = "Historique",
        ["Btn_Tab_Menu_Tooltip"] = "Tous les onglets ouverts",
        ["Btn_Tab_Scroll_Left"] = "Faire défiler les onglets à gauche",
        ["Btn_Tab_Scroll_Right"] = "Faire défiler les onglets à droite",
        ["Btn_Tab_Close_Tooltip"] = "Fermer l'onglet",

        // Drawer
        ["Drawer_Title"] = "HISTORIQUE",
        ["Drawer_Tab_Commands"] = "Commandes",
        ["Drawer_Tab_Directories"] = "Répertoires",
        ["Drawer_Search_Commands_Placeholder"] = "Filtrer les commandes...",
        ["Drawer_Search_Directories_Placeholder"] = "Filtrer les répertoires...",
        ["Drawer_Empty_Commands"] = "Aucune commande exécutée dans cet onglet.",
        ["Drawer_Empty_Directories"] = "Aucun répertoire enregistré dans cet onglet.",
        ["Drawer_Hint"] = "[ ▲/▼ Naviguer  |  Entrée Exécuter  |  Échap Fermer ]",

        // Help Modal
        ["Help_Modal_Title"] = "Raccourcis clavier & Utilisation",
        ["Help_Section_Navigation"] = "RACCOURCIS",
        ["Help_Section_Tabs"] = "GESTION DES ONGLETS",
        ["Help_Section_History"] = "VOLET HISTORIQUE",
        ["Help_Section_Features"] = "FONCTIONNALITÉS",
        ["Help_Close_Button"] = "Fermer",
        ["Help_Tab_New"] = "Ouvrir un nouvel onglet PowerShell",
        ["Help_Tab_Dup"] = "Dupliquer l'onglet actif dans le répertoire actuel",
        ["Help_Hist_Toggle"] = "Afficher/masquer le volet historique (▲/▼, Entrée)",
        ["Help_Nav_F1"] = "Ouvrir cette boîte de dialogue d'aide",
        ["Help_Nav_Esc"] = "Fermer le volet ou la boîte de dialogue et focaliser le terminal",
        ["Help_Feature_1"] = "• Historique : Survoler le bord gauche ou appuyer sur Ctrl+Maj+H. Naviguer avec ▲/▼ et Entrée pour exécuter.",
        ["Help_Feature_2"] = "• Glisser-déposer : Réorganiser les onglets par glisser avec le bouton gauche.",
        ["Help_Feature_3"] = "• Débordement : Boutons de défilement (‹ ›) et menu rapide (≡ ▾).",
        ["Help_Feature_4"] = "• Persistance : Tous les onglets, répertoires et historiques sont conservés.",

        // About Modal
        ["About_Modal_Title"] = "MultiShell",
        ["About_Modal_Subtitle"] = "Espace de travail PowerShell haute performance",
        ["About_Version"] = "Version :",
        ["About_Framework"] = "Framework :",
        ["About_UI_Terminal"] = "UI & Terminal :",
        ["About_License"] = "Licence :",
        ["About_Built_With"] = "Développé avec :",
        ["About_Built_With_Value"] = "Google Gemini & Antigravity",
        ["About_Footer"] = "Conçu selon les principes de Clean Architecture & Clean Code.",
        ["About_OK_Button"] = "OK"
    };

    private static readonly Dictionary<string, string> SpanishStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Settings Menu
        ["Settings_Menu_Title"] = "Configuración & Acerca de",
        ["Settings_App_Theme"] = "Tema de la aplicación",
        ["Settings_Terminal_Theme"] = "Tema del terminal",
        ["Settings_Language"] = "Idioma",
        ["Settings_Help_Title"] = "Ayuda & Atajos",
        ["Settings_About_Title"] = "Acerca de MultiShell",
        ["Theme_Dark"] = "Oscuro",
        ["Theme_Light"] = "Claro",
        ["Lang_German"] = "Deutsch",
        ["Lang_English"] = "English",
        ["Lang_French"] = "Français",
        ["Lang_Spanish"] = "Español",

        // Toolbar & Tabs
        ["Btn_New_Tab_Tooltip"] = "Nueva pestaña (Ctrl+Mayús+T)",
        ["Btn_Tab_History_Tooltip"] = "Historial de pestañas (Ctrl+Mayús+H)",
        ["Btn_Tab_History_Text"] = "Historial",
        ["Btn_Tab_Menu_Tooltip"] = "Todas las pestañas abiertas",
        ["Btn_Tab_Scroll_Left"] = "Desplazar pestañas a la izquierda",
        ["Btn_Tab_Scroll_Right"] = "Desplazar pestañas a la derecha",
        ["Btn_Tab_Close_Tooltip"] = "Cerrar pestaña",

        // Drawer
        ["Drawer_Title"] = "HISTORIAL",
        ["Drawer_Tab_Commands"] = "Comandos",
        ["Drawer_Tab_Directories"] = "Directorios",
        ["Drawer_Search_Commands_Placeholder"] = "Filtrar comandos...",
        ["Drawer_Search_Directories_Placeholder"] = "Filtrar directorios...",
        ["Drawer_Empty_Commands"] = "Aún no se han ejecutado comandos en esta pestaña.",
        ["Drawer_Empty_Directories"] = "Aún no se han registrado directorios en esta pestaña.",
        ["Drawer_Hint"] = "[ ▲/▼ Navegar  |  Enter Ejecutar  |  Esc Cerrar ]",

        // Help Modal
        ["Help_Modal_Title"] = "Atajos de teclado & Uso",
        ["Help_Section_Navigation"] = "ATAJOS",
        ["Help_Section_Tabs"] = "GESTIÓN DE PESTAÑAS",
        ["Help_Section_History"] = "PANEL DE HISTORIAL",
        ["Help_Section_Features"] = "CARACTERÍSTICAS",
        ["Help_Close_Button"] = "Cerrar",
        ["Help_Tab_New"] = "Abrir una nueva pestaña PowerShell",
        ["Help_Tab_Dup"] = "Duplicar la pestaña activa en el directorio actual",
        ["Help_Hist_Toggle"] = "Alternar panel de historial (Navegar con ▲/▼, Enter)",
        ["Help_Nav_F1"] = "Abrir este diálogo de ayuda",
        ["Help_Nav_Esc"] = "Cerrar el diálogo activo y enfocar el terminal",
        ["Help_Feature_1"] = "• Historial: Pase el ratón por el borde izquierdo o Ctrl+Mayús+H. Navegue con ▲/▼ y Enter para ejecutar.",
        ["Help_Feature_2"] = "• Arrastrar y soltar: Reordene las pestañas arrastrándolas con el ratón.",
        ["Help_Feature_3"] = "• Desbordamiento: Botones de desplazamiento (‹ ›) y menú rápido (≡ ▾).",
        ["Help_Feature_4"] = "• Persistencia: Todas las pestañas, directorios e historiales se guardan.",

        // About Modal
        ["About_Modal_Title"] = "MultiShell",
        ["About_Modal_Subtitle"] = "Espacio de trabajo PowerShell de alto rendimiento",
        ["About_Version"] = "Versión:",
        ["About_Framework"] = "Framework:",
        ["About_UI_Terminal"] = "UI & Terminal:",
        ["About_License"] = "Licencia:",
        ["About_Built_With"] = "Creado con:",
        ["About_Built_With_Value"] = "Google Gemini & Antigravity",
        ["About_Footer"] = "Diseñado con los principios de Clean Architecture & Clean Code.",
        ["About_OK_Button"] = "OK"
    };

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
