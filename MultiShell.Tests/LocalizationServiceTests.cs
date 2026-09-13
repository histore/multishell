using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public class LocalizationServiceTests
{
    [Fact]
    public void LocalizationService_SupportedLanguages_ContainsSixLanguages()
    {
        // Arrange & Act
        var service = new LocalizationService();

        // Assert
        Assert.Equal(6, service.SupportedLanguages.Count);
        Assert.Contains(service.SupportedLanguages, l => l.Code == "de");
        Assert.Contains(service.SupportedLanguages, l => l.Code == "en");
        Assert.Contains(service.SupportedLanguages, l => l.Code == "fr");
        Assert.Contains(service.SupportedLanguages, l => l.Code == "es");
        Assert.Contains(service.SupportedLanguages, l => l.Code == "it");
        Assert.Contains(service.SupportedLanguages, l => l.Code == "pt");
    }

    [Fact]
    public void LocalizationService_SetLanguage_SwitchesToFrench()
    {
        // Arrange
        var service = new LocalizationService();

        // Act
        service.SetLanguage("fr");

        // Assert
        Assert.Equal("fr", service.CurrentLanguage);
        Assert.Equal("Paramètres & À propos", service["Settings_Menu_Title"]);
        Assert.Equal("Thème de l'application", service["Settings_App_Theme"]);
        Assert.True(service.IsCustomLanguageSelected);
    }

    [Fact]
    public void LocalizationService_SetLanguage_SwitchesToSpanish()
    {
        // Arrange
        var service = new LocalizationService();

        // Act
        service.SetLanguage("es");

        // Assert
        Assert.Equal("es", service.CurrentLanguage);
        Assert.Equal("Configuración & Acerca de", service["Settings_Menu_Title"]);
        Assert.Equal("Tema de la aplicación", service["Settings_App_Theme"]);
        Assert.True(service.IsCustomLanguageSelected);
    }

    [Fact]
    public void LocalizationService_SetLanguage_SwitchesToItalian()
    {
        // Arrange
        var service = new LocalizationService();

        // Act
        service.SetLanguage("it");

        // Assert
        Assert.Equal("it", service.CurrentLanguage);
        Assert.Equal("Impostazioni & Informazioni", service["Settings_Menu_Title"]);
        Assert.Equal("Tema applicazione", service["Settings_App_Theme"]);
        Assert.True(service.IsCustomLanguageSelected);
    }

    [Fact]
    public void LocalizationService_SetLanguage_SwitchesToPortuguese()
    {
        // Arrange
        var service = new LocalizationService();

        // Act
        service.SetLanguage("pt");

        // Assert
        Assert.Equal("pt", service.CurrentLanguage);
        Assert.Equal("Definições & Sobre", service["Settings_Menu_Title"]);
        Assert.Equal("Tema da aplicação", service["Settings_App_Theme"]);
        Assert.True(service.IsCustomLanguageSelected);
    }

    [Fact]
    public void LocalizationService_ToggleLanguage_CyclesThroughAllLanguages()
    {
        // Arrange
        var service = new LocalizationService("de");

        // Act & Assert
        Assert.Equal("de", service.CurrentLanguage);

        service.ToggleLanguage();
        Assert.Equal("en", service.CurrentLanguage);

        service.ToggleLanguage();
        Assert.Equal("fr", service.CurrentLanguage);

        service.ToggleLanguage();
        Assert.Equal("es", service.CurrentLanguage);

        service.ToggleLanguage();
        Assert.Equal("it", service.CurrentLanguage);

        service.ToggleLanguage();
        Assert.Equal("pt", service.CurrentLanguage);

        service.ToggleLanguage();
        Assert.Equal("de", service.CurrentLanguage);
    }

    [Fact]
    public void LocalizationService_DetectSystemLanguage_ReturnsValidCode()
    {
        // Act
        var code = LocalizationService.DetectSystemLanguage();

        // Assert
        Assert.True(code is "de" or "en" or "fr" or "es" or "it" or "pt");
    }

    [Fact]
    public void LocalizationService_ReturnsFallbackOrKeyWhenMissing()
    {
        // Arrange
        var service = new LocalizationService();

        // Act & Assert
        Assert.Equal("NonExistentKey123", service["NonExistentKey123"]);
    }

    [Fact]
    public async Task TabStatePersistenceService_SavesAndLoadsSavedLanguage()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"multishell_loc_test_{Path.GetRandomFileName()}.json");
        var persistence = new TabStatePersistenceService(tempFile);

        try
        {
            var state = new WorkspaceState(new List<TabState>(), 0, SavedLanguage: "fr");

            // Act
            await persistence.SaveStateAsync(state);
            var loaded = await persistence.LoadStateAsync();

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal("fr", loaded.SavedLanguage);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void MainViewModel_SelectLanguageCommand_UpdatesAndSavesLanguage()
    {
        // Arrange
        var locService = new LocalizationService("de");
        var tempFile = Path.Combine(Path.GetTempPath(), $"multishell_vm_test_{Path.GetRandomFileName()}.json");
        var persistence = new TabStatePersistenceService(tempFile);

        try
        {
            var vm = new MainViewModel(
                new ShellProcessService(),
                persistence,
                new ThemeService(),
                locService,
                new FontSizeService());

            // Act
            vm.SelectLanguageCommand.Execute("es");

            // Assert
            Assert.Equal("es", vm.Loc.CurrentLanguage);
            Assert.Equal("Configuración & Acerca de", vm.Loc["Settings_Menu_Title"]);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Theory]
    [InlineData("de", "App-Schriftgröße", "Terminal-Schriftgröße", "Standard (3)")]
    [InlineData("en", "App Font Size", "Terminal Font Size", "Standard (3)")]
    [InlineData("fr", "Taille de police de l'app", "Taille de police du terminal", "Standard (3)")]
    [InlineData("es", "Tamaño de fuente de la app", "Tamaño de fuente del terminal", "Estándar (3)")]
    [InlineData("it", "Dimensione carattere app", "Dimensione carattere terminale", "Standard (3)")]
    [InlineData("pt", "Tamanho da fonte da app", "Tamanho da fonte do terminal", "Padrão (3)")]
    public void LocalizationService_FontSizeKeys_AreFullyTranslated(string lang, string expectedAppKey, string expectedTermKey, string expectedLvl3)
    {
        // Arrange
        var service = new LocalizationService(lang);

        // Act & Assert
        Assert.Equal(expectedAppKey, service["Settings_App_FontSize"]);
        Assert.Equal(expectedTermKey, service["Settings_Terminal_FontSize"]);
        Assert.Equal(expectedLvl3, service["FontSize_Level_3"]);
        Assert.False(string.IsNullOrWhiteSpace(service["FontSize_Level_1"]));
        Assert.False(string.IsNullOrWhiteSpace(service["FontSize_Level_5"]));
    }

    [Theory]
    [InlineData("de", "GitHub-Repository:")]
    [InlineData("en", "GitHub Repository:")]
    [InlineData("fr", "Dépôt GitHub :")]
    [InlineData("es", "Repositorio GitHub:")]
    [InlineData("it", "Repository GitHub:")]
    [InlineData("pt", "Repositório GitHub:")]
    public void LocalizationService_AboutGitHubKey_IsFullyTranslated(string lang, string expectedText)
    {
        // Arrange
        var service = new LocalizationService(lang);

        // Act & Assert
        Assert.Equal(expectedText, service["About_GitHub"]);
    }

    [Fact]
    public void MainViewModel_SelectedLanguage_UpdatesCurrentLanguageAndViceVersa()
    {
        // Arrange
        using var vm = new MainViewModel();

        // Act - Set to Italian via SelectedLanguage
        var italianOption = vm.AvailableLanguages.First(l => l.Code == "it");
        vm.SelectedLanguage = italianOption;

        // Assert
        Assert.Equal("it", vm.CurrentLanguage);
        Assert.Equal("it", vm.Loc.CurrentLanguage);
        Assert.Equal(italianOption, vm.SelectedLanguage);
        Assert.True(vm.IsItalian);
        Assert.False(vm.IsGerman);

        // Act - Set to Portuguese via SelectLanguage
        vm.SelectLanguage("pt");

        // Assert
        Assert.Equal("pt", vm.CurrentLanguage);
        Assert.Equal("pt", vm.SelectedLanguage.Code);
        Assert.True(vm.IsPortuguese);
    }
}
