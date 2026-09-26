using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using Avalonia.Media;
using Xunit;

namespace MultiShell.Tests;

/// <summary>
/// Unit tests for REQ-TAB-020: Custom Tab Renaming & Tab Color Palette Tagging.
/// </summary>
public class TabRenamingAndColorTests : IDisposable
{
    private readonly string _tempFile;

    public TabRenamingAndColorTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"multishell_tab_rename_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    [Fact]
    public void InitialState_CustomTitleAndTabColorAreNull_FlagsAreFalse()
    {
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        Assert.Null(vm.CustomTitle);
        Assert.Null(vm.TabColor);
        Assert.Null(vm.TabColorBrush);
        Assert.False(vm.HasCustomTitle);
        Assert.False(vm.HasTabColor);
        Assert.False(vm.IsRenaming);
        Assert.Equal(string.Empty, vm.RenameBuffer);
        Assert.Equal("PS 1", vm.DisplayTitle);
    }

    [Fact]
    public void StartRenaming_SetsIsRenamingTrue_InitializesRenameBuffer_AndFiresFocusEvent()
    {
        var session = new MockPowerShellSession("My Tab");
        using var vm = new TerminalTabViewModel(session);

        bool focusRequested = false;
        vm.FocusRenameBoxRequested += () => focusRequested = true;

        vm.StartRenaming();

        Assert.True(vm.IsRenaming);
        Assert.Equal("My Tab", vm.RenameBuffer);
        Assert.True(focusRequested);
    }

    [Fact]
    public void CommitRenaming_WithValidText_SetsCustomTitleAndUpdatesDisplayTitle()
    {
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        vm.StartRenaming();
        vm.RenameBuffer = "Production Server";
        vm.CommitRenaming();

        Assert.False(vm.IsRenaming);
        Assert.Equal("Production Server", vm.CustomTitle);
        Assert.True(vm.HasCustomTitle);
        Assert.Equal("Production Server", vm.DisplayTitle);
    }

    [Fact]
    public void CommitRenaming_WithWhitespaceOrEmpty_ClearsCustomTitle_FallsBackToDefaultTitle()
    {
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        vm.CustomTitle = "Previous Title";
        Assert.Equal("Previous Title", vm.DisplayTitle);

        vm.StartRenaming();
        vm.RenameBuffer = "   ";
        vm.CommitRenaming();

        Assert.False(vm.IsRenaming);
        Assert.Null(vm.CustomTitle);
        Assert.False(vm.HasCustomTitle);
        Assert.Equal("PS 1", vm.DisplayTitle);
    }

    [Fact]
    public void CancelRenaming_PreservesPreviousCustomTitle_ResetsIsRenaming()
    {
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);
        vm.CustomTitle = "Original Name";

        vm.StartRenaming();
        vm.RenameBuffer = "Draft Name";
        vm.CancelRenaming();

