using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public class SharedDirectoryHistoryIntegrationTests
{
    private class TestPowerShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; }
        public string? WorkingDirectory { get; set; }
        public ShellType ShellType { get; set; } = ShellType.PowerShell;
        public bool IsRunning { get; set; } = true;

        public event Action<byte[]>? DataReceived { add { } remove { } }
        public event Action<int>? Exited { add { } remove { } }
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted { add { } remove { } }

        public TestPowerShellSession(string title, string? workingDirectory = null)
        {
            Title = title;
            WorkingDirectory = workingDirectory;
        }

        public void Start() { IsRunning = true; }
        public void Send(byte[] input) { }
        public void Resize(int cols, int rows) { }

        public void TriggerDirectoryChange(string newDir)
        {
            WorkingDirectory = newDir;
            WorkingDirectoryChanged?.Invoke(newDir);
        }

        public void Dispose()
        {
            IsRunning = false;
        }
    }

    private class TestShellProcessService : IShellProcessService
    {
        public List<TestPowerShellSession> CreatedSessions { get; } = new();

        public IShellSession CreateSession(
            string title,
            string? workingDirectory = null,
            ShellType shellType = ShellType.PowerShell,
            string? customExecutable = null,
            string? customArguments = null)
        {
            var session = new TestPowerShellSession(title, workingDirectory) { ShellType = shellType };
            CreatedSessions.Add(session);
            return session;
        }
    }

    private class TestTabStatePersistenceService : ITabStatePersistenceService
    {
        public WorkspaceState? SavedState { get; set; }

        public Task SaveStateAsync(WorkspaceState state)
        {
            SavedState = state;
            return Task.CompletedTask;
        }

        public Task<WorkspaceState?> LoadStateAsync()
        {
            return Task.FromResult(SavedState);
        }
    }

    [Fact]
    public void MultipleTabs_ShareSameDirectoryHistoryService_SyncsImmediately()
    {
        // Arrange
        var directoryService = new DirectoryHistoryService();
        var session1 = new TestPowerShellSession("Tab 1", @"C:\projekte\start1");
        var session2 = new TestPowerShellSession("Tab 2", @"C:\projekte\start2");

        using var tab1 = new TerminalTabViewModel(session1, directoryHistoryService: directoryService);
        using var tab2 = new TerminalTabViewModel(session2, directoryHistoryService: directoryService);

        // Both tabs should contain initial directories
        Assert.Contains(@"C:\projekte\start1", tab1.DirectoryHistory);
        Assert.Contains(@"C:\projekte\start2", tab1.DirectoryHistory);
        Assert.Contains(@"C:\projekte\start1", tab2.DirectoryHistory);
        Assert.Contains(@"C:\projekte\start2", tab2.DirectoryHistory);

        // Act - Tab 1 changes directory
        session1.TriggerDirectoryChange(@"C:\projekte\newfolder");

        // Assert - Tab 2 must immediately reflect newfolder
        Assert.Contains(@"C:\projekte\newfolder", tab2.DirectoryHistory);
        Assert.Contains(@"C:\projekte\newfolder", tab2.FilteredDirectoryHistory);
        Assert.Equal(@"C:\projekte\newfolder", tab2.DirectoryHistory[^1]);
        Assert.Equal(@"C:\projekte\newfolder", tab1.DirectoryHistory[^1]);
    }

    [Fact]
    public void RevisitingExistingDirectory_FromAnotherTab_MovesToNewestPosition_InAllTabs()
    {
        // Arrange
        var directoryService = new DirectoryHistoryService();
        var session1 = new TestPowerShellSession("Tab 1", @"C:\projekte\first");
        var session2 = new TestPowerShellSession("Tab 2", @"C:\projekte\second");

        using var tab1 = new TerminalTabViewModel(session1, directoryHistoryService: directoryService);
        using var tab2 = new TerminalTabViewModel(session2, directoryHistoryService: directoryService);

        // Act - Tab 2 visits first folder again
        session2.TriggerDirectoryChange(@"C:\projekte\first");

        // Assert - Both tabs have exactly 2 directories, and "first" is at the end (newest), no duplicate
        Assert.Equal(2, tab1.DirectoryHistory.Count);
        Assert.Equal(2, tab2.DirectoryHistory.Count);
        Assert.Equal(@"C:\projekte\second", tab1.DirectoryHistory[0]);
        Assert.Equal(@"C:\projekte\first", tab1.DirectoryHistory[1]);
        Assert.Equal(@"C:\projekte\second", tab2.DirectoryHistory[0]);
        Assert.Equal(@"C:\projekte\first", tab2.DirectoryHistory[1]);
    }

    [Fact]
    public async Task WorkspaceState_PersistsAndRestores_SharedDirectoryHistory()
    {
        // Arrange
        var processService = new TestShellProcessService();
        var persistenceService = new TestTabStatePersistenceService
        {
            SavedState = new WorkspaceState(
                new List<TabState>
                {
                    new TabState("PS 1", @"C:\repo\start")
                },
                0,
                SharedDirectoryHistory: new List<string>
                {
                    @"C:\repo\folder1",
                    @"C:\repo\folder2",
                    @"C:\repo\folder3"
                })
        };

        using var mainVm = new MainViewModel(
            processService,
            persistenceService,
            new ThemeService(),
            new LocalizationService(),
            new FontSizeService());

        // Act
        await mainVm.InitializeWorkspaceAsync();

        // Assert - Tab has all shared directories restored
        Assert.Single(mainVm.Tabs);
        var tab = mainVm.Tabs[0];
        Assert.Contains(@"C:\repo\folder1", tab.DirectoryHistory);
        Assert.Contains(@"C:\repo\folder2", tab.DirectoryHistory);
        Assert.Contains(@"C:\repo\folder3", tab.DirectoryHistory);

        // Act 2 - Visit a new directory and save synchronously
        var session = processService.CreatedSessions[^1];
        session.TriggerDirectoryChange(@"C:\repo\folder4");
        await Task.Delay(50);
        mainVm.SaveCurrentStateSynchronously();

        // Assert - Saved state contains updated shared directories with folder4 at the end
        Assert.NotNull(persistenceService.SavedState);
        Assert.NotNull(persistenceService.SavedState.SharedDirectoryHistory);
        Assert.Contains(@"C:\repo\folder4", persistenceService.SavedState.SharedDirectoryHistory);
        Assert.Equal(@"C:\repo\folder4", persistenceService.SavedState.SharedDirectoryHistory[^1]);
    }
}
