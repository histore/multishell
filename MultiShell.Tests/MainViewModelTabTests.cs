using System;
using System.Collections.Generic;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using System.Threading.Tasks;
using Xunit;

namespace MultiShell.Tests;

public class MainViewModelTabTests
{


    private class MockPowerShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; }
        public string? WorkingDirectory { get; set; }
        public ShellType ShellType { get; set; } = ShellType.PowerShell;
        public bool IsRunning { get; set; } = true;
        public bool Disposed { get; private set; }
        public bool FailOnDispose { get; set; }
        public bool RefuseToStop { get; set; }

        public event Action<byte[]>? DataReceived { add { } remove { } }
        public event Action<int>? Exited;
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted;

        public MockPowerShellSession(string title, string? workingDirectory = null)
        {
            Title = title;
            WorkingDirectory = workingDirectory;
        }

        public void Start() { IsRunning = true; }
        public void Send(byte[] input) { }
        public void Resize(int cols, int rows) { }

        public void TriggerCommandExecuted(string cmd)
        {
            CommandExecuted?.Invoke(cmd);
        }

        public void TriggerExit(int exitCode = 0)
        {
            IsRunning = false;
            Exited?.Invoke(exitCode);
        }

        public void TriggerDirectoryChange(string newDir)
        {
            WorkingDirectory = newDir;
            WorkingDirectoryChanged?.Invoke(newDir);
        }

        public void Dispose()
        {
            if (FailOnDispose)
            {
                throw new InvalidOperationException("Failed to terminate process.");
            }

            Disposed = true;
            if (!RefuseToStop)
            {
                IsRunning = false;
            }
        }
    }

    private class FakePowerShellProcessService : IShellProcessService
    {
        public List<MockPowerShellSession> CreatedSessions { get; } = new();

        public IShellSession CreateSession(
            string title,
            string? workingDirectory = null,
            ShellType shellType = ShellType.PowerShell,
            string? customExecutable = null,
            string? customArguments = null)
        {
            var session = new MockPowerShellSession(title, workingDirectory) { ShellType = shellType };
            CreatedSessions.Add(session);
            return session;
        }
    }

    private class FakeTabStatePersistenceService : ITabStatePersistenceService
    {
        public WorkspaceState? SavedState { get; set; }
        public WorkspaceState? StateToReturn { get; set; }
        public int SaveCallCount { get; private set; }

        public Task SaveStateAsync(WorkspaceState state)
        {
            SavedState = state;
            SaveCallCount++;
            return Task.CompletedTask;
        }

        public Task<WorkspaceState?> LoadStateAsync()
        {
            return Task.FromResult(StateToReturn);
        }
    }

    [Fact]
    public void Constructor_InitializesWithSingleTab_WhenNoSavedState()
    {
        // Arrange
        
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();

        // Act
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());

        // Assert
        Assert.Single(mainVm.Tabs);
        Assert.NotNull(mainVm.SelectedTab);
        Assert.Equal("PS 1", mainVm.SelectedTab!.Title);
    }

    [Fact]
    public async Task InitializeWorkspaceAsync_RestoresTabsAndDirectories_WhenSavedStateExists()
    {
        // Arrange
        
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService
        {
            StateToReturn = new WorkspaceState(
                new List<TabState>
                {
                    new("frontend", @"C:\projekte\frontend"),
                    new("backend", @"C:\projekte\backend")
                },
                1)
        };

        // Act
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Assert
        Assert.Equal(2, mainVm.Tabs.Count);
        Assert.Equal(@"C:\projekte\frontend", mainVm.Tabs[0].Title);
        Assert.Equal(@"C:\projekte\frontend", mainVm.Tabs[0].WorkingDirectory);
        Assert.Equal(@"C:\projekte\backend", mainVm.Tabs[1].Title);
        Assert.Equal(@"C:\projekte\backend", mainVm.Tabs[1].WorkingDirectory);
        Assert.Same(mainVm.Tabs[1], mainVm.SelectedTab);
    }

    [Fact]
    public async Task InitializeWorkspaceAsync_PreservesExactTabOrderAndSelectedIndex()
    {
        // Arrange
        
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService
        {
            StateToReturn = new WorkspaceState(
                new List<TabState>
                {
                    new(@"C:\projekte\first", @"C:\projekte\first"),
                    new(@"C:\projekte\second", @"C:\projekte\second"),
                    new(@"C:\projekte\third", @"C:\projekte\third"),
                    new(@"C:\projekte\fourth", @"C:\projekte\fourth")
                },
                2)
        };

        // Act
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Assert - Verify exact order from 0 to 3
        Assert.Equal(4, mainVm.Tabs.Count);
        Assert.Equal(@"C:\projekte\first", mainVm.Tabs[0].WorkingDirectory);
        Assert.Equal(@"C:\projekte\second", mainVm.Tabs[1].WorkingDirectory);
        Assert.Equal(@"C:\projekte\third", mainVm.Tabs[2].WorkingDirectory);
        Assert.Equal(@"C:\projekte\fourth", mainVm.Tabs[3].WorkingDirectory);
        Assert.Same(mainVm.Tabs[2], mainVm.SelectedTab);
    }

    [Fact]
    public void AddNewTab_IncreasesTabCountAndSelectsNewTab()
    {
        // Arrange
        
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());

        // Act
        mainVm.AddNewTab();

        // Assert
        Assert.Equal(2, mainVm.Tabs.Count);
        Assert.Equal("PS 2", mainVm.SelectedTab?.Title);
    }

    [Fact]
    public void DuplicateTab_CreatesNewTabWithSameWorkingDirectory()
    {
        // Arrange
        
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        mainVm.SelectedTab!.WorkingDirectory = @"C:\projekte\quicklaunch";

        // Act
        mainVm.DuplicateTab();

        // Assert
        Assert.Equal(2, mainVm.Tabs.Count);
        Assert.Equal(@"C:\projekte\quicklaunch", mainVm.SelectedTab?.WorkingDirectory);
        Assert.Equal(@"C:\projekte\quicklaunch", processService.CreatedSessions[1].WorkingDirectory);
    }

    [Fact]
    public void MoveTab_ReordersTabsAndUpdatesSelectionAndSavesState()
    {
        // Arrange
        
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        mainVm.AddNewTabWithDirectory(@"C:\projekte\tab1");
        mainVm.AddNewTabWithDirectory(@"C:\projekte\tab2");
        mainVm.AddNewTabWithDirectory(@"C:\projekte\tab3");
        // Tabs: [0]=PS 1, [1]=tab1, [2]=tab2, [3]=tab3

        var tabToMove = mainVm.Tabs[1]; // tab1
        var targetTab = mainVm.Tabs[3]; // tab3

        // Act - Move tab1 to the position of tab3
        mainVm.MoveTab(tabToMove, targetTab);

        // Assert - Order should be: PS 1, tab2, tab3, tab1
        Assert.Equal(4, mainVm.Tabs.Count);
        Assert.Same(mainVm.Tabs[3], tabToMove);
        Assert.Same(mainVm.SelectedTab, tabToMove);
    }

    [Fact]
    public void MoveTab_WithInvalidArguments_DoesNothing()
    {
        // Arrange
        
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        mainVm.AddNewTab();

        var tab0 = mainVm.Tabs[0];

        // Act & Assert - Should not throw or modify
        mainVm.MoveTab(tab0, tab0);
        Assert.Same(mainVm.Tabs[0], tab0);
    }

    [Fact]
    public void CloseTab_RemovesTabAndDisposesSession()
    {
        // Arrange
        
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        mainVm.AddNewTab(); // Now has PS 1 and PS 2
        var tab2 = mainVm.Tabs[1];

        // Act
        mainVm.CloseTab(tab2);

        // Assert
        Assert.Single(mainVm.Tabs);
        Assert.Equal("PS 1", mainVm.SelectedTab?.Title);
    }

    [Fact]
    public void CloseTab_WhenProcessCannotBeTerminated_KeepsTabOpen()
    {
        // Arrange
        
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        mainVm.AddNewTab(); // PS 1 and PS 2
        var tab2 = mainVm.Tabs[1];
        var session2 = processService.CreatedSessions[1];
        session2.RefuseToStop = true;

        // Act
        mainVm.CloseTab(tab2);

        // Assert - Tab remains open
        Assert.Equal(2, mainVm.Tabs.Count);
        Assert.Contains(tab2, mainVm.Tabs);
    }

    [Fact]
    public void CloseTab_WhenTerminationThrowsException_KeepsTabOpen()
    {
        // Arrange
        
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        mainVm.AddNewTab(); // PS 1 and PS 2
        var tab2 = mainVm.Tabs[1];
        var session2 = processService.CreatedSessions[1];
        session2.FailOnDispose = true;

        // Act
        mainVm.CloseTab(tab2);

        // Assert - Tab remains open
        Assert.Equal(2, mainVm.Tabs.Count);
        Assert.Contains(tab2, mainVm.Tabs);
    }

    [Fact]
    public void ProcessExit_AutomaticallyClosesTab()
    {
        // Arrange
        
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        mainVm.AddNewTab(); // PS 1 and PS 2
        var tab2 = mainVm.Tabs[1];
        var session2 = processService.CreatedSessions[1];

        // Act - Simulate shell process exiting (e.g. user ran 'exit')
        session2.TriggerExit(0);

        // Assert - Tab was automatically removed
        Assert.Single(mainVm.Tabs);
        Assert.Equal("PS 1", mainVm.SelectedTab?.Title);
    }

    [Fact]
    public void SelectTab_UpdatesSelectedTabProperty()
    {
        // Arrange
        
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        mainVm.AddNewTab();

        // Act
        mainVm.SelectTab(mainVm.Tabs[0]);

        // Assert
        Assert.Equal("PS 1", mainVm.SelectedTab?.Title);
    }

    [Fact]
    public void SelectNextTab_AdvancesToNextTab_And_ClampsAtEnd()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        // mainVm initializes with 1 tab (Tabs[0] = "PS 1")
        mainVm.AddNewTab(); // Tab 1 (PS 2)
        mainVm.AddNewTab(); // Tab 2 (PS 3)

        Assert.Equal(3, mainVm.Tabs.Count);

        mainVm.SelectTab(mainVm.Tabs[0]);
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);

        // Act & Assert 1: Move from 0 to 1
        mainVm.SelectNextTab();
        Assert.Same(mainVm.Tabs[1], mainVm.SelectedTab);

        // Act & Assert 2: Move from 1 to 2
        mainVm.SelectNextTab();
        Assert.Same(mainVm.Tabs[2], mainVm.SelectedTab);

        // Act & Assert 3: Already at last tab, clamp at index 2
        mainVm.SelectNextTab();
        Assert.Same(mainVm.Tabs[2], mainVm.SelectedTab);
    }

    [Fact]
    public void SelectPreviousTab_MovesToPreviousTab_And_ClampsAtStart()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        // mainVm initializes with 1 tab (Tabs[0] = "PS 1")
        mainVm.AddNewTab(); // Tab 1 (PS 2)
        mainVm.AddNewTab(); // Tab 2 (PS 3)

        Assert.Equal(3, mainVm.Tabs.Count);

        mainVm.SelectTab(mainVm.Tabs[2]);
        Assert.Same(mainVm.Tabs[2], mainVm.SelectedTab);

        // Act & Assert 1: Move from 2 to 1
        mainVm.SelectPreviousTab();
        Assert.Same(mainVm.Tabs[1], mainVm.SelectedTab);

        // Act & Assert 2: Move from 1 to 0
        mainVm.SelectPreviousTab();
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);

        // Act & Assert 3: Already at first tab, clamp at index 0
        mainVm.SelectPreviousTab();
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);
    }

    [Fact]
    public void SelectNextTab_And_SelectPreviousTab_WhenSingleTab_DoesNothing()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        // Initial single tab
        Assert.Single(mainVm.Tabs);
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);

        // Act & Assert
        mainVm.SelectNextTab();
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);

        mainVm.SelectPreviousTab();
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);
    }

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
        Assert.Equal(new[] { "npm install", "npm run dev" }, tab.CommandHistory);
        Assert.Equal(new[] { @"C:\projekte", @"C:\projekte\app" }, tab.DirectoryHistory);
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

        // Act - Execute commands in session
        session.TriggerCommandExecuted("Get-ChildItem");
        session.TriggerCommandExecuted("git status");
        session.TriggerDirectoryChange(@"C:\projekte\demo");

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
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Act 1 - Open modal
        mainVm.OpenProfilesModal();
        Assert.True(mainVm.IsProfilesModalOpen);

        // Act 2 - Start new profile
        mainVm.StartNewProfile();
        Assert.True(mainVm.IsEditingProfile);
        Assert.True(mainVm.IsCreatingNewProfile);

        mainVm.EditingProfileName = "My Custom Shell";
        mainVm.EditingExecutablePath = @"C:\custom\shell.exe";
        mainVm.EditingIconTag = "CSH";

        await mainVm.SaveProfileAsync();

        // Assert - editing reset, profile added
        Assert.False(mainVm.IsEditingProfile);
        Assert.Contains(mainVm.Profiles, p => p.Name == "My Custom Shell" && p.IconTag == "CSH");

        // Act 3 - Close modal
        mainVm.CloseProfilesModal();
        Assert.False(mainVm.IsProfilesModalOpen);
    }

    [Fact]
    public async Task TabCycling_NextAndPreviousWithWrapAround_WorksProperly()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Create 3 tabs
        mainVm.AddNewTab();
        mainVm.AddNewTab();
        Assert.Equal(3, mainVm.Tabs.Count);

        mainVm.SelectedTab = mainVm.Tabs[0];

        // Act & Assert 1: Next tab
        mainVm.CycleNextTab();
        Assert.Same(mainVm.Tabs[1], mainVm.SelectedTab);

        mainVm.CycleNextTab();
        Assert.Same(mainVm.Tabs[2], mainVm.SelectedTab);

        // Wrap around to first
        mainVm.CycleNextTab();
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);

        // Cycle previous wraps around to last
        mainVm.CyclePreviousTab();
        Assert.Same(mainVm.Tabs[2], mainVm.SelectedTab);

        mainVm.CyclePreviousTab();
        Assert.Same(mainVm.Tabs[1], mainVm.SelectedTab);
    }

    [Fact]
    public async Task SelectTabByIndex_DirectJumpsAndLastTab_WorksProperly()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Create 5 tabs
        mainVm.AddNewTab();
        mainVm.AddNewTab();
        mainVm.AddNewTab();
        mainVm.AddNewTab();
        Assert.Equal(5, mainVm.Tabs.Count);

        // Act & Assert: Select index 2 (3rd tab)
        mainVm.SelectTabByIndex(2);
        Assert.Same(mainVm.Tabs[2], mainVm.SelectedTab);

        // Select index 0 (1st tab)
        mainVm.SelectTabByIndex(0);
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);

        // Select index -1 (last tab)
        mainVm.SelectTabByIndex(-1);
        Assert.Same(mainVm.Tabs[4], mainVm.SelectedTab);

        // Out of bounds does nothing
        mainVm.SelectTabByIndex(99);
        Assert.Same(mainVm.Tabs[4], mainVm.SelectedTab);
    }

    [Fact]
    public async Task MoveSelectedTab_ReordersTabsCorrectly()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTab();
        mainVm.AddNewTab();
        Assert.Equal(3, mainVm.Tabs.Count);

        var tab0 = mainVm.Tabs[0];
        var tab1 = mainVm.Tabs[1];
        var tab2 = mainVm.Tabs[2];

        mainVm.SelectedTab = tab1;

        // Move right (+1)
        mainVm.MoveSelectedTab(1);
        Assert.Equal(3, mainVm.Tabs.Count);
        Assert.Same(tab0, mainVm.Tabs[0]);
        Assert.Same(tab2, mainVm.Tabs[1]);
        Assert.Same(tab1, mainVm.Tabs[2]);
        Assert.Same(tab1, mainVm.SelectedTab);

        // Move right again (already at end) -> no change
        mainVm.MoveSelectedTab(1);
        Assert.Same(tab1, mainVm.Tabs[2]);

        // Move left twice (-1, -1) -> moves to index 0
        mainVm.MoveSelectedTab(-1);
        Assert.Same(tab1, mainVm.Tabs[1]);
        mainVm.MoveSelectedTab(-1);
        Assert.Same(tab1, mainVm.Tabs[0]);
    }

    [Fact]
    public async Task CloseSelectedTab_ClosesActiveTabAndSelectsAdjacent()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTab();
        mainVm.AddNewTab();
        Assert.Equal(3, mainVm.Tabs.Count);

        var tab1 = mainVm.Tabs[1];
        mainVm.SelectedTab = tab1;

        // Act
        mainVm.CloseSelectedTab();

        // Assert
        Assert.Equal(2, mainVm.Tabs.Count);
        Assert.DoesNotContain(tab1, mainVm.Tabs);
        Assert.NotNull(mainVm.SelectedTab);
    }
}
