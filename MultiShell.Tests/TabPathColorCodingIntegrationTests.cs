using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

/// <summary>
/// Integration tests for REQ-TAB-025: Dynamic Tab Path Color Stripes Coding.
/// </summary>
public class TabPathColorCodingIntegrationTests
{
    [Fact]
    public void TabViewModel_InitialPathColorStripes_IsEmptyAndHasFlagFalse()
    {
        var session = new MockShellSession("Test");
        using var tabVm = new TerminalTabViewModel(session);

        Assert.Empty(tabVm.PathColorStripes);
        Assert.False(tabVm.HasPathColorStripes);
    }

    [Fact]
    public void TabViewModel_SettingPathColorStripes_UpdatesHasPathColorStripes()
    {
        var session = new MockShellSession("Test");
        using var tabVm = new TerminalTabViewModel(session);

        var stripe = new PathColorStripe("multishell", "#6496C8");
        tabVm.PathColorStripes = new[] { stripe };

        Assert.Single(tabVm.PathColorStripes);
        Assert.True(tabVm.HasPathColorStripes);
        Assert.Equal("multishell", tabVm.PathColorStripes[0].FolderName);
    }

    [Fact]
    public void MainViewModel_MultipleTabs_DynamicallyCalculatesAndAppliesStripes()
    {
        var processService = new FakeShellProcessService();
        var persistenceService = new FakePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());

        // Open two tabs with C:\xxx and C:\yyy
        mainVm.CloseTab(mainVm.Tabs[0]);
        mainVm.AddNewTabWithDirectory(@"C:\xxx");
        mainVm.AddNewTabWithDirectory(@"C:\yyy");

        Assert.Equal(2, mainVm.Tabs.Count);

        var tab1 = mainVm.Tabs[0];
        var tab2 = mainVm.Tabs[1];

        // Drive C: is the common prefix and is omitted; both tabs show their folder stripes
        Assert.Single(tab1.PathColorStripes);
        Assert.Equal("xxx", tab1.PathColorStripes[0].FolderName);

