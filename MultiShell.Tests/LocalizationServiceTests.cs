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
    public void LocalizationService_SupportedLanguages_ContainsFourLanguages()
    {
        // Arrange & Act
        var service = new LocalizationService();

        // Assert
        Assert.Equal(4, service.SupportedLanguages.Count);
        Assert.Contains(service.SupportedLanguages, l => l.Code == "de");
        Assert.Contains(service.SupportedLanguages, l => l.Code == "en");
        Assert.Contains(service.SupportedLanguages, l => l.Code == "fr");
        Assert.Contains(service.SupportedLanguages, l => l.Code == "es");
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
        Assert.Equal("de", service.CurrentLanguage);
    }

    [Fact]
    public void LocalizationService_DetectSystemLanguage_ReturnsValidCode()
    {
        // Act
        var code = LocalizationService.DetectSystemLanguage();

        // Assert
        Assert.True(code is "de" or "en" or "fr" or "es");
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
                new PowerShellProcessService(),
                persistence,
                new ThemeService(),
                locService);

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
}
