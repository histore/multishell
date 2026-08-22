using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MultiShell.Models;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class TabStatePersistenceServiceTests : IDisposable
{
    private readonly string _tempFile;

    public TabStatePersistenceServiceTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"sharplauncher_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    [Fact]
    public async Task LoadStateAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        // Arrange
        var service = new TabStatePersistenceService(_tempFile);

        // Act
        var state = await service.LoadStateAsync();

        // Assert
        Assert.Null(state);
    }

    [Fact]
    public async Task SaveStateAsync_And_LoadStateAsync_PreservesTabsAndDirectory()
    {
        // Arrange
        var service = new TabStatePersistenceService(_tempFile);
        var originalState = new WorkspaceState(
            new List<TabState>
            {
                new("quicklaunch", @"C:\projekte\quicklaunch"),
                new("docs", @"C:\projekte\docs")
            },
            1);

        // Act
        await service.SaveStateAsync(originalState);
        var loadedState = await service.LoadStateAsync();

        // Assert
        Assert.NotNull(loadedState);
        Assert.Equal(2, loadedState.Tabs.Count);
        Assert.Equal("quicklaunch", loadedState.Tabs[0].Title);
        Assert.Equal(@"C:\projekte\quicklaunch", loadedState.Tabs[0].WorkingDirectory);
        Assert.Equal("docs", loadedState.Tabs[1].Title);
        Assert.Equal(@"C:\projekte\docs", loadedState.Tabs[1].WorkingDirectory);
        Assert.Equal(1, loadedState.SelectedIndex);
    }

    [Fact]
    public async Task SaveStateAsync_And_LoadStateAsync_PreservesCommandAndDirectoryHistories()
    {
        // Arrange
        var service = new TabStatePersistenceService(_tempFile);
        var originalState = new WorkspaceState(
            new List<TabState>
            {
                new(
                    "Tab 1",
                    @"C:\projekte\repo",
                    new List<string> { "git status", "dotnet build", "dotnet test" },
                    new List<string> { @"C:\projekte", @"C:\projekte\repo" })
            },
            0);

        // Act
        await service.SaveStateAsync(originalState);
        var loadedState = await service.LoadStateAsync();

        // Assert
        Assert.NotNull(loadedState);
        Assert.Single(loadedState.Tabs);
        var tab = loadedState.Tabs[0];
        Assert.NotNull(tab.CommandHistory);
        Assert.Equal(new[] { "git status", "dotnet build", "dotnet test" }, tab.CommandHistory);
        Assert.NotNull(tab.DirectoryHistory);
        Assert.Equal(new[] { @"C:\projekte", @"C:\projekte\repo" }, tab.DirectoryHistory);
    }

    [Fact]
    public async Task LoadStateAsync_WhenFileCorrupt_ReturnsNullGracefully()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempFile, "{ this is not valid json }");
        var service = new TabStatePersistenceService(_tempFile);

        // Act
        var state = await service.LoadStateAsync();

        // Assert
        Assert.Null(state);
    }

    [Fact]
    public async Task SaveStateAsync_And_LoadStateAsync_PreservesFontSizeLevels()
    {
        // Arrange
        var service = new TabStatePersistenceService(_tempFile);
        var originalState = new WorkspaceState(
            new List<TabState> { new("Tab 1", @"C:\projekte") },
            0,
            SavedLanguage: "de",
            AppFontSizeLevel: 4,
            TerminalFontSizeLevel: 2);

        // Act
        await service.SaveStateAsync(originalState);
        var loadedState = await service.LoadStateAsync();

        // Assert
        Assert.NotNull(loadedState);
        Assert.Equal(4, loadedState.AppFontSizeLevel);
        Assert.Equal(2, loadedState.TerminalFontSizeLevel);
        Assert.Equal("de", loadedState.SavedLanguage);
    }
}
