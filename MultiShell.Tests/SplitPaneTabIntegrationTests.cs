using System;
using System.Linq;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using MultiShell.Views;
using Xunit;

namespace MultiShell.Tests;

public class SplitPaneTabIntegrationTests
{
    private class DummyShellService : IShellProcessService
    {
        public IShellSession CreateSession(
            string title,
            string? workingDirectory = null,
            ShellType shellType = ShellType.PowerShell,
            string? customExecutable = null,
            string? customArguments = null)
        {
            return new DummySession(title, workingDirectory, shellType);
        }
    }

    private class DummySession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; set; }
        public string? WorkingDirectory { get; set; }
        public ShellType ShellType { get; set; }
        public bool IsRunning { get; set; } = true;

#pragma warning disable CS0067
        public event Action<ReadOnlyMemory<byte>>? DataReceived;
        public event Action<int>? Exited;
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted;
        public event Action<int>? CommandCompleted;
#pragma warning restore CS0067

        public System.Collections.Generic.List<byte[]> SentInputs { get; } = new();

        public DummySession(string title, string? workingDir, ShellType shellType)
        {
            Title = title;
            WorkingDirectory = workingDir;
            ShellType = shellType;
        }

        public void Start() => IsRunning = true;
        public void Send(byte[] input) => SentInputs.Add(input);
        public void Resize(int cols, int rows) { }
        public void SimulateExit(int exitCode = 0)
        {
            IsRunning = false;
            Exited?.Invoke(exitCode);
        }
        public void Dispose() => IsRunning = false;
    }

    private class MockEmptyPersistenceService : ITabStatePersistenceService
    {
        public Task<WorkspaceState?> LoadStateAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkspaceState?>(null);

        public Task SaveStateAsync(WorkspaceState state, System.Threading.CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public void MainViewModel_CloseActivePaneOrTab_ClosesPaneWhenMultipleExist()
    {
        var shellService = new DummyShellService();
        var mainVm = new MainViewModel(shellService, new MockEmptyPersistenceService(), new ThemeService());

        Assert.Single(mainVm.Tabs);
        var tab = mainVm.SelectedTab!;
        tab.SplitRight();

        Assert.Equal(2, tab.PaneCount);
        Assert.Single(mainVm.Tabs);

        // Act: Should close only the active pane, NOT the tab
        mainVm.CloseActivePaneOrTab();

        Assert.Equal(1, tab.PaneCount);
        Assert.Single(mainVm.Tabs);
        Assert.Same(tab, mainVm.SelectedTab);

        // Act: Second close closes the last pane and therefore the tab
        mainVm.CloseActivePaneOrTab();

        Assert.Empty(mainVm.Tabs);
        Assert.Null(mainVm.SelectedTab);
    }

    [Fact]
    public void MainViewModel_SplitCommands_OperateOnSelectedTab()
    {
        var shellService = new DummyShellService();
        var mainVm = new MainViewModel(shellService, new MockEmptyPersistenceService(), new ThemeService());
        Assert.Single(mainVm.Tabs);
        var tab = mainVm.SelectedTab!;

        mainVm.SplitActiveTabRight();
        Assert.Equal(2, tab.PaneCount);

        mainVm.SplitActiveTabDown();
        Assert.Equal(3, tab.PaneCount);

        mainVm.FocusNextPaneActiveTab();
        Assert.True(tab.ActivePane.IsActive);

        mainVm.ToggleZoomActiveTab();
        Assert.True(tab.IsZoomed);

        mainVm.ToggleZoomActiveTab();
        Assert.False(tab.IsZoomed);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("it")]
    [InlineData("pt")]
    public void Localization_SplitPaneKeys_AreDefinedInAllSupportedLanguages(string langCode)
    {
        var loc = new LocalizationService(langCode);

        Assert.False(string.IsNullOrWhiteSpace(loc["Pane_Split_Right"]));
        Assert.False(string.IsNullOrWhiteSpace(loc["Pane_Split_Down"]));
        Assert.False(string.IsNullOrWhiteSpace(loc["Pane_Zoom"]));
        Assert.False(string.IsNullOrWhiteSpace(loc["Pane_Close"]));
        Assert.False(string.IsNullOrWhiteSpace(loc["Menu_Pane_SplitRight"]));
        Assert.False(string.IsNullOrWhiteSpace(loc["Menu_Pane_SplitDown"]));
        Assert.False(string.IsNullOrWhiteSpace(loc["Menu_Pane_ToggleZoom"]));
    }

    [Fact]
    public void SplitTab_InitialPaneExit_CollapsesPaneWithoutClosingSurvivingTab()
    {
        var initialSession = new DummySession("Initial", @"C:\initial", ShellType.PowerShell);
        var splitSession = new DummySession("Split", @"C:\split", ShellType.PowerShell);
        using var tab = new TerminalTabViewModel(initialSession);
        var initialPane = tab.ActivePane;
        var splitPane = tab.SplitPane(initialPane, PaneSplitDirection.Vertical, splitSession);

        int closeRequestedCount = 0;
        tab.CloseRequested += _ => closeRequestedCount++;

        // Act: Initial pane shell exits while split pane is still running
        initialSession.SimulateExit(0);

        // Assert: Tab stays open with surviving pane active and receiving input
        Assert.Equal(0, closeRequestedCount);
        Assert.Equal(1, tab.PaneCount);
        Assert.Same(splitPane, tab.ActivePane);
        Assert.True(initialPane.IsDisposed);
        Assert.True(tab.IsRunning);

        tab.ExecuteHistoryCommand("echo alive");
        Assert.NotEmpty(splitSession.SentInputs);
    }

    [Fact]
    public void SplitTab_ManualCloseOfInitialPane_KeepsTabRunningAndRoutesInputToSurvivingPane()
    {
        var initialSession = new DummySession("Initial", @"C:\initial", ShellType.PowerShell);
        var splitSession = new DummySession("Split", @"C:\split", ShellType.CMD);
        using var tab = new TerminalTabViewModel(initialSession);
        var initialPane = tab.ActivePane;
        var splitPane = tab.SplitPane(initialPane, PaneSplitDirection.Horizontal, splitSession);

        tab.ClosePane(initialPane);

        Assert.Equal(1, tab.PaneCount);
        Assert.Same(splitPane, tab.ActivePane);
        Assert.True(initialPane.IsDisposed);
        Assert.True(tab.IsRunning);
        Assert.Equal(ShellType.CMD, tab.ShellType);

        tab.SendInput("dir\r"u8.ToArray());
        Assert.Single(splitSession.SentInputs);
    }

    [Fact]
    public void SinglePaneTab_SessionExit_FiresCloseRequestedExactlyOnce()
    {
        var session = new DummySession("Solo", @"C:\solo", ShellType.PowerShell);
        using var tab = new TerminalTabViewModel(session);
        int closeRequestedCount = 0;
        tab.CloseRequested += _ => closeRequestedCount++;

        session.SimulateExit(0);

        Assert.Equal(1, closeRequestedCount);
        Assert.False(tab.IsRunning);
    }

    [Fact]
    public void TerminalTab_Dispose_DisposesInitialPaneAsWellAsSplitPanes()
    {
        var initialSession = new DummySession("Initial", @"C:\initial", ShellType.PowerShell);
        var splitSession = new DummySession("Split", @"C:\split", ShellType.PowerShell);
        var tab = new TerminalTabViewModel(initialSession);
        var initialPane = tab.ActivePane;
        var splitPane = tab.SplitPane(initialPane, PaneSplitDirection.Vertical, splitSession);

        tab.Dispose();

        Assert.True(tab.IsDisposed);
        Assert.True(initialPane.IsDisposed);
        Assert.True(splitPane.IsDisposed);
    }
}
