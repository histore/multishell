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
        public ShellType ShellType { get; set; } = ShellType.PowerShell;
        public bool IsRunning { get; set; } = true;
        public List<byte[]> SentInputs { get; } = new();

        public event Action<ReadOnlyMemory<byte>>? DataReceived { add { } remove { } }
        public event Action<int>? Exited { add { } remove { } }
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted;
        public event Action<int>? CommandCompleted { add { } remove { } }

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
    public void NavigateToHistoryDirectory_TrimsTrailingSeparators_ForPowerShell()
    {
        // Arrange
        var session = new MockShellSession();
        var tab = new TerminalTabViewModel(session);

        // Act: Navigate to folder with trailing backslash
        tab.NavigateToHistoryDirectory(@"C:\My Project's Dir\Subfolder\");

        // Assert: Sent input has trimmed trailing slash so \" is not escaped
        Assert.NotEmpty(session.SentInputs);
        var sentText = Encoding.UTF8.GetString(session.SentInputs[0]);
        Assert.Equal("Set-Location -LiteralPath \"C:\\My Project's Dir\\Subfolder\"\r", sentText);
    }

    [Fact]
    public void NavigateToHistoryDirectory_WSL_UsesLfAndEscapedQuotes()
    {
        // Arrange
        var session = new MockShellSession { ShellType = Models.ShellType.WSL };
        var tab = new TerminalTabViewModel(session);

        // Act: Navigate in WSL
        tab.NavigateToHistoryDirectory("/mnt/c/Users/test folder/");

        // Assert: Ends with \n and has trimmed trailing slash
        Assert.NotEmpty(session.SentInputs);
        var sentText = Encoding.UTF8.GetString(session.SentInputs[0]);
        Assert.Equal("cd \"/mnt/c/Users/test folder\"\n", sentText);
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

    [Fact]
    public void REQ_TAB_012_HistoryOverlay_ExecuteHistoryCommand_AppendsNewline_ForExecution()
    {
        // Arrange
        var session = new MockShellSession();
        var tab = new TerminalTabViewModel(session);

        // Act - Execute sends command with carriage return
        tab.ExecuteHistoryCommand("git status");

        // Assert
        Assert.NotEmpty(session.SentInputs);
        var sentText = Encoding.UTF8.GetString(session.SentInputs[0]);
        Assert.Equal("git status\r", sentText);
    }

    [Fact]
    public void REQ_TAB_012_HistoryOverlay_PasteHistoryCommand_SendsWithoutNewline()
    {
        // Arrange
        var session = new MockShellSession();
        var tab = new TerminalTabViewModel(session);

        // Act - Paste sends raw text without execution newline
        tab.PasteHistoryCommand("git status");

        // Assert
        Assert.NotEmpty(session.SentInputs);
        var sentText = Encoding.UTF8.GetString(session.SentInputs[0]);
        Assert.Equal("git status", sentText);
    }

    [Fact]
    public void DeleteCommand_RemovesFromCommandHistoryAndFilteredCollection()
    {
        // Arrange
        var pathHistory = new PathCommandHistoryService();
        var dirHistory = new DirectoryHistoryService();
        var session = new MockShellSession { WorkingDirectory = @"C:\TestRepo" };
        var tab = new TerminalTabViewModel(session, pathCommandHistoryService: pathHistory, directoryHistoryService: dirHistory);

        pathHistory.RecordCommand(@"C:\TestRepo", "git status");
        pathHistory.RecordCommand(@"C:\TestRepo", "dotnet test");

        Assert.Equal(2, tab.FilteredCommandHistory.Count);

        // Act
        tab.DeleteCommand("git status");

        // Assert
        Assert.Single(tab.FilteredCommandHistory);
        Assert.Equal("dotnet test", tab.FilteredCommandHistory[0]);
        Assert.Single(tab.CommandHistory);
        Assert.Equal("dotnet test", tab.CommandHistory[0]);
        Assert.DoesNotContain(tab.GlobalHistory, g => g.Text == "git status");
    }

    [Fact]
    public void DeleteDirectory_RemovesFromDirectoryHistoryAndFilteredCollection()
    {
        // Arrange
        var pathHistory = new PathCommandHistoryService();
        var dirHistory = new DirectoryHistoryService();
        var session = new MockShellSession { WorkingDirectory = null };
        var tab = new TerminalTabViewModel(session, pathCommandHistoryService: pathHistory, directoryHistoryService: dirHistory);

        dirHistory.RecordDirectory(@"C:\Projects\Repo1");
        dirHistory.RecordDirectory(@"C:\Projects\Repo2");

        Assert.Equal(2, tab.FilteredDirectoryHistory.Count);

        // Act
        tab.DeleteDirectory(@"C:\Projects\Repo1");

        // Assert
        Assert.Single(tab.FilteredDirectoryHistory);
        Assert.Equal(DirectoryHistoryService.NormalizePath(@"C:\Projects\Repo2"), tab.FilteredDirectoryHistory[0]);
        Assert.DoesNotContain(tab.GlobalHistory, g => g.Text == DirectoryHistoryService.NormalizePath(@"C:\Projects\Repo1"));
    }

    [Fact]
    public void DeleteGlobalItem_Command_RemovesFromAllPathsAndGlobalHistory()
    {
        // Arrange
        var pathHistory = new PathCommandHistoryService();
        var dirHistory = new DirectoryHistoryService();
        var session = new MockShellSession { WorkingDirectory = null };
        var tab = new TerminalTabViewModel(session, pathCommandHistoryService: pathHistory, directoryHistoryService: dirHistory);

        pathHistory.RecordCommand(@"C:\TestRepo", "npm run lint");
        tab.SyncGlobalHistory();

        var globalItem = tab.GlobalHistory.FirstOrDefault(g => g.Text == "npm run lint");
        Assert.NotNull(globalItem);
        Assert.True(globalItem.IsCommand);

        // Act
        tab.DeleteGlobalItem(globalItem);

        // Assert
        Assert.Empty(tab.FilteredCommandHistory);
        Assert.Empty(tab.FilteredGlobalHistory);
        Assert.DoesNotContain(tab.GlobalHistory, g => g.Text == "npm run lint");
    }

    [Fact]
    public void DeleteGlobalItem_Directory_RemovesFromDirectoryHistoryAndGlobalHistory()
    {
        // Arrange
        var pathHistory = new PathCommandHistoryService();
        var dirHistory = new DirectoryHistoryService();
        var session = new MockShellSession { WorkingDirectory = null };
        var tab = new TerminalTabViewModel(session, pathCommandHistoryService: pathHistory, directoryHistoryService: dirHistory);

        dirHistory.RecordDirectory(@"C:\Projects\UniqueDir");
        tab.SyncGlobalHistory();

        var normalized = DirectoryHistoryService.NormalizePath(@"C:\Projects\UniqueDir");
        var globalItem = tab.GlobalHistory.FirstOrDefault(g => g.Text == normalized);
        Assert.NotNull(globalItem);
        Assert.False(globalItem.IsCommand);

        // Act
        tab.DeleteGlobalItem(globalItem);

        // Assert
        Assert.Empty(tab.FilteredDirectoryHistory);
        Assert.Empty(tab.FilteredGlobalHistory);
        Assert.DoesNotContain(tab.GlobalHistory, g => g.Text == normalized);
    }
}
