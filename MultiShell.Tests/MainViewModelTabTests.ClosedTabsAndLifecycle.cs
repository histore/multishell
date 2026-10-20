using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using MultiShell.Views;
using Xunit;

namespace MultiShell.Tests;

public partial class MainViewModelTabTests
{
    [Fact]
    public async Task CloseTab_AddsToClosedTabs_AndCapsAt10Entries()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Create and close 12 tabs
        for (int i = 1; i <= 12; i++)
        {
            mainVm.AddNewTabWithDirectory($@"C:\projects\tab{i}");
            var session = processService.CreatedSessions[^1];
            session.TriggerCommandExecuted($"command-{i}");
            var tab = mainVm.Tabs[^1];
            mainVm.CloseTab(tab);
        }

        // Assert - Max 10 items, newest first
        Assert.Equal(10, mainVm.ClosedTabs.Count);
        Assert.True(mainVm.HasClosedTabs);
        Assert.Equal(@"C:\projects\tab12", mainVm.ClosedTabs[0].WorkingDirectory);
        Assert.Contains("command-12", mainVm.ClosedTabs[0].CommandHistory);
        // The oldest ones (tab 1 and tab 2) should have been evicted
        Assert.DoesNotContain(mainVm.ClosedTabs, c => c.WorkingDirectory == @"C:\projects\tab1");
        Assert.DoesNotContain(mainVm.ClosedTabs, c => c.WorkingDirectory == @"C:\projects\tab2");
        Assert.Equal(@"C:\projects\tab3", mainVm.ClosedTabs[^1].WorkingDirectory);
    }

    [Fact]
    public async Task RestoreClosedTab_OpensTabWithRestoredHistory_AndRemovesFromClosedTabs()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTabWithDirectory(@"C:\projects\custom-workdir", ShellType.WSL);
        var session = processService.CreatedSessions[^1];
        session.TriggerCommandExecuted("git status");
        session.TriggerCommandExecuted("dotnet build");

        var tabToClose = mainVm.Tabs[^1];
        mainVm.CloseTab(tabToClose);

        Assert.Single(mainVm.ClosedTabs);
        var closedItem = mainVm.ClosedTabs[0];

        // Act - Restore
        mainVm.RestoreClosedTab(closedItem);

        // Assert
        Assert.Empty(mainVm.ClosedTabs);
        Assert.False(mainVm.HasClosedTabs);
        var restoredTab = mainVm.SelectedTab;
        Assert.NotNull(restoredTab);
        Assert.Equal(ShellType.WSL, restoredTab.ShellType);
        Assert.Equal(@"C:\projects\custom-workdir", restoredTab.WorkingDirectory);
        Assert.Equal(2, restoredTab.CommandHistory.Count);
        Assert.Contains("git status", restoredTab.CommandHistory);
        Assert.Contains("dotnet build", restoredTab.CommandHistory);
    }

    [Fact]
    public async Task RemoveClosedTab_PurgesSpecificClosedTab()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTabWithDirectory(@"C:\dir1");
        mainVm.AddNewTabWithDirectory(@"C:\dir2");
        mainVm.CloseTab(mainVm.Tabs[1]);
        mainVm.CloseTab(mainVm.Tabs[1]);

        Assert.Equal(2, mainVm.ClosedTabs.Count);
        var targetToRemove = mainVm.ClosedTabs[0];

        // Act
        mainVm.RemoveClosedTab(targetToRemove);

        // Assert
        Assert.Single(mainVm.ClosedTabs);
        Assert.DoesNotContain(targetToRemove, mainVm.ClosedTabs);
    }

    [Fact]
    public async Task ClearClosedTabs_RemovesAllClosedTabs()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTabWithDirectory(@"C:\dir1");
        mainVm.AddNewTabWithDirectory(@"C:\dir2");
        mainVm.CloseTab(mainVm.Tabs[1]);
        mainVm.CloseTab(mainVm.Tabs[1]);
        Assert.True(mainVm.HasClosedTabs);

        // Act
        mainVm.ClearClosedTabs();

        // Assert
        Assert.Empty(mainVm.ClosedTabs);
        Assert.False(mainVm.HasClosedTabs);
    }

    [Fact]
    public async Task Persistence_SavesAndRestoresClosedTabs()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm1 = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm1.InitializeWorkspaceAsync();

        mainVm1.AddNewTabWithDirectory(@"C:\persist-test", ShellType.NuShell);
        var session = processService.CreatedSessions[^1];
        session.TriggerCommandExecuted("nu-command-1");
        var tab = mainVm1.Tabs[^1];
        mainVm1.CloseTab(tab);

        mainVm1.SaveCurrentStateSynchronously();

        // Act - Start second workspace session with persisted state
        using var mainVm2 = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm2.InitializeWorkspaceAsync();

        // Assert
        Assert.Single(mainVm2.ClosedTabs);
        Assert.True(mainVm2.HasClosedTabs);
        var restoredClosedItem = mainVm2.ClosedTabs[0];
        Assert.Equal(ShellType.NuShell, restoredClosedItem.ShellType);
        Assert.Equal(@"C:\persist-test", restoredClosedItem.WorkingDirectory);
        Assert.Contains("nu-command-1", restoredClosedItem.CommandHistory);
    }

    [Fact]
    public void IsInteractiveTabControl_CorrectlyIdentifiesButtonsAndFreeSpace()
    {
        // Button is interactive
        var button = new Avalonia.Controls.Button();
        Assert.True(MainWindow.IsInteractiveTabControl(button));

        // Child of Button (e.g. TextBlock inside tab button) is interactive
        var textBlock = new Avalonia.Controls.TextBlock();
        button.Content = textBlock;
        Assert.True(MainWindow.IsInteractiveTabControl(textBlock));

        // Empty space controls (Grid, ScrollViewer, StackPanel) are NOT interactive
        var scrollViewer = new Avalonia.Controls.ScrollViewer();
        var grid = new Avalonia.Controls.Grid();
        var stackPanel = new Avalonia.Controls.StackPanel();
        grid.Children.Add(stackPanel);

        Assert.False(MainWindow.IsInteractiveTabControl(scrollViewer));
        Assert.False(MainWindow.IsInteractiveTabControl(grid));
        Assert.False(MainWindow.IsInteractiveTabControl(stackPanel));
        Assert.False(MainWindow.IsInteractiveTabControl(null));
    }

    [Fact]
    public async Task AddNewTabCommand_ExecutedFromEmptyTabBarArea_CreatesAndSelectsNewTab()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        var initialCount = mainVm.Tabs.Count;
        Assert.Equal(1, initialCount);

        // Act: Simulating double-click on free area which executes AddNewTabCommand
        mainVm.AddNewTabCommand.Execute(null);

        // Assert: New tab added and selected
        Assert.Equal(initialCount + 1, mainVm.Tabs.Count);
        Assert.Equal(mainVm.Tabs[^1], mainVm.SelectedTab);
    }

    [Fact]
    public async Task SelectedTab_WhenChanged_UpdatesIsSelectedOnTabs()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Assert initial tab is selected
        Assert.Single(mainVm.Tabs);
        var tab1 = mainVm.Tabs[0];
        Assert.Same(tab1, mainVm.SelectedTab);
        Assert.True(tab1.IsSelected);

        // Act: Add second tab
        mainVm.AddNewTabCommand.Execute(null);
        var tab2 = mainVm.Tabs[1];

        // Assert tab2 is selected and tab1 is deselected
        Assert.Same(tab2, mainVm.SelectedTab);
        Assert.True(tab2.IsSelected);
        Assert.False(tab1.IsSelected);

        // Act: Select tab1 again
        mainVm.SelectTabCommand.Execute(tab1);

        // Assert tab1 is selected and tab2 is deselected
        Assert.Same(tab1, mainVm.SelectedTab);
        Assert.True(tab1.IsSelected);
        Assert.False(tab2.IsSelected);
    }

    [Fact]
    public async Task SaveCurrentStateSynchronously_PrunesNonExistentPaths_OnExit()
    {
        // Arrange
        var persistenceService = new FakeTabStatePersistenceService();
        var processService = new FakePowerShellProcessService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        var baseDir = Path.Combine(Path.GetTempPath(), $"multishell_exit_test_{Guid.NewGuid():N}");
        var existingDir = Path.Combine(baseDir, "existing");
        var deletedDir = Path.Combine(baseDir, "deleted");
        Directory.CreateDirectory(existingDir);
        Directory.CreateDirectory(deletedDir);

        try
        {
            // Record commands in both paths
            mainVm.PathCommandHistoryService.RecordCommand(existingDir, "cmd-keep");
            mainVm.PathCommandHistoryService.RecordCommand(deletedDir, "cmd-drop");

            // Delete one directory before exit
            Directory.Delete(deletedDir, recursive: true);

            // Act - Shutdown save
            mainVm.SaveCurrentStateSynchronously();

            // Assert
            Assert.NotNull(persistenceService.SavedState);
            Assert.NotNull(persistenceService.SavedState.PathCommandHistory);

            var normalizedExisting = PathCommandHistoryService.NormalizePath(existingDir);
            var normalizedDeleted = PathCommandHistoryService.NormalizePath(deletedDir);

            Assert.True(persistenceService.SavedState.PathCommandHistory.ContainsKey(normalizedExisting));
            Assert.False(persistenceService.SavedState.PathCommandHistory.ContainsKey(normalizedDeleted));
            Assert.Equal(new[] { "cmd-keep" }, persistenceService.SavedState.PathCommandHistory[normalizedExisting]);
        }
        finally
        {
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task InitializeWorkspaceAsync_CapsRestoredTabs_AtMaxRestoreTabsLimit()
    {
        // Arrange: prepare state with 100 tabs
        var excessiveTabs = new List<TabState>();
        for (int i = 0; i < 100; i++)
        {
            excessiveTabs.Add(new TabState($"Tab {i}", @"C:\Windows", null, null, ShellType.PowerShell));
        }

        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService
        {
            StateToReturn = new WorkspaceState(excessiveTabs, 0, null, 3, 3, ShellType.PowerShell)
        };

        using var vm = new MainViewModel(
            processService,
            persistenceService,
            new ThemeService(),
            new LocalizationService(),
            new FontSizeService());

        // Act
        await vm.InitializeWorkspaceAsync();

        // Assert: Should cap tabs at MaxRestoreTabsLimit (50)
        Assert.Equal(MainViewModel.MaxRestoreTabsLimit, vm.Tabs.Count);
    }

    [Fact]
    public void OpenDirectoryFromDrop_WithNullOrEmpty_DoesNothing()
    {
        var processService = new FakePowerShellProcessService();
        using var vm = new MainViewModel(processService, new FakeTabStatePersistenceService(), new ThemeService(), new LocalizationService(), new FontSizeService());
        var initialTab = vm.SelectedTab!;

        vm.OpenDirectoryFromDrop(null, openInNewTab: false);
        vm.OpenDirectoryFromDrop("", openInNewTab: false);
        vm.OpenDirectoryFromDrop("   ", openInNewTab: false);

        Assert.Single(vm.Tabs);
        Assert.Same(initialTab, vm.SelectedTab);
    }

    [Fact]
    public void OpenDirectoryFromDrop_WithoutShift_NavigatesSelectedTab()
    {
        var processService = new FakePowerShellProcessService();
        using var vm = new MainViewModel(processService, new FakeTabStatePersistenceService(), new ThemeService(), new LocalizationService(), new FontSizeService());
        var session = processService.CreatedSessions[0];

        vm.OpenDirectoryFromDrop(@"C:\Projects\MultiShell", openInNewTab: false);

        Assert.Single(vm.Tabs);
        Assert.NotEmpty(session.SentInputs);
        var sentText = System.Text.Encoding.UTF8.GetString(session.SentInputs[^1]);
        Assert.Contains(@"C:\Projects\MultiShell", sentText);
    }

    [Fact]
    public void OpenDirectoryFromDrop_WithShift_CreatesNewTabInDirectory()
    {
        var processService = new FakePowerShellProcessService();
        using var vm = new MainViewModel(processService, new FakeTabStatePersistenceService(), new ThemeService(), new LocalizationService(), new FontSizeService());

        vm.OpenDirectoryFromDrop(@"C:\Projects\MultiShell", openInNewTab: true);

        Assert.Equal(2, vm.Tabs.Count);
        Assert.Equal(@"C:\Projects\MultiShell", vm.SelectedTab!.WorkingDirectory);
    }

    [Fact]
    public void OpenDirectoryFromDrop_WhenNoTabsOpen_CreatesNewTabRegardlessOfShift()
    {
        var processService = new FakePowerShellProcessService();
        using var vm = new MainViewModel(processService, new FakeTabStatePersistenceService(), new ThemeService(), new LocalizationService(), new FontSizeService());
        vm.CloseTab(vm.Tabs[0]);
        Assert.Empty(vm.Tabs);

        vm.OpenDirectoryFromDrop(@"C:\Projects\MultiShell", openInNewTab: false);

        Assert.Single(vm.Tabs);
        Assert.Equal(@"C:\Projects\MultiShell", vm.SelectedTab!.WorkingDirectory);
    }

    [Fact]
    public void OpenDirectoryFromDrop_WithSpecificTargetTab_NavigatesTargetTabAndActivatesIt()
    {
        var processService = new FakePowerShellProcessService();
        using var vm = new MainViewModel(processService, new FakeTabStatePersistenceService(), new ThemeService(), new LocalizationService(), new FontSizeService());
        vm.AddNewTab(); // Tab 2
        var tab1 = vm.Tabs[0];
        var tab2 = vm.Tabs[1];
        vm.SelectedTab = tab2;

        var session1 = processService.CreatedSessions[0];

        vm.OpenDirectoryFromDrop(@"D:\Workspace", openInNewTab: false, targetTab: tab1);

        Assert.Same(tab1, vm.SelectedTab);
        Assert.NotEmpty(session1.SentInputs);
        var sentText = System.Text.Encoding.UTF8.GetString(session1.SentInputs[^1]);
        Assert.Contains(@"D:\Workspace", sentText);
    }

    [Fact]
    public void CloseTab_UnregistersPropertyChangedAndTabEvents()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        using var vm = new MainViewModel(processService, new FakeTabStatePersistenceService(), new ThemeService(), new LocalizationService(), new FontSizeService());
        vm.AddNewTab(); // PS 1 and PS 2
        var tab2 = vm.Tabs[1];

        // Act - Close tab2
        vm.CloseTab(tab2);

        // Mutating custom title on the closed tab should not alter WindowTitle or throw
        var initialTitle = vm.WindowTitle;
        var exception = Record.Exception(() =>
        {
            tab2.CustomTitle = "Zombie Tab Title";
            tab2.TabColor = "#FF0000";
        });

        // Assert
        Assert.Null(exception);
        Assert.Equal(initialTitle, vm.WindowTitle);
    }

    [Fact]
    public void CloseTab_DisposesViewModelAndDetachesModelUpdateUI()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        using var vm = new MainViewModel(processService, new FakeTabStatePersistenceService(), new ThemeService(), new LocalizationService(), new FontSizeService());
        vm.AddNewTab(); // PS 1 and PS 2
        var tab2 = vm.Tabs[1];
        tab2.TerminalModel.UpdateUI = () => { };

        // Act
        vm.CloseTab(tab2);

        // Assert
        Assert.Single(vm.Tabs);
        Assert.DoesNotContain(tab2, vm.Tabs);
        Assert.Null(tab2.TerminalModel.UpdateUI);
        Assert.Empty(tab2.CommandHistory);
    }
}
