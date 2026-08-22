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


    private class MockPowerShellSession : IPowerShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; }
        public string? WorkingDirectory { get; set; }
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

    private class FakePowerShellProcessService : IPowerShellProcessService
    {
        public List<MockPowerShellSession> CreatedSessions { get; } = new();

        public IPowerShellSession CreateSession(string title, string? workingDirectory = null)
        {
            var session = new MockPowerShellSession(title, workingDirectory);
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
        using var mainVm = new MainViewModel(processService, persistenceService);

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
        using var mainVm = new MainViewModel(processService, persistenceService);
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
        using var mainVm = new MainViewModel(processService, persistenceService);
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
        using var mainVm = new MainViewModel(processService, persistenceService);

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
        using var mainVm = new MainViewModel(processService, persistenceService);
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
        using var mainVm = new MainViewModel(processService, persistenceService);
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
        using var mainVm = new MainViewModel(processService, persistenceService);
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
        using var mainVm = new MainViewModel(processService, persistenceService);
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
        using var mainVm = new MainViewModel(processService, persistenceService);
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
        using var mainVm = new MainViewModel(processService, persistenceService);
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
        using var mainVm = new MainViewModel(processService, persistenceService);
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
        using var mainVm = new MainViewModel(processService, persistenceService);
        mainVm.AddNewTab();

        // Act
        mainVm.SelectTab(mainVm.Tabs[0]);

        // Assert
        Assert.Equal("PS 1", mainVm.SelectedTab?.Title);
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
        using var mainVm = new MainViewModel(processService, persistenceService);
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
        using var mainVm = new MainViewModel(processService, persistenceService);
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
        using var mainVm = new MainViewModel(processService, persistenceService);
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
}
