using System;
using System.IO;
using System.Threading.Tasks;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public class MainViewModelTests
{
    [Fact]
    public void AppVersion_IsNotNullOrEmpty_AndStartsWithV()
    {
        // Arrange & Act
        using var vm = new MainViewModel();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(vm.AppVersion));
        Assert.StartsWith("v", vm.AppVersion);
    }

    [Fact]
    public void GitHubUrl_IsConfiguredCorrectly()
    {
        // Arrange & Act
        using var vm = new MainViewModel();

        // Assert
        Assert.Equal("https://github.com/histore/multishell", vm.GitHubUrl);
        Assert.NotNull(vm.OpenGitHubUrlCommand);
    }

    [Fact]
    public async Task WindowTitle_ReflectsSelectedTabPathWithoutEllipsis_WhenPathIsNotExcessivelyLong()
    {
        // Arrange
        using var vm = new MainViewModel();
        await vm.InitializeWorkspaceAsync();

        // Assert - Title starts with MultiShell - and contains untruncated path when reasonable length
        Assert.StartsWith("MultiShell - ", vm.WindowTitle);
        Assert.NotNull(vm.SelectedTab);
        if (!string.IsNullOrWhiteSpace(vm.SelectedTab.WorkingDirectory) && vm.SelectedTab.WorkingDirectory.Length <= 65)
        {
            Assert.Equal($"MultiShell - {vm.SelectedTab.WorkingDirectory}", vm.WindowTitle);
        }
    }

    private class MockThemeService : IThemeService
    {
        public bool IsDarkAppTheme { get; private set; } = true;
        public bool IsDarkTerminalTheme { get; private set; } = true;

        public void SetAppTheme(bool isDark) => IsDarkAppTheme = isDark;
        public void SetTerminalTheme(bool isDark) => IsDarkTerminalTheme = isDark;
        public void ToggleAppTheme() => IsDarkAppTheme = !IsDarkAppTheme;
        public void ToggleTerminalTheme() => IsDarkTerminalTheme = !IsDarkTerminalTheme;
    }

    [Fact]
    public void ToggleAppTheme_SwitchesAppThemeIndependently()
    {
        // Arrange
        var mockTheme = new MockThemeService();
        using var vm = new MainViewModel(new ShellProcessService(), new TabStatePersistenceService(), mockTheme);

        Assert.True(vm.IsDarkAppTheme);
        Assert.True(vm.IsDarkTerminalTheme);

        // Act
        vm.ToggleAppThemeCommand.Execute(null);

        // Assert: App theme is light, but Terminal remains dark
        Assert.False(vm.IsDarkAppTheme);
        Assert.False(mockTheme.IsDarkAppTheme);
        Assert.True(vm.IsDarkTerminalTheme);
        Assert.True(mockTheme.IsDarkTerminalTheme);
    }

    [Fact]
    public void ToggleTerminalTheme_SwitchesTerminalThemeIndependently()
    {
        // Arrange
        var mockTheme = new MockThemeService();
        using var vm = new MainViewModel(new ShellProcessService(), new TabStatePersistenceService(), mockTheme);

        // Act
        vm.ToggleTerminalThemeCommand.Execute(null);

        // Assert: Terminal is light, but App UI remains dark
        Assert.True(vm.IsDarkAppTheme);
        Assert.True(mockTheme.IsDarkAppTheme);
        Assert.False(vm.IsDarkTerminalTheme);
        Assert.False(mockTheme.IsDarkTerminalTheme);
    }

    [Fact]
    public void SetThemes_UpdatesStatesExplicitly()
    {
        // Arrange
        var mockTheme = new MockThemeService();
        using var vm = new MainViewModel(new ShellProcessService(), new TabStatePersistenceService(), mockTheme);

        // Act
        vm.SetAppTheme(false);
        vm.SetTerminalTheme(false);

        // Assert
        Assert.False(vm.IsDarkAppTheme);
        Assert.False(vm.IsDarkTerminalTheme);

        // Act
        vm.SetAppTheme(true);
        vm.SetTerminalTheme(true);

        // Assert
        Assert.True(vm.IsDarkAppTheme);
        Assert.True(vm.IsDarkTerminalTheme);
    }

    [Fact]
    public void SetFontSizeLevels_UpdatesValuesAndScales()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"multishell_state_test_{Guid.NewGuid():N}.json");
        try
        {
            var fontSizeService = new FontSizeService();
            using var vm = new MainViewModel(
                new ShellProcessService(),
                new TabStatePersistenceService(tempFile),
                new MockThemeService(),
                new LocalizationService(),
                fontSizeService);

            Assert.Equal(3, vm.AppFontSizeLevel);
            Assert.Equal(3, vm.TerminalFontSizeLevel);
            Assert.Equal(1.0, vm.AppFontScale);
            Assert.Equal(12.0, vm.TerminalFontSize);

            // Act - Set via command
            vm.SetAppFontSizeLevelCommand.Execute(5);
            vm.SetTerminalFontSizeLevelCommand.Execute(1);

            // Assert
            Assert.Equal(5, vm.AppFontSizeLevel);
            Assert.Equal(1.25, vm.AppFontScale);
            Assert.Equal(1, vm.TerminalFontSizeLevel);
            Assert.Equal(9.5, vm.TerminalFontSize);
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
    public void IncreaseAndDecreaseFontSize_UpdatesLevelsWithinRange()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"multishell_state_test_{Guid.NewGuid():N}.json");
        try
        {
            var fontSizeService = new FontSizeService();
            using var vm = new MainViewModel(
                new ShellProcessService(),
                new TabStatePersistenceService(tempFile),
                new MockThemeService(),
                new LocalizationService(),
                fontSizeService);

            // Act: Increase
            vm.IncreaseAppFontSizeCommand.Execute(null);
            vm.IncreaseTerminalFontSizeCommand.Execute(null);

            // Assert
            Assert.Equal(4, vm.AppFontSizeLevel);
            Assert.Equal(4, vm.TerminalFontSizeLevel);

            // Act: Decrease
            vm.DecreaseAppFontSizeCommand.Execute(null);
            vm.DecreaseAppFontSizeCommand.Execute(null);

            // Assert
            Assert.Equal(2, vm.AppFontSizeLevel);
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
    public void ResetFontSizeLevels_ResetsBothLevelsToDefault()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"multishell_state_test_{Guid.NewGuid():N}.json");
        try
        {
            var fontSizeService = new FontSizeService(initialAppLevel: 1, initialTerminalLevel: 5);
            using var vm = new MainViewModel(
                new ShellProcessService(),
                new TabStatePersistenceService(tempFile),
                new MockThemeService(),
                new LocalizationService(),
                fontSizeService);

            Assert.Equal(1, vm.AppFontSizeLevel);
            Assert.Equal(5, vm.TerminalFontSizeLevel);

            // Act
            vm.ResetFontSizeLevels();

            // Assert
            Assert.Equal(3, vm.AppFontSizeLevel);
            Assert.Equal(3, vm.TerminalFontSizeLevel);
            Assert.Equal(12.0, vm.TerminalFontSize);
            Assert.Equal(1.0, vm.AppFontScale);
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
    public void ResetTerminalFontSize_ResetsOnlyTerminalLevelToDefault()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"multishell_state_test_{Guid.NewGuid():N}.json");
        try
        {
            var fontSizeService = new FontSizeService(initialAppLevel: 1, initialTerminalLevel: 5);
            using var vm = new MainViewModel(
                new ShellProcessService(),
                new TabStatePersistenceService(tempFile),
                new MockThemeService(),
                new LocalizationService(),
                fontSizeService);

            // Act
            vm.ResetTerminalFontSize();

            // Assert
            Assert.Equal(1, vm.AppFontSizeLevel);
            Assert.Equal(3, vm.TerminalFontSizeLevel);
            Assert.Equal(12.0, vm.TerminalFontSize);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
