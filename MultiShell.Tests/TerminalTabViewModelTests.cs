using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public partial class TerminalTabViewModelTests
{
    private class MockPowerShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; }
        public string? WorkingDirectory { get; set; }
        public ShellType ShellType { get; set; } = ShellType.PowerShell;
        public bool IsRunning { get; set; }
        public bool Disposed { get; private set; }
        public bool Started { get; private set; }
        public List<byte[]> SentData { get; } = new();
        public (int Cols, int Rows)? LastResize { get; private set; }

        public event Action<ReadOnlyMemory<byte>>? DataReceived;
        public event Action<int>? Exited;
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted;
        public event Action<int>? CommandCompleted;

        public void SimulateCommandCompleted(int exitCode) => CommandCompleted?.Invoke(exitCode);

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

        public void SimulateDataReceived(ReadOnlyMemory<byte> data)
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
        Assert.Equal(@"C:\Projects", vm.DisplayTitle);
        Assert.Equal(@"C:\Projects", vm.WorkingDirectory);
        Assert.Single(vm.DirectoryHistory);
        Assert.Equal(@"C:\Projects", vm.DirectoryHistory[0]);
        Assert.NotNull(vm.TerminalModel);
    }

    [Theory]
    [InlineData(@"C:\short\path", 22, @"C:\short\path")]
    [InlineData(@"C:\projekte\csharp\multishell", 22, @"C:\...\multishell")]
    [InlineData(@"C:\projekte\csharp\multishell\Services\Subfolder", 22, @"C:\...\Subfolder")]
    [InlineData(@"/home/user/workspace/development/multishell", 22, @"/.../multishell")]
    [InlineData(@"PS 1", 22, @"PS 1")]
    public void FormatMiddleEllipsis_FormatsPathsCorrectly(string input, int maxLen, string expected)
    {
        var actual = TerminalTabViewModel.FormatMiddleEllipsis(input, maxLen);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DisplayTitle_UpdatesWhenDirectoryChanges()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);
        Assert.Equal("PS 1", vm.DisplayTitle);
        Assert.Equal("PS 1", vm.TabTooltip);

        // Act
        session.SimulateDirectoryChange(@"C:\projekte\csharp\multishell\Services\Subfolder");

        // Assert
        Assert.Equal(@"C:\...\Subfolder", vm.DisplayTitle);
        Assert.Equal(@"C:\projekte\csharp\multishell\Services\Subfolder", vm.TabTooltip);
    }

    [Fact]
    public void TabTooltip_ReturnsFullPathEvenWhenTitleIsTruncated()
    {
        // Arrange
        var fullPath = @"C:\Users\heino\source\repos\very-long-folder-structure\sub\target";
        var session = new MockPowerShellSession("TestPS", fullPath);
        using var vm = new TerminalTabViewModel(session);

        // Assert - DisplayTitle is compacted with middle ellipsis, TabTooltip is full untruncated path
        Assert.Equal(@"C:\...\target", vm.DisplayTitle);
        Assert.Equal(fullPath, vm.TabTooltip);
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
        Assert.Equal("git status", vm.CommandHistory[0]);
        Assert.Equal("Get-Process", vm.CommandHistory[1]);
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

        // Assert - A should only exist once at the beginning (newest first)
        Assert.Equal(2, vm.CommandHistory.Count);
        Assert.Equal("git status", vm.CommandHistory[0]);
        Assert.Equal("dotnet test", vm.CommandHistory[1]);
    }

    [Fact]
    public void ExecuteHistoryCommand_SendsCommandToSessionWithReturn()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.ExecuteHistoryCommand("dotnet test");

        // Assert - Executes with \r
        Assert.Single(session.SentData);
        var sentString = Encoding.UTF8.GetString(session.SentData[0]);
        Assert.Equal("dotnet test\r", sentString);
    }

    [Fact]
    public void PasteHistoryCommand_PastesCommandWithoutReturn()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.PasteHistoryCommand("dotnet test");

        // Assert - Pastes WITHOUT \r
        Assert.Single(session.SentData);
        var sentString = Encoding.UTF8.GetString(session.SentData[0]);
        Assert.Equal("dotnet test", sentString);
    }

    [Fact]
    public void PasteHistoryDirectory_PastesNavigationWithoutReturn()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.PasteHistoryDirectory(@"C:\projekte\app");

        // Assert - Pastes navigation WITHOUT \r
        Assert.Single(session.SentData);
        var sentString = Encoding.UTF8.GetString(session.SentData[0]);
        Assert.Equal("Set-Location -LiteralPath \"C:\\projekte\\app\"", sentString);
    }

    [Fact]
    public void InsertPath_WithoutSpaces_PastesRawPathWithoutReturn()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.InsertPath(@"C:\projects\multishell\README.md");

        // Assert - Pastes without quotes and without \r
        Assert.Single(session.SentData);
        var sentString = Encoding.UTF8.GetString(session.SentData[0]);
        Assert.Equal(@"C:\projects\multishell\README.md", sentString);
    }

    [Fact]
    public void InsertPath_WithSpaces_WrapsInQuotesWithoutReturn()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.InsertPath(@"C:\Program Files\My App\script.ps1");

        // Assert - Automatically wrapped in double quotes
        Assert.Single(session.SentData);
        var sentString = Encoding.UTF8.GetString(session.SentData[0]);
        Assert.Equal(@"""C:\Program Files\My App\script.ps1""", sentString);
    }

    [Fact]
    public void InsertPath_AlreadyQuoted_DoesNotDoubleQuote()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.InsertPath(@"""C:\Program Files\My App""");

        // Assert - Preserves existing quotes
        Assert.Single(session.SentData);
        var sentString = Encoding.UTF8.GetString(session.SentData[0]);
        Assert.Equal(@"""C:\Program Files\My App""", sentString);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InsertPath_NullOrWhitespace_SendsNothing(string? path)
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.InsertPath(path);

        // Assert
        Assert.Empty(session.SentData);
    }

    [Fact]
    public void InsertPath_FiresFocusTerminalRequestedEvent()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);
        var eventFired = false;
        vm.FocusTerminalRequested += () => eventFired = true;

        // Act
        vm.InsertPath(@"C:\temp\file.txt");

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void NavigateToHistoryDirectory_SendsSetLocationToSessionWithReturn()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.NavigateToHistoryDirectory(@"C:\projekte\app");

        // Assert - Executes with \r
        Assert.Single(session.SentData);
        var sentString = Encoding.UTF8.GetString(session.SentData[0]);
        Assert.Equal("Set-Location -LiteralPath \"C:\\projekte\\app\"\r", sentString);
    }

    [Fact]
    public void NavigateToHistoryDirectory_ForCmd_SendsCdCommandWithReturn()
    {
        // Arrange
        var session = new MockPowerShellSession("CMD 1") { ShellType = ShellType.CMD };
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.NavigateToHistoryDirectory(@"C:\projekte\app");

        // Assert - Uses cd /d for CMD
        Assert.Single(session.SentData);
        var sentString = Encoding.UTF8.GetString(session.SentData[0]);
        Assert.Equal("cd /d \"C:\\projekte\\app\"\r", sentString);
    }

    [Theory]
    [InlineData("chcp 65001", true)]
    [InlineData("chcp 65001 >$null", true)]
    [InlineData("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8", true)]
    [InlineData("$OutputEncoding = [System.Text.Encoding]::UTF8", true)]
    [InlineData("$function:prompt = { ... }", true)]
    [InlineData("Set-PSReadLineOption -AddToHistoryHandler { ... }", true)]
    [InlineData("__multishell_init", true)]
    [InlineData("git status", false)]
    [InlineData("dotnet test", false)]
    [InlineData("dir", false)]
    [InlineData("npm run build", false)]
    [InlineData("cd src", false)]
    [InlineData("cls", false)]
    [InlineData("clear", false)]
    public void IsInternalConfigurationCommand_ValidatesCommandPatterns(string cmd, bool expectedIgnored)
    {
        // Act
        bool result = TerminalTabViewModel.IsInternalConfigurationCommand(cmd);

        // Assert
        Assert.Equal(expectedIgnored, result);
    }

    [Fact]
    public void OnSessionCommandExecuted_FiltersInternalCommandsAndKeepsUserCommands()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session);

        // Act
        session.SimulateCommandExecuted("chcp 65001 >$null");
        session.SimulateCommandExecuted("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8");
        session.SimulateCommandExecuted("git status");
        session.SimulateCommandExecuted("dotnet build");
        session.SimulateCommandExecuted("Set-PSReadLineOption -AddToHistoryHandler { ... }");

        // Assert
        Assert.Equal(2, vm.CommandHistory.Count);
        Assert.Contains("git status", vm.CommandHistory);
        Assert.Contains("dotnet build", vm.CommandHistory);
        Assert.DoesNotContain("chcp 65001 >$null", vm.CommandHistory);
        Assert.DoesNotContain("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8", vm.CommandHistory);
    }

    [Fact]
    public void RestoreHistory_FiltersInternalCommands()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session);
        var rawHistory = new[]
        {
            "chcp 65001",
            "git status",
            "[Console]::OutputEncoding = UTF8",
            "cargo build",
            "$OutputEncoding = UTF8"
        };

        // Act
        vm.RestoreHistory(rawHistory, null);

        // Assert
        Assert.Equal(2, vm.CommandHistory.Count);
        Assert.Contains("git status", vm.CommandHistory);
        Assert.Contains("cargo build", vm.CommandHistory);
        Assert.DoesNotContain("chcp 65001", vm.CommandHistory);
        Assert.DoesNotContain("$OutputEncoding = UTF8", vm.CommandHistory);
    }
}
