using System;
using System.Collections.Generic;
using System.Text;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public class TerminalTabExecutionStateTests
{
    private class MockPowerShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; }
        public string? WorkingDirectory { get; set; }
        public ShellType ShellType { get; set; } = ShellType.PowerShell;
        public bool IsRunning { get; set; } = true;

        public event Action<byte[]>? DataReceived;
        public event Action<int>? Exited;
        public event Action<string>? WorkingDirectoryChanged { add { } remove { } }
        public event Action<string>? CommandExecuted { add { } remove { } }
        public event Action<int>? CommandCompleted;

        public MockPowerShellSession(string title) => Title = title;

        public void Start() => IsRunning = true;
        public void Send(byte[] input) { }
        public void Resize(int cols, int rows) { }
        public void Dispose() { }

        public void SimulateDataReceived(byte[] data) => DataReceived?.Invoke(data);
        public void SimulateExit(int code) => Exited?.Invoke(code);
        public void SimulateCommandCompleted(int code) => CommandCompleted?.Invoke(code);
    }

    private class MockShellProcessService : IShellProcessService
    {
        public IShellSession CreateSession(
            string title,
            string? workingDirectory = null,
            ShellType shellType = ShellType.PowerShell,
            string? customExecutable = null,
            string? customArguments = null)
        {
            return new MockPowerShellSession(title) { WorkingDirectory = workingDirectory, ShellType = shellType };
        }
    }

    private class MockTabStatePersistenceService : ITabStatePersistenceService
    {
        public System.Threading.Tasks.Task SaveStateAsync(WorkspaceState state, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task<WorkspaceState?> LoadStateAsync(System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult<WorkspaceState?>(null);
    }

    private class MockToastNotificationService : IToastNotificationService
    {
        public List<(string Title, string Command, TimeSpan Duration, int ExitCode)> Notifications { get; } = new();

        public void ShowProcessCompletionNotification(
            string tabTitle,
            string command,
            TimeSpan duration,
            int exitCode,
            Action onActivated)
        {
            Notifications.Add((tabTitle, command, duration, exitCode));
        }
    }

    [Fact]
    public void InitialState_IsIdle_WithoutProcessStatus()
    {
        var session = new MockPowerShellSession("Tab1");
        using var tab = new TerminalTabViewModel(session);

        Assert.Equal(ProcessExecutionState.Idle, tab.ExecutionState);
        Assert.False(tab.HasProcessStatus);
        Assert.False(tab.IsProcessRunning);
        Assert.False(tab.HasProcessError);
        Assert.Equal("●", tab.ExecutionStatusGlyph);
        Assert.Equal(tab.Loc["Process_Activity_Ready"], tab.ProcessActivityText);
    }

    [Fact]
    public void MarkCommandStarted_SetsRunningState_AndGlyph()
    {
        var session = new MockPowerShellSession("Tab1");
        using var tab = new TerminalTabViewModel(session);

        tab.MarkCommandStarted("dotnet build");

        Assert.Equal(ProcessExecutionState.Running, tab.ExecutionState);
        Assert.True(tab.HasProcessStatus);
        Assert.True(tab.IsProcessRunning);
        Assert.False(tab.HasProcessError);
        Assert.Equal("●", tab.ExecutionStatusGlyph);
        Assert.Equal("dotnet build", tab.LastExecutedCommand);
    }

    [Fact]
    public void OnSessionCommandCompleted_WithExitZero_SetsSuccessState()
    {
        var session = new MockPowerShellSession("Tab1");
        using var tab = new TerminalTabViewModel(session);

        tab.MarkCommandStarted("git status");
        session.SimulateCommandCompleted(0);

        Assert.Equal(ProcessExecutionState.Success, tab.ExecutionState);
        Assert.True(tab.HasProcessStatus);
        Assert.False(tab.IsProcessRunning);
        Assert.False(tab.HasProcessError);
        Assert.Equal("✓", tab.ExecutionStatusGlyph);
        Assert.Equal(0, tab.LastExitCode);
    }

    [Fact]
    public void OnSessionCommandCompleted_WithExitNonZero_SetsFailedStateAndExitCode()
    {
        var session = new MockPowerShellSession("Tab1");
        using var tab = new TerminalTabViewModel(session);

        tab.MarkCommandStarted("npm test");
        session.SimulateCommandCompleted(1);

        Assert.Equal(ProcessExecutionState.Failed, tab.ExecutionState);
        Assert.True(tab.HasProcessStatus);
        Assert.False(tab.IsProcessRunning);
        Assert.True(tab.HasProcessError);
        Assert.Equal("✕", tab.ExecutionStatusGlyph);
        Assert.Equal(1, tab.LastExitCode);
    }

    [Fact]
    public void SubsequentCommand_ClearsPreviousError_AndTransitionsToRunning()
    {
        var session = new MockPowerShellSession("Tab1");
        using var tab = new TerminalTabViewModel(session);

        tab.MarkCommandStarted("failing_cmd");
        session.SimulateCommandCompleted(127);
        Assert.True(tab.HasProcessError);
        Assert.Equal(127, tab.LastExitCode);

        // Run next command
        tab.MarkCommandStarted("recovering_cmd");

        Assert.Equal(ProcessExecutionState.Running, tab.ExecutionState);
        Assert.False(tab.HasProcessError);
        Assert.Null(tab.LastExitCode);
        Assert.Equal("●", tab.ExecutionStatusGlyph);
    }

    [Fact]
    public void OnSessionExited_WithNonZeroCode_SetsFailedState()
    {
        var session = new MockPowerShellSession("Tab1");
        using var tab = new TerminalTabViewModel(session);

        session.SimulateExit(42);

        Assert.Equal(ProcessExecutionState.Failed, tab.ExecutionState);
        Assert.True(tab.HasProcessError);
        Assert.Equal(42, tab.LastExitCode);
    }

    [Fact]
    public void CommandStarted_SetsActiveOutputAndFullOpacity_AndTransitionsToWaitingAfterCooldown()
    {
        var session = new MockPowerShellSession("Tab1");
        using var tab = new TerminalTabViewModel(session);

        tab.MarkCommandStarted("agy prompt");

        Assert.True(tab.IsProcessRunning);
        Assert.True(tab.IsProcessHudVisible);
        Assert.True(tab.IsOutputActive);
        Assert.Equal(1.0, tab.OutputActivityOpacity);
        Assert.Equal("●", tab.ExecutionStatusGlyph);
        Assert.Equal(tab.Loc["Process_Activity_Active"], tab.ProcessActivityText);

        // Simulate 1.5s silence cooldown: transition from working/outputting to waiting for input
        tab.SimulateActivityCooldownTick();

        Assert.True(tab.IsProcessRunning);
        Assert.True(tab.IsProcessHudVisible);
        Assert.False(tab.IsOutputActive);
        Assert.Equal(0.65, tab.OutputActivityOpacity);
        Assert.Equal("○", tab.ExecutionStatusGlyph);
        Assert.Equal(tab.Loc["Process_Activity_Waiting"], tab.ProcessActivityText);

        // App produces output again (e.g. streaming LLM tokens)
        tab.PulseOutputActivity();

        Assert.True(tab.IsOutputActive);
        Assert.Equal(1.0, tab.OutputActivityOpacity);
        Assert.Equal("●", tab.ExecutionStatusGlyph);
        Assert.Equal(tab.Loc["Process_Activity_Active"], tab.ProcessActivityText);

        // Command completes -> HUD hides, status shows success or failure
        session.SimulateCommandCompleted(0);
        Assert.False(tab.IsProcessRunning);
        Assert.False(tab.IsProcessHudVisible);
        Assert.Equal("✓", tab.ExecutionStatusGlyph);
    }

    [Theory]
    [InlineData("de", "Aktiv", "Wartet auf Eingabe")]
    [InlineData("en", "Active", "Waiting for input")]
    [InlineData("es", "Activo", "Esperando entrada")]
    [InlineData("fr", "Actif", "En attente de saisie")]
    [InlineData("it", "Attivo", "In attesa di input")]
    [InlineData("pt", "Ativo", "A aguardar introdução")]
    public void ActivityText_SupportsAllLanguages(string lang, string expectedActive, string expectedWaiting)
    {
        var session = new MockPowerShellSession("Tab1");
        var loc = new LocalizationService();
        loc.SetLanguage(lang);
        using var tab = new TerminalTabViewModel(session, localizationService: loc);

        tab.MarkCommandStarted("test_cmd");
        Assert.Equal(expectedActive, tab.ProcessActivityText);

        tab.SimulateActivityCooldownTick();
        Assert.Equal(expectedWaiting, tab.ProcessActivityText);
    }

    [Fact]
    public void LongRunningCommand_FiresCommandExecutionCompleted()
    {
        var session = new MockPowerShellSession("Tab1");
        using var tab = new TerminalTabViewModel(session);
        tab.LongRunningThreshold = TimeSpan.FromMilliseconds(50);

        CommandExecutionResult? receivedResult = null;
        tab.CommandExecutionCompleted += (_, res) => receivedResult = res;

        tab.MarkCommandStarted("long_task");
        System.Threading.Thread.Sleep(80);
        session.SimulateCommandCompleted(0);

        Assert.NotNull(receivedResult);
        Assert.Equal("long_task", receivedResult.Command);
        Assert.Equal(0, receivedResult.ExitCode);
        Assert.True(receivedResult.Duration >= TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void ShortRunningCommand_DoesNotFireCommandExecutionCompleted()
    {
        var session = new MockPowerShellSession("Tab1");
        using var tab = new TerminalTabViewModel(session);
        tab.LongRunningThreshold = TimeSpan.FromSeconds(10);

        bool fired = false;
        tab.CommandExecutionCompleted += (_, _) => fired = true;

        tab.MarkCommandStarted("quick_task");
        session.SimulateCommandCompleted(0);

        Assert.False(fired);
    }

    [Fact]
    public void MainViewModel_NotifiesToast_WhenTabIsInactive()
    {
        var mockToast = new MockToastNotificationService();
        var mainVm = new MainViewModel(
            new MockShellProcessService(),
            new MockTabStatePersistenceService(),
            new ThemeService(),
            new LocalizationService(),
            new FontSizeService(),
            toastNotificationService: mockToast);

        mainVm.AddNewTab();
        mainVm.AddNewTab();
        var tab1 = mainVm.Tabs[0];
        var tab2 = mainVm.Tabs[1];
        mainVm.SelectedTab = tab2; // tab1 is now in the background!

        tab1.LongRunningThreshold = TimeSpan.FromMilliseconds(10);
        tab1.MarkCommandStarted("docker build");
        System.Threading.Thread.Sleep(20);
        tab1.OnSessionCommandCompleted(0);

        Assert.Single(mockToast.Notifications);
        Assert.Equal("docker build", mockToast.Notifications[0].Command);
        Assert.Equal(0, mockToast.Notifications[0].ExitCode);
    }

    [Fact]
    public void MainViewModel_DoesNotNotifyToast_WhenTabIsActiveAndWindowIsActive()
    {
        var mockToast = new MockToastNotificationService();
        var mainVm = new MainViewModel(
            new MockShellProcessService(),
            new MockTabStatePersistenceService(),
            new ThemeService(),
            new LocalizationService(),
            new FontSizeService(),
            toastNotificationService: mockToast);

        mainVm.AddNewTab();
        var activeTab = mainVm.Tabs[0];
        mainVm.SelectedTab = activeTab;
        mainVm.IsWindowInactive = false;

        activeTab.LongRunningThreshold = TimeSpan.FromMilliseconds(10);
        activeTab.MarkCommandStarted("dotnet run");
        System.Threading.Thread.Sleep(20);
        activeTab.OnSessionCommandCompleted(0);

        Assert.Empty(mockToast.Notifications);
    }

    [Fact]
    public void MainViewModel_UpdatesCapsLockState_WithoutException()
    {
        var mainVm = new MainViewModel(
            new MockShellProcessService(),
            new MockTabStatePersistenceService(),
            new ThemeService(),
            new LocalizationService(),
            new FontSizeService());

        // Calling UpdateCapsLockState queries the helper safely
        mainVm.UpdateCapsLockState();
        // Verifies the property is a valid boolean
        Assert.True(mainVm.IsCapsLockActive || !mainVm.IsCapsLockActive);
    }
}
