using System;
using System.IO;
using System.Threading.Tasks;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public class WindowCoordinatorServiceTests
{
    private class MockShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; set; } = "TestSession";
        public string? WorkingDirectory { get; set; } = @"C:\Test";
        public ShellType ShellType => ShellType.PowerShell;
        public bool IsRunning { get; set; } = true;
        public bool IsDisposed { get; private set; }

        public event Action<ReadOnlyMemory<byte>>? DataReceived { add { } remove { } }
        public event Action<int>? Exited { add { } remove { } }
        public event Action<string>? WorkingDirectoryChanged { add { } remove { } }
        public event Action<string>? CommandExecuted { add { } remove { } }
        public event Action<int>? CommandCompleted { add { } remove { } }

        public void Start() { }
        public void Send(byte[] input) { }
        public void Resize(int cols, int rows) { }
        public void Dispose()
        {
            IsDisposed = true;
            IsRunning = false;
        }
    }

    private class MockShellProcessService : IShellProcessService
    {
        public IShellSession CreateSession(string title, string? workingDirectory, ShellType shellType, string? customExecutable = null, string? customArguments = null)
        {
            return new MockShellSession { Title = title, WorkingDirectory = workingDirectory };
        }
    }

    private class MockTabStatePersistenceService : ITabStatePersistenceService
    {
        public Task SaveStateAsync(WorkspaceState state, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkspaceState?> LoadStateAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult<WorkspaceState?>(null);
    }

    private class MockThemeService : IThemeService
    {
        public bool IsDarkAppTheme => true;
        public bool IsDarkTerminalTheme => true;
        public event Action<bool>? AppThemeChanged { add { } remove { } }
        public event Action<bool>? TerminalThemeChanged { add { } remove { } }
        public void SetAppTheme(bool isDark) { }
        public void SetTerminalTheme(bool isDark) { }
        public void ToggleAppTheme() { }
        public void ToggleTerminalTheme() { }
    }

    private static MainViewModel CreateTestViewModel(IWindowCoordinatorService coordinator)
    {
        return new MainViewModel(
            new MockShellProcessService(),
            new MockTabStatePersistenceService(),
            new MockThemeService(),
            new LocalizationService("en"),
            new FontSizeService(),
            windowCoordinator: coordinator);
    }

    [Fact]
    public void RegisterWindow_FirstWindow_IsPrimaryAndNumberOne()
    {
        var coordinator = new WindowCoordinatorService();
        using var vm = CreateTestViewModel(coordinator);

        coordinator.RegisterWindow(vm);

        Assert.True(vm.IsPrimary);
        Assert.Equal(1, vm.WindowNumber);
        Assert.Same(vm, coordinator.PrimaryWindow);
        Assert.Single(coordinator.Windows);
    }

    [Fact]
    public void RegisterWindow_SecondWindow_IsSecondaryAndNumberTwo()
    {
        var coordinator = new WindowCoordinatorService();
        using var primary = CreateTestViewModel(coordinator);
        using var secondary = CreateTestViewModel(coordinator);

        coordinator.RegisterWindow(primary);
        coordinator.RegisterWindow(secondary);

        Assert.True(primary.IsPrimary);
        Assert.Equal(1, primary.WindowNumber);

        Assert.False(secondary.IsPrimary);
        Assert.Equal(2, secondary.WindowNumber);
        Assert.Equal(2, coordinator.Windows.Count);
    }

    [Fact]
    public void UnregisterWindow_RemovesWindowFromCollection()
    {
        var coordinator = new WindowCoordinatorService();
        using var primary = CreateTestViewModel(coordinator);
        using var secondary = CreateTestViewModel(coordinator);

        coordinator.RegisterWindow(primary);
        coordinator.RegisterWindow(secondary);
        Assert.Equal(2, coordinator.Windows.Count);

        coordinator.UnregisterWindow(secondary);
        Assert.Single(coordinator.Windows);
        Assert.DoesNotContain(secondary, coordinator.Windows);
    }

    [Fact]
    public async Task UndockTabAsync_MovesTabToNewSecondaryWindow_ZeroPtyRestart()
    {
        var coordinator = new WindowCoordinatorService();
        using var primary = CreateTestViewModel(coordinator);
        coordinator.RegisterWindow(primary);

        primary.AddNewTab();
        Assert.Equal(2, primary.Tabs.Count);

        var tabToUndock = primary.Tabs[1];
        var session = (MockShellSession)tabToUndock.Session;

        MainViewModel? openedViewModel = null;
        coordinator.WindowOpenRequested += (_, args) =>
        {
            openedViewModel = args.ViewModel;
        };

        var newVm = await coordinator.UndockTabAsync(primary, tabToUndock);

        Assert.Single(primary.Tabs);
        Assert.DoesNotContain(tabToUndock, primary.Tabs);

        Assert.NotNull(openedViewModel);
        Assert.Same(newVm, openedViewModel);
        Assert.False(newVm.IsPrimary);
        Assert.Equal(2, newVm.WindowNumber);
        Assert.Single(newVm.Tabs);
        Assert.Same(tabToUndock, newVm.Tabs[0]);
        Assert.Same(tabToUndock, newVm.SelectedTab);

        Assert.False(session.IsDisposed);
    }

    [Fact]
    public async Task DockBackTabAsync_MovesTabBackToPrimaryWindow_AndClosesEmptySecondaryWindow()
    {
        var coordinator = new WindowCoordinatorService();
        using var primary = CreateTestViewModel(coordinator);
        coordinator.RegisterWindow(primary);

        using var secondary = CreateTestViewModel(coordinator);
        coordinator.RegisterWindow(secondary, isPrimary: false);

        var tabToDockBack = secondary.Tabs[0];

        MainViewModel? closedVm = null;
        coordinator.WindowCloseRequested += (_, args) =>
        {
            closedVm = args.ViewModel;
        };

        await coordinator.DockBackTabAsync(secondary, tabToDockBack);

        Assert.Empty(secondary.Tabs);
        Assert.Contains(tabToDockBack, primary.Tabs);
        Assert.Same(tabToDockBack, primary.SelectedTab);

        Assert.Same(secondary, closedVm);
    }

    [Fact]
    public async Task CloseWindowAsync_SecondaryWindow_ArchivesTabsAndKeepsPrimaryOpen()
    {
        var coordinator = new WindowCoordinatorService();
        using var primary = CreateTestViewModel(coordinator);
        coordinator.RegisterWindow(primary);

        using var secondary = CreateTestViewModel(coordinator);
        coordinator.RegisterWindow(secondary, isPrimary: false);
        var tabTitle = secondary.Tabs[0].Title;

        bool shutdownRequested = false;
        coordinator.ApplicationShutdownRequested += (_, _) => shutdownRequested = true;

        MainViewModel? closedVm = null;
        coordinator.WindowCloseRequested += (_, args) => closedVm = args.ViewModel;

        await coordinator.CloseWindowAsync(secondary);

        Assert.Same(secondary, closedVm);
        Assert.False(shutdownRequested);

        Assert.Single(primary.ClosedTabs);
        Assert.Equal(tabTitle, primary.ClosedTabs[0].Title);
    }

    [Fact]
    public async Task CloseWindowAsync_PrimaryWindow_SignalsApplicationShutdownAndClosesSecondaries()
    {
        var coordinator = new WindowCoordinatorService();
        using var primary = CreateTestViewModel(coordinator);
        coordinator.RegisterWindow(primary);

        using var secondary = CreateTestViewModel(coordinator);
        coordinator.RegisterWindow(secondary, isPrimary: false);

        bool shutdownRequested = false;
        coordinator.ApplicationShutdownRequested += (_, _) => shutdownRequested = true;

        MainViewModel? closedSecondary = null;
        coordinator.WindowCloseRequested += (_, args) => closedSecondary = args.ViewModel;

        await coordinator.CloseWindowAsync(primary);

        Assert.True(shutdownRequested);
        Assert.Same(secondary, closedSecondary);
    }

    [Fact]
    public void WindowTitle_FormattedWithSecondaryPrefix_WhenNotPrimary()
    {
        var coordinator = new WindowCoordinatorService();
        using var secondary = CreateTestViewModel(coordinator);
        coordinator.RegisterWindow(secondary, isPrimary: false);

        Assert.False(secondary.IsPrimary);
        Assert.StartsWith("[Fenster 2 ⧉] MultiShell", secondary.WindowTitle);
    }

    private class MockPreloadedPersistenceService : ITabStatePersistenceService
    {
        private readonly WorkspaceState _state;
        public MockPreloadedPersistenceService(WorkspaceState state) => _state = state;
        public Task SaveStateAsync(WorkspaceState state, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkspaceState?> LoadStateAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult<WorkspaceState?>(_state);
    }

    [Fact]
    public async Task InitializeWorkspaceAsync_RestoresPrimaryAndSecondaryWindows_FromState()
    {
        var coordinator = new WindowCoordinatorService();

        var state = new WorkspaceState(
            new List<TabState> { new("PrimaryTab", @"C:\primary") },
            0,
            Version: 2,
            SecondaryWindows: new List<WindowWorkspaceState>
            {
                new(2, new List<TabState> { new("SecondaryTab", @"C:\secondary") }, 0)
            });

        MainViewModel? openedSecondary = null;
        coordinator.WindowOpenRequested += (_, args) =>
        {
            openedSecondary = args.ViewModel;
        };

        using var primary = new MainViewModel(
            new MockShellProcessService(),
            new MockPreloadedPersistenceService(state),
            new MockThemeService(),
            new LocalizationService("en"),
            new FontSizeService(),
            windowCoordinator: coordinator);

        coordinator.RegisterWindow(primary, isPrimary: true);
        await primary.InitializeWorkspaceAsync();

        Assert.Single(primary.Tabs);
        Assert.Equal(@"C:\primary", primary.Tabs[0].WorkingDirectory);

        Assert.NotNull(openedSecondary);
        Assert.False(openedSecondary.IsPrimary);
        Assert.Equal(2, openedSecondary.WindowNumber);
        Assert.Single(openedSecondary.Tabs);
        Assert.Equal(@"C:\secondary", openedSecondary.Tabs[0].WorkingDirectory);
    }

    [Fact]
    public void TransferTab_MovesTabFromSourceToTarget_AtSpecifiedIndex_ZeroPtyRestart()
    {
        var coordinator = new WindowCoordinatorService();
        using var primary = CreateTestViewModel(coordinator);
        coordinator.RegisterWindow(primary, isPrimary: true);
        primary.AddNewTab();

        using var secondary = CreateTestViewModel(coordinator);
        coordinator.RegisterWindow(secondary, isPrimary: false);

        var tabToTransfer = primary.Tabs[1];
        var session = (MockShellSession)tabToTransfer.Session;

        coordinator.TransferTab(primary, secondary, tabToTransfer, insertIndex: 0);

        Assert.Single(primary.Tabs);
        Assert.DoesNotContain(tabToTransfer, primary.Tabs);

        Assert.Equal(2, secondary.Tabs.Count);
        Assert.Same(tabToTransfer, secondary.Tabs[0]);
        Assert.Same(tabToTransfer, secondary.SelectedTab);

        Assert.False(session.IsDisposed);
    }

    [Fact]
    public void TransferTab_FromSecondaryToPrimary_ClosesSecondaryWhenEmpty()
    {
        var coordinator = new WindowCoordinatorService();
        using var primary = CreateTestViewModel(coordinator);
        coordinator.RegisterWindow(primary, isPrimary: true);

        using var secondary = CreateTestViewModel(coordinator);
        coordinator.RegisterWindow(secondary, isPrimary: false);

        var tabToTransfer = secondary.Tabs[0];

        MainViewModel? closedVm = null;
        coordinator.WindowCloseRequested += (_, args) => closedVm = args.ViewModel;

        coordinator.TransferTab(secondary, primary, tabToTransfer);

        Assert.Empty(secondary.Tabs);
        Assert.Contains(tabToTransfer, primary.Tabs);
        Assert.Same(tabToTransfer, primary.SelectedTab);
        Assert.Same(secondary, closedVm);
    }
}