        Assert.Single(tab2.PathColorStripes);
        Assert.Equal("yyy", tab2.PathColorStripes[0].FolderName);
    }

    [Fact]
    public void MainViewModel_DirectoryChanged_OnlyUpdatesNavigatingTab_OtherTabsRemainStable()
    {
        var processService = new FakeShellProcessService();
        var persistenceService = new FakePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());

        mainVm.CloseTab(mainVm.Tabs[0]);
        mainVm.AddNewTabWithDirectory(@"C:\dt\projectA");
        mainVm.AddNewTabWithDirectory(@"C:\dt\projectB");

        var tab1 = mainVm.Tabs[0];
        var tab2 = mainVm.Tabs[1];

        // Initially both tabs display all their folders
        Assert.Equal(2, tab1.PathColorStripes.Count);
        Assert.Equal("dt", tab1.PathColorStripes[0].FolderName);
        Assert.Equal("projectA", tab1.PathColorStripes[1].FolderName);

        Assert.Equal(2, tab2.PathColorStripes.Count);
        Assert.Equal("dt", tab2.PathColorStripes[0].FolderName);
        Assert.Equal("projectB", tab2.PathColorStripes[1].FolderName);

        // When tab2 navigates into projectA\sub
        var session2 = processService.CreatedSessions.Last();
        session2.TriggerDirectoryChanged(@"C:\dt\projectA\sub");

        // Tab1 remains completely stable (still 2 stripes, same folders and colors)
        Assert.Equal(2, tab1.PathColorStripes.Count);
        Assert.Equal("dt", tab1.PathColorStripes[0].FolderName);
        Assert.Equal("projectA", tab1.PathColorStripes[1].FolderName);

        // Tab2 updates its own stripes to reflect its new directory
        Assert.Equal(3, tab2.PathColorStripes.Count);
        Assert.Equal("dt", tab2.PathColorStripes[0].FolderName);
        Assert.Equal("projectA", tab2.PathColorStripes[1].FolderName);
        Assert.Equal("sub", tab2.PathColorStripes[2].FolderName);
    }

    [Fact]
    public void MainViewModel_ClosingTab_LeavesRemainingTabsCompletelyUnchanged()
    {
        var processService = new FakeShellProcessService();
        var persistenceService = new FakePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());

        mainVm.CloseTab(mainVm.Tabs[0]);
        mainVm.AddNewTabWithDirectory(@"C:\dt\projectA\Services");
        mainVm.AddNewTabWithDirectory(@"C:\dt\projectA\Core");
        mainVm.AddNewTabWithDirectory(@"D:\tools");

        var tab1 = mainVm.Tabs[0];
        var tab2 = mainVm.Tabs[1];
        var tab3 = mainVm.Tabs[2];

        Assert.Equal(3, tab1.PathColorStripes.Count);
        Assert.Equal("dt", tab1.PathColorStripes[0].FolderName);
        Assert.Equal("projectA", tab1.PathColorStripes[1].FolderName);
        Assert.Equal("Services", tab1.PathColorStripes[2].FolderName);

        Assert.Equal(3, tab2.PathColorStripes.Count);
        Assert.Equal("dt", tab2.PathColorStripes[0].FolderName);
        Assert.Equal("projectA", tab2.PathColorStripes[1].FolderName);
        Assert.Equal("Core", tab2.PathColorStripes[2].FolderName);

        Assert.Single(tab3.PathColorStripes);
        Assert.Equal("tools", tab3.PathColorStripes[0].FolderName);

        // Close the D:\tools tab
        mainVm.CloseTab(tab3);

        // Tab1 and Tab2 remain completely unchanged
        Assert.Equal(3, tab1.PathColorStripes.Count);
        Assert.Equal("dt", tab1.PathColorStripes[0].FolderName);
        Assert.Equal("projectA", tab1.PathColorStripes[1].FolderName);
        Assert.Equal("Services", tab1.PathColorStripes[2].FolderName);

        Assert.Equal(3, tab2.PathColorStripes.Count);
        Assert.Equal("dt", tab2.PathColorStripes[0].FolderName);
        Assert.Equal("projectA", tab2.PathColorStripes[1].FolderName);
        Assert.Equal("Core", tab2.PathColorStripes[2].FolderName);
    }

    [Fact]
    public void MainViewModel_CloseTab_DoesNotReallocateStripeObjectsOnRemainingTabs()
    {
        var processService = new FakeShellProcessService();
        var persistenceService = new FakePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());

        mainVm.CloseTab(mainVm.Tabs[0]);
        mainVm.AddNewTabWithDirectory(@"C:\dt\projectA");
        mainVm.AddNewTabWithDirectory(@"C:\dt\projectB");

        var tab1 = mainVm.Tabs[0];
        var tab2 = mainVm.Tabs[1];

        var tab1OriginalStripes = tab1.PathColorStripes;

        // Close second tab
        mainVm.CloseTab(tab2);

        // Assert tab1 stripes reference remains strictly identical (no object churn or reallocation)
        Assert.Same(tab1OriginalStripes, tab1.PathColorStripes);
    }

    private class MockShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; set; }
        public string? WorkingDirectory { get; set; }
        public ShellType ShellType { get; set; } = ShellType.PowerShell;
        public bool IsRunning { get; set; } = true;

#pragma warning disable CS0067
        public event Action<byte[]>? DataReceived;
        public event Action<int>? Exited;
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted;
#pragma warning restore CS0067

        public MockShellSession(string title, string? workingDirectory = null)
        {
            Title = title;
            WorkingDirectory = workingDirectory;
        }

        public void TriggerDirectoryChanged(string newDir)
        {
            WorkingDirectory = newDir;
            WorkingDirectoryChanged?.Invoke(newDir);
        }

        public void Start() { }
        public void Send(byte[] input) { }
        public void Resize(int cols, int rows) { }
        public void Dispose() { IsRunning = false; }
    }

    private class FakeShellProcessService : IShellProcessService
    {
        public List<MockShellSession> CreatedSessions { get; } = new();

        public IShellSession CreateSession(
            string title,
            string? workingDirectory = null,
            ShellType shellType = ShellType.PowerShell,
            string? customExecutable = null,
            string? customArguments = null)
        {
            var session = new MockShellSession(title, workingDirectory) { ShellType = shellType };
            CreatedSessions.Add(session);
            return session;
        }
    }

    private class FakePersistenceService : ITabStatePersistenceService
    {
        public Task SaveStateAsync(WorkspaceState state, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkspaceState?> LoadStateAsync(CancellationToken cancellationToken = default) => Task.FromResult<WorkspaceState?>(null);
    }
}
