using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public class HistoryOverlayInteractionTests
{
    private class MockShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; } = "Test";
        public string? WorkingDirectory { get; set; } = @"C:\TestDir";
        public ShellType ShellType => ShellType.PowerShell;
        public bool IsRunning { get; set; } = true;
        public List<byte[]> SentInputs { get; } = new();

        public event Action<byte[]>? DataReceived { add { } remove { } }
        public event Action<int>? Exited { add { } remove { } }
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted;

        public void Start() { }
        public void Send(byte[] input) => SentInputs.Add(input);
        public void Resize(int cols, int rows) { }
        public void Dispose() { }

        public void TriggerCommand(string cmd) => CommandExecuted?.Invoke(cmd);
        public void TriggerDirChange(string dir)
        {
            WorkingDirectory = dir;
            WorkingDirectoryChanged?.Invoke(dir);
        }
    }

    [Fact]
    public void REQ_TAB_012_HistoryOverlay_CommandFiltering_WorksDynamically()
    {
        // Arrange
        var pathHistory = new PathCommandHistoryService();
        var dirHistory = new DirectoryHistoryService();
        var session = new MockShellSession { WorkingDirectory = @"C:\Projects\MultiShell" };
        var tab = new TerminalTabViewModel(session, pathCommandHistoryService: pathHistory, directoryHistoryService: dirHistory);

        pathHistory.RecordCommand(@"C:\Projects\MultiShell", "git status");
        pathHistory.RecordCommand(@"C:\Projects\MultiShell", "git commit -m \"feat\"");
        pathHistory.RecordCommand(@"C:\Projects\MultiShell", "dotnet test");

        // Act & Assert initial state
        Assert.Equal(3, tab.FilteredCommandHistory.Count);

        // Filter for "git"
        tab.CommandFilterQuery = "git";
        Assert.Equal(2, tab.FilteredCommandHistory.Count);
        Assert.Contains(tab.FilteredCommandHistory, c => c.Contains("git status"));
        Assert.Contains(tab.FilteredCommandHistory, c => c.Contains("git commit"));

        // Clear filter
        tab.CommandFilterQuery = string.Empty;
        Assert.Equal(3, tab.FilteredCommandHistory.Count);
    }

    [Fact]
    public void REQ_TAB_012_HistoryOverlay_DirectoryFiltering_WorksDynamically()
    {
        // Arrange
        var pathHistory = new PathCommandHistoryService();
        var dirHistory = new DirectoryHistoryService();
        var session = new MockShellSession { WorkingDirectory = null };
        var tab = new TerminalTabViewModel(session, pathCommandHistoryService: pathHistory, directoryHistoryService: dirHistory);

        dirHistory.RecordDirectory(@"C:\Projects\MultiShell");
        dirHistory.RecordDirectory(@"C:\Projects\OtherApp");
        dirHistory.RecordDirectory(@"D:\Data\Logs");

        // Act & Assert initial state
        Assert.Equal(3, tab.FilteredDirectoryHistory.Count);

        // Filter for "Projects"
        tab.DirectoryFilterQuery = "Projects";
        Assert.Equal(2, tab.FilteredDirectoryHistory.Count);

        // Clear filter
        tab.DirectoryFilterQuery = string.Empty;
        Assert.Equal(3, tab.FilteredDirectoryHistory.Count);
    }

    [Fact]
    public void REQ_TAB_012_HistoryOverlay_NavigateToDirectory_SendsCdCommandToTerminal()
    {
        // Arrange
        var session = new MockShellSession();
        var tab = new TerminalTabViewModel(session);

        // Act
        tab.NavigateToHistoryDirectory(@"C:\Target\Folder");

        // Assert: Sent input contains cd command
        Assert.NotEmpty(session.SentInputs);
        var sentText = Encoding.UTF8.GetString(session.SentInputs[0]);
        Assert.Contains(@"Set-Location", sentText);
        Assert.Contains(@"C:\Target\Folder", sentText);
    }

    [Fact]
    public void REQ_TAB_012_HistoryOverlay_ExecuteCommand_SendsExactCommandToTerminal()
    {
        // Arrange
        var session = new MockShellSession();
        var tab = new TerminalTabViewModel(session);

        // Act
        tab.SendInput(Encoding.UTF8.GetBytes("npm run build\r\n"));

        // Assert
        Assert.NotEmpty(session.SentInputs);
        var sentText = Encoding.UTF8.GetString(session.SentInputs[0]);
        Assert.Equal("npm run build\r\n", sentText);
    }
}
