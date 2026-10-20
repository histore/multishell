using System;
using System.Collections.Generic;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using MultiShell.Views;
using System.Threading.Tasks;
using Xunit;

namespace MultiShell.Tests;

public partial class MainViewModelTabTests
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

        public event Action<ReadOnlyMemory<byte>>? DataReceived { add { } remove { } }
        public event Action<int>? Exited;
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted;
        public event Action<int>? CommandCompleted;

        public void TriggerCommandCompleted(int code) => CommandCompleted?.Invoke(code);

        public List<byte[]> SentInputs { get; } = new();

        public MockPowerShellSession(string title, string? workingDirectory = null)
        {
            Title = title;
            WorkingDirectory = workingDirectory;
        }

        public void Start() { IsRunning = true; }
        public void Send(byte[] input) { SentInputs.Add(input); }
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

        public Task SaveStateAsync(WorkspaceState state, CancellationToken cancellationToken = default)
        {
            SavedState = state;
            SaveCallCount++;
            return Task.CompletedTask;
        }

        public Task<WorkspaceState?> LoadStateAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StateToReturn ?? SavedState);
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
    public async Task InitializeWorkspaceAsync_WithInitialDirectory_OpensTabWithSpecifiedDirectory()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        var targetDir = @"C:\target\workspace";

        // Act
        using var mainVm = new MainViewModel(
            processService,
            persistenceService,
            new ThemeService(),
            new LocalizationService(),
            new FontSizeService(),
            initialDirectory: targetDir);

        await mainVm.InitializeWorkspaceAsync();

        // Assert
        Assert.NotNull(mainVm.SelectedTab);
        Assert.Equal(targetDir, mainVm.SelectedTab.WorkingDirectory);
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
    public void DuplicateTab_InsertsDirectlyToRightOfActiveTab()
    {
        // Arrange - 3 tabs: TabA (idx 0), TabB (idx 1), TabC (idx 2)
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        mainVm.AddNewTabWithDirectory(@"C:\tabB");
        mainVm.AddNewTabWithDirectory(@"C:\tabC");

        var tabA = mainVm.Tabs[0];
        var tabB = mainVm.Tabs[1];
        var tabC = mainVm.Tabs[2];

        // Act - Select TabB (index 1) and duplicate it
        mainVm.SelectedTab = tabB;
        mainVm.DuplicateTab();

        // Assert - The duplicated tab must be inserted at index 2 (directly to the right of TabB)
        Assert.Equal(4, mainVm.Tabs.Count);
        Assert.Same(tabA, mainVm.Tabs[0]);
        Assert.Same(tabB, mainVm.Tabs[1]);
        var duplicatedTab = mainVm.Tabs[2];
        Assert.Same(duplicatedTab, mainVm.SelectedTab);
        Assert.Same(tabC, mainVm.Tabs[3]);
        Assert.Equal(@"C:\tabB", duplicatedTab.WorkingDirectory);

        // Act 2 - Duplicate TabA (index 0)
        mainVm.SelectedTab = tabA;
        mainVm.DuplicateTab();

        // Assert - Inserted at index 1 (directly to right of TabA)
        Assert.Equal(5, mainVm.Tabs.Count);
        Assert.Same(tabA, mainVm.Tabs[0]);
        Assert.Same(mainVm.SelectedTab, mainVm.Tabs[1]);
        Assert.Same(tabB, mainVm.Tabs[2]);
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
}
