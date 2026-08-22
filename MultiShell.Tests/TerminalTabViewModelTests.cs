using System;
using System.Collections.Generic;
using System.Text;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public class TerminalTabViewModelTests
{
    private class MockPowerShellSession : IPowerShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; }
        public string? WorkingDirectory { get; set; }
        public bool IsRunning { get; set; }
        public bool Disposed { get; private set; }
        public bool Started { get; private set; }
        public List<byte[]> SentData { get; } = new();
        public (int Cols, int Rows)? LastResize { get; private set; }

        public event Action<byte[]>? DataReceived;
        public event Action<int>? Exited;
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted;

        public MockPowerShellSession(string title, string? workingDirectory = null)
        {
            Title = title;
            WorkingDirectory = workingDirectory;
        }

        public void Start()
        {
            IsRunning = true;
            Started = true;
        }

        public void Send(byte[] input)
        {
            SentData.Add(input);
        }

        public void Resize(int cols, int rows)
        {
            LastResize = (cols, rows);
        }

        public void SimulateDataReceived(byte[] data)
        {
            DataReceived?.Invoke(data);
        }

        public void SimulateExit(int exitCode)
        {
            IsRunning = false;
            Exited?.Invoke(exitCode);
        }

        public void SimulateDirectoryChange(string newDir)
        {
            WorkingDirectory = newDir;
            WorkingDirectoryChanged?.Invoke(newDir);
        }

        public void SimulateCommandExecuted(string cmd)
        {
            CommandExecuted?.Invoke(cmd);
        }

        public void Dispose()
        {
            Disposed = true;
            IsRunning = false;
        }
    }

    [Fact]
    public void Constructor_CreatesTerminalModelAndSetsTitle()
    {
        // Arrange & Act
        var session = new MockPowerShellSession("TestPS", @"C:\Projects");
        using var vm = new TerminalTabViewModel(session);

        // Assert
        Assert.Equal(@"C:\Projects", vm.Title);
        Assert.Equal(@"C:\Projects", vm.WorkingDirectory);
        Assert.Single(vm.DirectoryHistory);
        Assert.Equal(@"C:\Projects", vm.DirectoryHistory[0]);
        Assert.NotNull(vm.TerminalModel);
    }

    [Fact]
    public void StartSession_StartsUnderlyingSession()
    {
        // Arrange
        var session = new MockPowerShellSession("TestPS");
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.StartSession();

        // Assert
        Assert.True(session.Started);
        Assert.True(vm.IsRunning);
    }

    [Fact]
    public void RequestClose_InvokesCloseRequestedEvent()
    {
        // Arrange
        var session = new MockPowerShellSession("TestPS");
        using var vm = new TerminalTabViewModel(session);
        var eventFired = false;
        vm.CloseRequested += _ => eventFired = true;

        // Act
        vm.RequestClose();

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void Dispose_DisposesUnderlyingSession()
    {
        // Arrange
        var session = new MockPowerShellSession("TestPS");
        var vm = new TerminalTabViewModel(session);

        // Act
        vm.Dispose();

        // Assert
        Assert.True(session.Disposed);
    }

    [Fact]
    public void SessionExited_UpdatesIsRunningAndRequestsClose()
    {
        // Arrange
        var session = new MockPowerShellSession("TestPS");
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();
        var closeRequestedFired = false;
        vm.CloseRequested += _ => closeRequestedFired = true;

        // Act
        session.SimulateExit(0);

        // Assert
        Assert.False(vm.IsRunning);
        Assert.True(closeRequestedFired);
    }

    [Fact]
    public void DirectoryChanged_UpdatesWorkingDirectoryAndTitleAndAppendsHistory()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);
        string? notifiedDir = null;
        vm.DirectoryChanged += (_, dir) => notifiedDir = dir;

        // Act
        session.SimulateDirectoryChange(@"C:\projekte\quicklaunch");

        // Assert
        Assert.Equal(@"C:\projekte\quicklaunch", vm.WorkingDirectory);
        Assert.Equal(@"C:\projekte\quicklaunch", vm.Title);
        Assert.Equal(@"C:\projekte\quicklaunch", notifiedDir);
        Assert.Contains(@"C:\projekte\quicklaunch", vm.DirectoryHistory);
    }

    [Fact]
    public void CommandExecuted_AppendsToCommandHistory()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Act
        session.SimulateCommandExecuted("Get-Process");
        session.SimulateCommandExecuted("git status");

        // Assert
        Assert.Equal(2, vm.CommandHistory.Count);
        Assert.Equal("Get-Process", vm.CommandHistory[0]);
        Assert.Equal("git status", vm.CommandHistory[1]);
    }

    [Fact]
    public void CommandExecuted_WhenDuplicateExecuted_RemovesOlderEntryAndMovesToNewest()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Act - Execute A, B, A
        session.SimulateCommandExecuted("git status");
        session.SimulateCommandExecuted("dotnet test");
        session.SimulateCommandExecuted("git status");

        // Assert - A should only exist once at the end
        Assert.Equal(2, vm.CommandHistory.Count);
        Assert.Equal("dotnet test", vm.CommandHistory[0]);
        Assert.Equal("git status", vm.CommandHistory[1]);
    }

    [Fact]
    public void ExecuteHistoryCommand_SendsCommandToSessionWithoutExecuting()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.ExecuteHistoryCommand("dotnet test");

        // Assert - Insert into command line without auto-executing \r\n
        Assert.Single(session.SentData);
        var sentString = Encoding.UTF8.GetString(session.SentData[0]);
        Assert.Equal("dotnet test", sentString);
    }

    [Fact]
    public void NavigateToHistoryDirectory_SendsSetLocationToSessionWithoutExecuting()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.NavigateToHistoryDirectory(@"C:\projekte\app");

        // Assert - Insert into command line without auto-executing \r\n
        Assert.Single(session.SentData);
        var sentString = Encoding.UTF8.GetString(session.SentData[0]);
        Assert.Equal("Set-Location -LiteralPath \"C:\\projekte\\app\"", sentString);
    }
}