        Assert.False(vm.IsRenaming);
        Assert.Equal("Original Name", vm.CustomTitle);
        Assert.Equal("Original Name", vm.DisplayTitle);
    }

    [Fact]
    public void ResetCustomTitle_ClearsCustomTitle_RestoresAutoTitle()
    {
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);
        vm.CustomTitle = "Custom Name";
        Assert.True(vm.HasCustomTitle);

        vm.ResetCustomTitle();

        Assert.Null(vm.CustomTitle);
        Assert.False(vm.HasCustomTitle);
        Assert.Equal("PS 1", vm.DisplayTitle);
    }

    [Fact]
    public void SetTabColor_WithHexCode_UpdatesTabColorAndTabColorBrush()
    {
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        vm.SetTabColor("#F7768E");

        Assert.Equal("#F7768E", vm.TabColor);
        Assert.True(vm.HasTabColor);
        Assert.NotNull(vm.TabColorBrush);
    }

    [Fact]
    public void SetTabColor_WithNullOrWhitespace_ClearsTabColor()
    {
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);
        vm.SetTabColor("#9ECE6A");
        Assert.True(vm.HasTabColor);

        vm.SetTabColor("   ");

        Assert.Null(vm.TabColor);
        Assert.False(vm.HasTabColor);
        Assert.Null(vm.TabColorBrush);
    }

    [Fact]
    public void CloseOtherTabs_ClosesAllExceptTargetTab()
    {
        var processService = new FakePowerShellProcessService();
        var persistence = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistence, new ThemeService(), new LocalizationService(), new FontSizeService());

        mainVm.AddNewTabCommand.Execute(null); // Tab 2
        mainVm.AddNewTabCommand.Execute(null); // Tab 3
        Assert.Equal(3, mainVm.Tabs.Count);

        var targetTab = mainVm.Tabs[1];
        mainVm.CloseOtherTabsCommand.Execute(targetTab);

        Assert.Single(mainVm.Tabs);
        Assert.Same(targetTab, mainVm.Tabs[0]);
    }

    [Fact]
    public void CloseTabsToRight_ClosesOnlyTabsToRightOfTarget()
    {
        var processService = new FakePowerShellProcessService();
        var persistence = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistence, new ThemeService(), new LocalizationService(), new FontSizeService());

        mainVm.AddNewTabCommand.Execute(null); // Tab 2
        mainVm.AddNewTabCommand.Execute(null); // Tab 3
        mainVm.AddNewTabCommand.Execute(null); // Tab 4
        Assert.Equal(4, mainVm.Tabs.Count);

        var targetTab = mainVm.Tabs[1];
        mainVm.CloseTabsToRightCommand.Execute(targetTab);

        Assert.Equal(2, mainVm.Tabs.Count);
        Assert.Same(targetTab, mainVm.Tabs[1]);
    }

    [Fact]
    public async Task TabStatePersistence_SavesAndRestores_CustomTitleAndTabColor()
    {
        var service = new TabStatePersistenceService(_tempFile);
        var originalState = new WorkspaceState(
            new List<TabState>
            {
                new("PS 1", @"C:\projects\repo", CustomTitle: "Dev Environment", TabColor: "#7AA2F7"),
                new("PS 2", @"C:\projects\docs", CustomTitle: null, TabColor: "#9ECE6A")
            },
            0);

        await service.SaveStateAsync(originalState);
        var loaded = await service.LoadStateAsync();

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Tabs.Count);
        Assert.Equal("Dev Environment", loaded.Tabs[0].CustomTitle);
        Assert.Equal("#7AA2F7", loaded.Tabs[0].TabColor);
        Assert.Null(loaded.Tabs[1].CustomTitle);
        Assert.Equal("#9ECE6A", loaded.Tabs[1].TabColor);
    }

    [Fact]
    public void ClosedTabItemViewModel_PreservesCustomTitleAndTabColor_OnRoundTrip()
    {
        var originalTabState = new TabState(
            "PS 1",
            @"C:\projects",
            new List<string> { "git status" },
            new List<string> { @"C:\projects" },
            ShellType.PowerShell,
            CustomTitle: "Custom Name",
            TabColor: "#BB9AF7");

        var closedItem = ClosedTabItemViewModel.FromTabState(originalTabState);

        Assert.Equal("Custom Name", closedItem.CustomTitle);
        Assert.Equal("#BB9AF7", closedItem.TabColor);

        var restoredState = closedItem.ToTabState();
        Assert.Equal("Custom Name", restoredState.CustomTitle);
        Assert.Equal("#BB9AF7", restoredState.TabColor);
    }

    private class MockPowerShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; }
        public string? WorkingDirectory { get; set; }
        public ShellType ShellType { get; set; } = ShellType.PowerShell;
        public bool IsRunning { get; set; }
        public bool Disposed { get; private set; }
        public bool Started { get; private set; }

#pragma warning disable CS0067
        public event Action<byte[]>? DataReceived;
        public event Action<int>? Exited;
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted;
#pragma warning restore CS0067

        public MockPowerShellSession(string title, string? workingDirectory = null)
        {
            Title = title;
            WorkingDirectory = workingDirectory;
        }

        public void Start() => IsRunning = true;
        public void Send(byte[] input) { }
        public void Resize(int cols, int rows) { }
        public void Dispose() => Disposed = true;
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
        public int SaveCallCount { get; private set; }

        public Task SaveStateAsync(WorkspaceState state)
        {
            SavedState = state;
            SaveCallCount++;
            return Task.CompletedTask;
        }

        public Task<WorkspaceState?> LoadStateAsync()
        {
            return Task.FromResult(SavedState);
        }
    }
}
