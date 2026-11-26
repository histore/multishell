using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using MultiShell.Views;
using MultiShell.Views.Dialogs;
using Xunit;

namespace MultiShell.Tests;

public class MainViewModelCloseConfirmationTests
{
    private sealed class MockShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; }
        public string? WorkingDirectory { get; }
        public ShellType ShellType { get; }
        public bool IsRunning { get; private set; } = true;

        public event Action<ReadOnlyMemory<byte>>? DataReceived { add { } remove { } }
        public event Action<int>? Exited { add { } remove { } }
        public event Action<string>? WorkingDirectoryChanged { add { } remove { } }
        public event Action<string>? CommandExecuted { add { } remove { } }
        public event Action<int>? CommandCompleted;

        public MockShellSession(string title, string? workingDir = null, ShellType shellType = ShellType.PowerShell)
        {
            Title = title;
            WorkingDirectory = workingDir;
            ShellType = shellType;
        }

        public void Start() => IsRunning = true;
        public void Send(byte[] input) { }
        public void Resize(int cols, int rows) { }
        public void Dispose() => IsRunning = false;
        public void CompleteCommand(int exitCode = 0) => CommandCompleted?.Invoke(exitCode);
    }

    private sealed class MockShellProcessService : IShellProcessService
    {
        public List<MockShellSession> CreatedSessions { get; } = new();

        public IShellSession CreateSession(
            string title,
            string? workingDirectory = null,
            ShellType shellType = ShellType.PowerShell,
            string? customExecutable = null,
            string? customArguments = null)
        {
            var session = new MockShellSession(title, workingDirectory, shellType);
            CreatedSessions.Add(session);
            return session;
        }
    }

    private sealed class MockPersistenceService : ITabStatePersistenceService
    {
        public Task SaveStateAsync(WorkspaceState state, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkspaceState?> LoadStateAsync(CancellationToken cancellationToken = default) => Task.FromResult<WorkspaceState?>(null);
    }

    private static MainViewModel CreateViewModel(
        MockShellProcessService processService,
        IWindowCoordinatorService? coordinator = null,
        bool isSatellite = false)
    {
        return new MainViewModel(
            processService,
            new MockPersistenceService(),
            new ThemeService(),
            new LocalizationService("en"),
            new FontSizeService(),
            windowCoordinator: coordinator,
            isSatellite: isSatellite);
    }

    [Fact]
    public async Task TryRequestCloseConfirmation_WhenAllTabsIdle_ReturnsFalseAndDoesNotOpenDialog()
    {
        // Arrange
        var processService = new MockShellProcessService();
        using var vm = CreateViewModel(processService);
        await vm.InitializeWorkspaceAsync();

        // Act
        var shouldCancel = vm.TryRequestCloseConfirmation();

        // Assert
        Assert.False(shouldCancel);
        Assert.False(vm.IsCloseConfirmationOpen);
        Assert.Empty(vm.ActiveProcessTabs);
    }

    [Fact]
    public async Task TryRequestCloseConfirmation_WhenTabHasRunningProcess_OpensDialogAndSelectsFirstAffectedTab()
    {
        // Arrange
        var processService = new MockShellProcessService();
        using var vm = CreateViewModel(processService);
        await vm.InitializeWorkspaceAsync();
        vm.AddNewTab();
        var tab1 = vm.Tabs[0];
        var tab2 = vm.Tabs[1];
        tab1.MarkCommandStarted("dotnet watch");
        vm.SelectedTab = tab2;

        // Act
        var shouldCancel = vm.TryRequestCloseConfirmation();

        // Assert
        Assert.True(shouldCancel);
        Assert.True(vm.IsCloseConfirmationOpen);
        var item = Assert.Single(vm.ActiveProcessTabs);
        Assert.Same(tab1, item.Tab);
        Assert.Same(item, vm.SelectedActiveProcessTab);
        Assert.Equal("dotnet watch", item.CommandSummary);
        Assert.Equal(vm.Loc["CloseConfirm_Message_Single"], vm.CloseConfirmationMessage);
    }

    [Fact]
    public async Task TryRequestCloseConfirmation_DetectsRunningProcessInBackgroundSplitPane_AndSwitchFocusesIt()
    {
        // Arrange
        var processService = new MockShellProcessService();
        using var vm = CreateViewModel(processService);
        await vm.InitializeWorkspaceAsync();
        var tab1 = vm.Tabs[0];
        var initialPane = tab1.ActivePane!;
        var splitPane = tab1.SplitActivePane(PaneSplitDirection.Vertical);
        splitPane.Tab.MarkCommandStarted("ping -t localhost", splitPane);
        tab1.SetActivePane(initialPane); // Focus back to idle pane

        vm.AddNewTab(); // Switch to tab2
        Assert.True(tab1.HasActiveProcess);

        // Act
        var shouldCancel = vm.TryRequestCloseConfirmation();
        Assert.True(shouldCancel);
        vm.SwitchToActiveProcessTab();

        // Assert
        Assert.False(vm.IsCloseConfirmationOpen);
        Assert.Same(tab1, vm.SelectedTab);
        Assert.Same(splitPane, tab1.ActivePane);
    }

    [Fact]
    public async Task CancelCloseConfirmation_DismissesDialogAndRePromptsOnNextCloseWhileProcessActive()
    {
        // Arrange
        var processService = new MockShellProcessService();
        using var vm = CreateViewModel(processService);
        await vm.InitializeWorkspaceAsync();
        vm.Tabs[0].MarkCommandStarted("npm run dev");
        var dismissedCount = 0;
        vm.CloseConfirmationDismissed += () => dismissedCount++;

        Assert.True(vm.TryRequestCloseConfirmation());

        // Act
        vm.CancelCloseConfirmation();

        // Assert
        Assert.False(vm.IsCloseConfirmationOpen);
        Assert.Empty(vm.ActiveProcessTabs);
        Assert.Null(vm.SelectedActiveProcessTab);
        Assert.Equal(1, dismissedCount);
        Assert.True(vm.TryRequestCloseConfirmation());
    }

    [Fact]
    public async Task ConfirmExitFromCloseDialog_AllowsSubsequentCloseWithoutRePrompting()
    {
        // Arrange
        var processService = new MockShellProcessService();
        using var vm = CreateViewModel(processService);
        await vm.InitializeWorkspaceAsync();
        vm.Tabs[0].MarkCommandStarted("cargo build");
        var exitRequestedCount = 0;
        vm.ExitConfirmedRequested += () => exitRequestedCount++;

        Assert.True(vm.TryRequestCloseConfirmation());

        // Act
        vm.ConfirmExitFromCloseDialog();

        // Assert
        Assert.False(vm.IsCloseConfirmationOpen);
        Assert.Equal(1, exitRequestedCount);
        Assert.False(vm.TryRequestCloseConfirmation());
    }

    [Fact]
    public async Task TryRequestCloseConfirmation_WhenProcessCompletesAfterCancel_AllowsClose()
    {
        // Arrange
        var processService = new MockShellProcessService();
        using var vm = CreateViewModel(processService);
        await vm.InitializeWorkspaceAsync();
        vm.Tabs[0].MarkCommandStarted("git fetch");
        Assert.True(vm.TryRequestCloseConfirmation());
        vm.CancelCloseConfirmation();

        // Act
        processService.CreatedSessions[0].CompleteCommand(0);

        // Assert
        Assert.False(vm.Tabs[0].HasActiveProcess);
        Assert.False(vm.TryRequestCloseConfirmation());
    }

    [Fact]
    public async Task TryRequestCloseConfirmation_IncludesSecondaryWindowActiveProcesses_AndSkipsPromptOnSecondaryWindow()
    {
        // Arrange
        var coordinator = new WindowCoordinatorService();
        var processService = new MockShellProcessService();
        using var primary = CreateViewModel(processService, coordinator, isSatellite: false);
        coordinator.RegisterWindow(primary, isPrimary: true);
        await primary.InitializeWorkspaceAsync();

        primary.AddNewTab();
        var tab2 = primary.Tabs[1];
        await coordinator.UndockTabAsync(primary, tab2);

        var secondary = Assert.IsType<MainViewModel>(Assert.Single(coordinator.Windows, w => !w.IsPrimary));
        tab2.MarkCommandStarted("python train.py");

        var secondaryFocusCount = 0;
        secondary.WindowFocusRequested += () => secondaryFocusCount++;

        // Act & Assert: secondary window close never prompts
        Assert.False(secondary.TryRequestCloseConfirmation());

        // Primary window close prompts for secondary window active process
        Assert.True(primary.TryRequestCloseConfirmation());
        var activeItem = Assert.Single(primary.ActiveProcessTabs);
        Assert.Same(secondary, activeItem.OwnerWindow);
        Assert.True(activeItem.HasWindowBadge);

        primary.SwitchToActiveProcessTab(activeItem);
        Assert.Same(tab2, secondary.SelectedTab);
        Assert.Equal(1, secondaryFocusCount);
    }

    [Collection("HeadlessUI")]
    public class CloseConfirmModalHeadlessUiTests
    {
        [Fact]
        public async Task MainWindow_ClosingWithActiveProcess_CancelsCloseAndPreselectsCancelButton()
        {
            await HeadlessTestSession.DispatchAsync(async () =>
            {
                // Arrange
                var processService = new MockShellProcessService();
                using var vm = CreateViewModel(processService);
                await vm.InitializeWorkspaceAsync();
                vm.Tabs[0].MarkCommandStarted("dotnet test");

                var window = new MainWindow { DataContext = vm };
                try
                {
                    window.Show();
                    Dispatcher.UIThread.RunJobs();

                    // Act: attempt to close main window
                    window.Close();
                    Dispatcher.UIThread.RunJobs();

                    // Assert: close was cancelled and modal opened with Cancel pre-selected
                    Assert.True(window.IsVisible);
                    Assert.True(vm.IsCloseConfirmationOpen);

                    var modal = window.FindControl<CloseConfirmModalView>("CloseConfirmModal");
                    Assert.NotNull(modal);
                    Assert.True(modal!.IsVisible);

                    var cancelBtn = modal.FindControl<Button>("CancelExitButton");
                    var switchBtn = modal.FindControl<Button>("SwitchToTabButton");
                    var exitBtn = modal.FindControl<Button>("ExitApplicationButton");
                    Assert.NotNull(cancelBtn);
                    Assert.NotNull(switchBtn);
                    Assert.NotNull(exitBtn);
                    Assert.True(cancelBtn!.IsDefault);
                    Assert.True(cancelBtn.IsCancel);
                    Assert.True(cancelBtn.IsFocused);
                }
                finally
                {
                    vm.ConfirmExitFromCloseDialog();
                    Dispatcher.UIThread.RunJobs();
                }
            });
        }
    }
}
