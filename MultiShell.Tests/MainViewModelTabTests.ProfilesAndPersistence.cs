using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public partial class MainViewModelTabTests
{
    [Fact]
    public async Task InitializeWorkspaceAsync_RestoresTabHistories_FromSavedState()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService
        {
            StateToReturn = new WorkspaceState(
                new List<TabState>
                {
                    new(
                        "Tab 1",
                        @"C:\projekte\app",
                        new List<string> { "npm install", "npm run dev" },
                        new List<string> { @"C:\projekte", @"C:\projekte\app" })
                },
                0)
        };

        // Act
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Assert
        Assert.Single(mainVm.Tabs);
        var tab = mainVm.Tabs[0];
        Assert.Equal(new[] { "npm run dev", "npm install" }, tab.CommandHistory);
        Assert.Equal(new[] { @"C:\projekte\app", @"C:\projekte" }, tab.DirectoryHistory);
    }

    [Fact]
    public async Task InitializeWorkspaceAsync_RestoresTabHistories_FiltersByRecencyRegardlessOfLength()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService
        {
            StateToReturn = new WorkspaceState(
                new List<TabState>
                {
                    new(
                        "Tab 1",
                        @"C:\projekte\app",
                        new List<string> { "git log", "git commit -m \"fix\"" },
                        new List<string> { @"C:\projekte", @"C:\projekte\app\very\long\nested\subfolder" })
                },
                0)
        };

        // Act
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Assert: FilteredCommandHistory ranks newer longer command ahead of older shorter command
        var tab = mainVm.Tabs[0];
        tab.CommandFilterQuery = "git";
        Assert.Equal(2, tab.FilteredCommandHistory.Count);
        Assert.Equal("git commit -m \"fix\"", tab.FilteredCommandHistory[0]);
        Assert.Equal("git log", tab.FilteredCommandHistory[1]);

        // Assert: FilteredDirectoryHistory ranks newer longer directory ahead of older shorter directory
        tab.DirectoryFilterQuery = "projekte";
        Assert.True(tab.FilteredDirectoryHistory.Count >= 2);
        var longDir = DirectoryHistoryService.NormalizePath(@"C:\projekte\app\very\long\nested\subfolder");
        var shortDir = DirectoryHistoryService.NormalizePath(@"C:\projekte");
        var longIndex = tab.FilteredDirectoryHistory.IndexOf(longDir);
        var shortIndex = tab.FilteredDirectoryHistory.IndexOf(shortDir);
        Assert.True(longIndex >= 0 && shortIndex >= 0);
        Assert.True(longIndex < shortIndex, "Newer directory must rank before older shorter directory.");
    }

    [Fact]
    public async Task HistoryChange_TriggersSaveState_WithLatestHistories()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        var session = processService.CreatedSessions[^1];

        // Act - Change directory and execute commands in session
        session.TriggerDirectoryChange(@"C:\projekte\demo");
        session.TriggerCommandExecuted("Get-ChildItem");
        session.TriggerCommandExecuted("git status");

        // Allow async save task
        await Task.Delay(50);
        mainVm.SaveCurrentStateSynchronously();

        // Assert
        Assert.NotNull(persistenceService.SavedState);
        var savedTab = persistenceService.SavedState.Tabs[0];
        Assert.Contains("Get-ChildItem", savedTab.CommandHistory!);
        Assert.Contains("git status", savedTab.CommandHistory!);
        Assert.Contains(@"C:\projekte\demo", savedTab.DirectoryHistory!);
    }

    [Fact]
    public async Task CloseTab_RemovesTabAndPurgesItsHistory_FromSavedState()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTabWithDirectory(@"C:\projekte\tab2");
        var session2 = processService.CreatedSessions[^1];
        session2.TriggerCommandExecuted("specific-command-for-tab-2");

        // Act - Close tab 2
        var tab2 = mainVm.Tabs[1];
        mainVm.CloseTab(tab2);

        // Allow save
        await Task.Delay(50);
        mainVm.SaveCurrentStateSynchronously();

        // Assert - Saved state now only contains tab 1
        Assert.NotNull(persistenceService.SavedState);
        Assert.Single(persistenceService.SavedState.Tabs);
        var savedTab1 = persistenceService.SavedState.Tabs[0];
        Assert.DoesNotContain("specific-command-for-tab-2", savedTab1.CommandHistory ?? new List<string>());
    }

    [Fact]
    public async Task AddNewTabWithShell_CreatesTabWithRequestedShellType()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Act
        mainVm.AddNewTabWithShell(ShellType.WSL);
        mainVm.AddNewTabWithShell(ShellType.CMD);

        // Assert
        Assert.Equal(3, mainVm.Tabs.Count);
        Assert.Equal(ShellType.PowerShell, mainVm.Tabs[0].ShellType);
        Assert.Equal(ShellType.WSL, mainVm.Tabs[1].ShellType);
        Assert.Equal(ShellType.CMD, mainVm.Tabs[2].ShellType);
        Assert.Equal("WSL", mainVm.Tabs[1].ShellIconTag);
        Assert.Equal("CMD", mainVm.Tabs[2].ShellIconTag);
    }

    [Fact]
    public async Task InitializeWorkspaceAsync_RestoresHeterogeneousShellTypes()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService
        {
            StateToReturn = new WorkspaceState(
                new List<TabState>
                {
                    new("PS 1", @"C:\ps", ShellType: ShellType.PowerShell),
                    new("WSL 2", @"/home/user", ShellType: ShellType.WSL),
                    new("CMD 3", @"C:\cmd", ShellType: ShellType.CMD)
                },
                1)
        };

        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());

        // Act
        await mainVm.InitializeWorkspaceAsync();

        // Assert
        Assert.Equal(3, mainVm.Tabs.Count);
        Assert.Equal(ShellType.PowerShell, mainVm.Tabs[0].ShellType);
        Assert.Equal(ShellType.WSL, mainVm.Tabs[1].ShellType);
        Assert.Equal(ShellType.CMD, mainVm.Tabs[2].ShellType);
        Assert.Equal(mainVm.Tabs[1], mainVm.SelectedTab);
    }

    [Fact]
    public async Task DuplicateTab_PreservesSourceTabShellType()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTabWithShell(ShellType.NuShell);
        var nuTab = mainVm.Tabs[1];
        mainVm.SelectedTab = nuTab;

        // Act
        mainVm.DuplicateTab();

        // Assert
        Assert.Equal(3, mainVm.Tabs.Count);
        var duplicatedTab = mainVm.Tabs[2];
        Assert.Equal(ShellType.NuShell, duplicatedTab.ShellType);
    }

    [Fact]
    public async Task AddNewTab_UsesLastSelectedShellType_AndPersistsAcrossRestarts()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Act 1 - Select CMD via AddNewTabWithShell
        mainVm.AddNewTabWithShell(ShellType.CMD);
        Assert.Equal(ShellType.CMD, mainVm.DefaultShellType);

        // Act 2 - Subsequent click on (+) AddNewTab should create CMD tab
        mainVm.AddNewTab();
        Assert.Equal(3, mainVm.Tabs.Count);
        Assert.Equal(ShellType.CMD, mainVm.Tabs[2].ShellType);

        // Act 3 - Force synchronous save and verify saved WorkspaceState has DefaultShellType
        mainVm.SaveCurrentStateSynchronously();
        Assert.NotNull(persistenceService.SavedState);
        Assert.Equal(ShellType.CMD, persistenceService.SavedState.DefaultShellType);
    }

    [Fact]
    public async Task NewTabTooltip_UpdatesDynamically_WhenDefaultShellOrLanguageChanges()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        var locService = new LocalizationService("de", isUserSelection: true);
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), locService, new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Initial default: PowerShell in German
        Assert.Contains("PowerShell", mainVm.NewTabTooltip);
        Assert.Contains("Neuer", mainVm.NewTabTooltip);

        // Switch to WSL
        mainVm.AddNewTabWithShell(ShellType.WSL);
        Assert.Contains("WSL", mainVm.NewTabTooltip);

        // Switch to English
        locService.SetLanguage("en", isUserSelection: true);
        Assert.Contains("New", mainVm.NewTabTooltip);
        Assert.Contains("WSL", mainVm.NewTabTooltip);
    }

    [Fact]
    public async Task AddNewTabWithProfile_SpawnsTabWithProfileSettings()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        var customProfile = new TerminalProfileItemViewModel(new TerminalProfile(
            Guid.NewGuid(),
            "Git Bash",
            @"C:\Program Files\Git\bin\bash.exe",
            "--login -i",
            IconTag: "GIT",
            ShellType: ShellType.PowerShell));

        // Act
        mainVm.AddNewTabWithProfile(customProfile);

        // Assert
        Assert.Equal(2, mainVm.Tabs.Count);
        var tab = mainVm.Tabs[1];
        Assert.Equal("GIT 2", tab.Title);
        Assert.Equal(ShellType.PowerShell, tab.ShellType);
    }

    [Fact]
    public async Task ProfileModal_SaveAndStartNewProfile_WorkflowWorks()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_profiles_{Guid.NewGuid():N}.json");
        try
        {
            var processService = new FakePowerShellProcessService();
            var persistenceService = new FakeTabStatePersistenceService();
            var profileService = new TerminalProfileService(tempFile);
            using var mainVm = new MainViewModel(
                processService,
                persistenceService,
                new ThemeService(),
                new LocalizationService(),
                new FontSizeService(),
                terminalProfileService: profileService);
            await mainVm.InitializeWorkspaceAsync();

            // Act 1 - Open modal
            mainVm.OpenProfilesModal();
            Assert.True(mainVm.IsProfilesModalOpen);

            // Act 2 - Start new profile
            mainVm.StartNewProfile();
            Assert.True(mainVm.IsEditingProfile);
            Assert.True(mainVm.IsCreatingNewProfile);
            Assert.Equal(TerminalProfileService.GetDefaultWorkingDirectory(), mainVm.EditingWorkingDirectory);

            mainVm.EditingProfileName = "My Custom Shell";
            mainVm.EditingExecutablePath = @"C:\custom\shell.exe";
            mainVm.EditingWorkingDirectory = @"C:\custom\projects";
            mainVm.EditingIconTag = "CSH";

            await mainVm.SaveProfileAsync();

            // Assert - editing reset, profile added
            Assert.False(mainVm.IsEditingProfile);
            var added = mainVm.Profiles.FirstOrDefault(p => p.Name == "My Custom Shell" && p.IconTag == "CSH");
            Assert.NotNull(added);
            Assert.Equal(@"C:\custom\projects", added.WorkingDirectory);

            // Act 3 - Close modal
            mainVm.CloseProfilesModal();
            Assert.False(mainVm.IsProfilesModalOpen);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    [Fact]
    public async Task AddNewTabWithProfile_UsesConfiguredProfileWorkingDirectory()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        var customDir = @"C:\MyWorkspace\App";
        var customProfile = new TerminalProfile(
            Guid.NewGuid(),
            "Node Shell",
            "pwsh.exe",
            Arguments: null,
            WorkingDirectory: customDir,
            IconTag: "NODE",
            ShellType: ShellType.PowerShell);

        var profileVm = new TerminalProfileItemViewModel(customProfile);

        // Act
        mainVm.AddNewTabWithProfile(profileVm);

        // Assert
        Assert.NotNull(mainVm.SelectedTab);
        var session = processService.CreatedSessions.LastOrDefault();
        Assert.NotNull(session);
        Assert.Equal(customDir, session.WorkingDirectory);
    }

    [Fact]
    public async Task AddNewTab_UsesMatchingProfileWorkingDirectory_WhenWorkingDirectoryNotExplicitlyProvided()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        var customDir = @"C:\CustomPowerShellDir";
        var psProfile = mainVm.Profiles.FirstOrDefault(p => p.ShellType == ShellType.PowerShell);
        Assert.NotNull(psProfile);
        psProfile.WorkingDirectory = customDir;

        // Act
        mainVm.AddNewTab();

        // Assert
        var lastSession = processService.CreatedSessions.LastOrDefault();
        Assert.NotNull(lastSession);
        Assert.Equal(customDir, lastSession.WorkingDirectory);
    }

    [Fact]
    public async Task AddNewTabWithShell_UsesMatchingProfileWorkingDirectory_WhenWorkingDirectoryNotExplicitlyProvided()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        var customCmdDir = @"C:\CustomCmdDir";
        var cmdProfile = mainVm.Profiles.FirstOrDefault(p => p.ShellType == ShellType.CMD);
        Assert.NotNull(cmdProfile);
        cmdProfile.WorkingDirectory = customCmdDir;

        // Act
        mainVm.AddNewTabWithShell(ShellType.CMD);

        // Assert
        var lastSession = processService.CreatedSessions.LastOrDefault();
        Assert.NotNull(lastSession);
        Assert.Equal(customCmdDir, lastSession.WorkingDirectory);
        Assert.Equal(ShellType.CMD, lastSession.ShellType);
    }

    [Fact]
    public async Task AddNewTabWithDirectory_OverridesProfileWorkingDirectory_WhenExplicitlyProvided()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        var explicitDir = @"C:\ExplicitOverrideDir";

        // Act
        mainVm.AddNewTabWithDirectory(explicitDir, ShellType.PowerShell);

        // Assert
        var lastSession = processService.CreatedSessions.LastOrDefault();
        Assert.NotNull(lastSession);
        Assert.Equal(explicitDir, lastSession.WorkingDirectory);
    }

    [Fact]
    public async Task StartEditProfile_PopulatesEditingWorkingDirectory()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        var profileItem = mainVm.Profiles.First();

        // Act
        mainVm.StartEditProfile(profileItem);

        // Assert
        Assert.True(mainVm.IsEditingProfile);
        Assert.False(mainVm.IsCreatingNewProfile);
        Assert.Equal(profileItem.WorkingDirectory, mainVm.EditingWorkingDirectory);
    }
}
