using System;
using System.IO;
using System.Threading.Tasks;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public class MainViewModelTests
{
    private sealed class FakeShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; set; }
        public string? WorkingDirectory { get; set; }
        public ShellType ShellType { get; }
        public bool IsRunning { get; private set; }

#pragma warning disable CS0067
        public event Action<ReadOnlyMemory<byte>>? DataReceived;
        public event Action<int>? Exited;
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted;
        public event Action<int>? CommandCompleted;
#pragma warning restore CS0067

        public FakeShellSession(string title, string? workingDir, ShellType shellType)
        {
            Title = title;
            WorkingDirectory = workingDir ?? Environment.CurrentDirectory;
            ShellType = shellType;
        }

        public void Start() => IsRunning = true;
        public void Send(byte[] input) { }
        public void Resize(int cols, int rows) { }
        public void Dispose() => IsRunning = false;
    }

    private sealed class FakeShellProcessService : IShellProcessService
    {
        public IShellSession CreateSession(
            string title,
            string? workingDirectory = null,
            ShellType shellType = ShellType.PowerShell,
            string? customExecutable = null,
            string? customArguments = null) =>
            new FakeShellSession(title, workingDirectory, shellType);
    }

    private static MainViewModel CreateIsolatedViewModel(
        out string tempFile,
        IThemeService? themeService = null,
        ILocalizationService? localizationService = null,
        IFontSizeService? fontSizeService = null,
        bool isSatellite = false)
    {
        tempFile = Path.Combine(Path.GetTempPath(), $"multishell_test_{Guid.NewGuid():N}.json");
        return new MainViewModel(
            new FakeShellProcessService(),
            new TabStatePersistenceService(tempFile),
            themeService ?? new MockThemeService(),
            localizationService ?? new LocalizationService(),
            fontSizeService ?? new FontSizeService(),
            isSatellite: isSatellite);
    }

    [Fact]
    public void AppVersion_IsNotNullOrEmpty_AndStartsWithV()
    {
        // Arrange & Act
        using var vm = CreateIsolatedViewModel(out var tempFile);
        try
        {
            // Assert
            Assert.False(string.IsNullOrWhiteSpace(vm.AppVersion));
            Assert.StartsWith("v", vm.AppVersion);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void UiTerminalStack_ContainsAvalonia12AndConPTY()
    {
        // Arrange & Act
        using var vm = CreateIsolatedViewModel(out var tempFile);
        try
        {
            // Assert
            Assert.Contains("Avalonia UI 12", vm.UiTerminalStack);
            Assert.Contains("ConPTY", vm.UiTerminalStack);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void GitHubUrl_IsConfiguredCorrectly()
    {
        // Arrange & Act
        using var vm = CreateIsolatedViewModel(out var tempFile);
        try
        {
            // Assert
            Assert.Equal("https://github.com/histore/multishell", vm.GitHubUrl);
            Assert.NotNull(vm.OpenGitHubUrlCommand);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WindowTitle_ReflectsSelectedTabPathWithoutEllipsis_WhenPathIsNotExcessivelyLong()
    {
        // Arrange
        using var vm = CreateIsolatedViewModel(out var tempFile);
        try
        {
            await vm.InitializeWorkspaceAsync();

            // Assert - Title starts with MultiShell - and contains untruncated path when reasonable length
            Assert.StartsWith("MultiShell - ", vm.WindowTitle);
            Assert.NotNull(vm.SelectedTab);
            if (!string.IsNullOrWhiteSpace(vm.SelectedTab.WorkingDirectory) && vm.SelectedTab.WorkingDirectory.Length <= 65)
            {
                Assert.Equal($"MultiShell - {vm.SelectedTab.WorkingDirectory}", vm.WindowTitle);
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private class MockThemeService : IThemeService
    {
        public bool IsDarkAppTheme { get; private set; } = true;
        public bool IsDarkTerminalTheme { get; private set; } = true;
        public event Action<bool>? AppThemeChanged;
        public event Action<bool>? TerminalThemeChanged;

        public void SetAppTheme(bool isDark)
        {
            IsDarkAppTheme = isDark;
            AppThemeChanged?.Invoke(isDark);
        }

        public void SetTerminalTheme(bool isDark)
        {
            IsDarkTerminalTheme = isDark;
            TerminalThemeChanged?.Invoke(isDark);
        }

        public void ToggleAppTheme() => SetAppTheme(!IsDarkAppTheme);
        public void ToggleTerminalTheme() => SetTerminalTheme(!IsDarkTerminalTheme);
    }

    [Fact]
    public void ToggleAppTheme_SwitchesAppThemeIndependently()
    {
        var mockTheme = new MockThemeService();
        using var vm = CreateIsolatedViewModel(out var tempFile, themeService: mockTheme);
        try
        {
            Assert.True(vm.IsDarkAppTheme);
            Assert.True(vm.IsDarkTerminalTheme);

            vm.ToggleAppThemeCommand.Execute(null);

            Assert.False(vm.IsDarkAppTheme);
            Assert.False(mockTheme.IsDarkAppTheme);
            Assert.True(vm.IsDarkTerminalTheme);
            Assert.True(mockTheme.IsDarkTerminalTheme);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ToggleTerminalTheme_SwitchesTerminalThemeIndependently()
    {
        var mockTheme = new MockThemeService();
        using var vm = CreateIsolatedViewModel(out var tempFile, themeService: mockTheme);
        try
        {
            vm.ToggleTerminalThemeCommand.Execute(null);

            Assert.True(vm.IsDarkAppTheme);
            Assert.True(mockTheme.IsDarkAppTheme);
            Assert.False(vm.IsDarkTerminalTheme);
            Assert.False(mockTheme.IsDarkTerminalTheme);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void SetThemes_UpdatesStatesExplicitly()
    {
        var mockTheme = new MockThemeService();
        using var vm = CreateIsolatedViewModel(out var tempFile, themeService: mockTheme);
        try
        {
            vm.SetAppTheme(false);
            vm.SetTerminalTheme(false);

            Assert.False(vm.IsDarkAppTheme);
            Assert.False(vm.IsDarkTerminalTheme);

            vm.SetAppTheme(true);
            vm.SetTerminalTheme(true);

            Assert.True(vm.IsDarkAppTheme);
            Assert.True(vm.IsDarkTerminalTheme);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void SetFontSizeLevels_UpdatesValuesAndScales()
    {
        using var vm = CreateIsolatedViewModel(out var tempFile, fontSizeService: new FontSizeService());
        try
        {
            Assert.Equal(4, vm.AppFontSizeLevel);
            Assert.Equal(4, vm.TerminalFontSizeLevel);
            Assert.Equal(1.0, vm.AppFontScale);
            Assert.Equal(14.0, vm.TerminalFontSize);

            vm.SetAppFontSizeLevelCommand.Execute(7);
            vm.SetTerminalFontSizeLevelCommand.Execute(1);

            Assert.Equal(7, vm.AppFontSizeLevel);
            Assert.Equal(1.30, vm.AppFontScale);
            Assert.Equal(1, vm.TerminalFontSizeLevel);
            Assert.Equal(10.0, vm.TerminalFontSize);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void IncreaseAndDecreaseFontSize_UpdatesLevelsWithinRange()
    {
        using var vm = CreateIsolatedViewModel(out var tempFile, fontSizeService: new FontSizeService());
        try
        {
            vm.IncreaseAppFontSizeCommand.Execute(null);
            vm.IncreaseTerminalFontSizeCommand.Execute(null);

            Assert.Equal(5, vm.AppFontSizeLevel);
            Assert.Equal(5, vm.TerminalFontSizeLevel);

            vm.DecreaseAppFontSizeCommand.Execute(null);
            vm.DecreaseAppFontSizeCommand.Execute(null);

            Assert.Equal(3, vm.AppFontSizeLevel);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ResetFontSizeLevels_ResetsBothLevelsToDefault()
    {
        var fontSizeService = new FontSizeService(initialAppLevel: 1, initialTerminalLevel: 7);
        using var vm = CreateIsolatedViewModel(out var tempFile, fontSizeService: fontSizeService);
        try
        {
            Assert.Equal(1, vm.AppFontSizeLevel);
            Assert.Equal(7, vm.TerminalFontSizeLevel);

            vm.ResetFontSizeLevels();

            Assert.Equal(4, vm.AppFontSizeLevel);
            Assert.Equal(4, vm.TerminalFontSizeLevel);
            Assert.Equal(14.0, vm.TerminalFontSize);
            Assert.Equal(1.0, vm.AppFontScale);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ResetTerminalFontSize_ResetsOnlyTerminalLevelToDefault()
    {
        var fontSizeService = new FontSizeService(initialAppLevel: 1, initialTerminalLevel: 7);
        using var vm = CreateIsolatedViewModel(out var tempFile, fontSizeService: fontSizeService);
        try
        {
            vm.ResetTerminalFontSize();

            Assert.Equal(1, vm.AppFontSizeLevel);
            Assert.Equal(4, vm.TerminalFontSizeLevel);
            Assert.Equal(14.0, vm.TerminalFontSize);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Dispose_UnsubscribesSharedServiceEvents()
    {
        var localizationService = new LocalizationService("en", isUserSelection: true);
        var fontSizeService = new FontSizeService(initialAppLevel: 4, initialTerminalLevel: 4);
        var vm = CreateIsolatedViewModel(
            out var tempFile,
            localizationService: localizationService,
            fontSizeService: fontSizeService,
            isSatellite: true);
        try
        {
            vm.Dispose();
            localizationService.SetLanguage("de", isUserSelection: true);
            fontSizeService.SetAppFontSizeLevel(6);
            fontSizeService.SetTerminalFontSizeLevel(7);

            Assert.Equal("en", vm.CurrentLanguage);
            Assert.Equal(4, vm.AppFontSizeLevel);
            Assert.Equal(4, vm.TerminalFontSizeLevel);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
