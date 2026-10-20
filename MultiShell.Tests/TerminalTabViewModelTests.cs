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

    [Fact]
    public void PageUp_And_PageDown_CanBeCalledWithoutException()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session);

        // Act & Assert (ensure methods invoke on underlying TerminalControlModel safely)
        var exUp = Record.Exception(() => vm.PageUp());
        var exDown = Record.Exception(() => vm.PageDown());

        Assert.Null(exUp);
        Assert.Null(exDown);
    }

    [Fact]
    public void SendPageUp_SendsCorrectVtSequenceToSession()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.SendPageUp(alt: false);

        // Assert
        Assert.Single(session.SentData);
        Assert.Equal("\u001b[5~", Encoding.UTF8.GetString(session.SentData[0]));
    }

    [Fact]
    public void SendPageUp_WithAlt_SendsCorrectVtSequenceToSession()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.SendPageUp(alt: true);

        // Assert
        Assert.Single(session.SentData);
        Assert.Equal("\u001b[5;3~", Encoding.UTF8.GetString(session.SentData[0]));
    }

    [Fact]
    public void SendPageDown_SendsCorrectVtSequenceToSession()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.SendPageDown(alt: false);

        // Assert
        Assert.Single(session.SentData);
        Assert.Equal("\u001b[6~", Encoding.UTF8.GetString(session.SentData[0]));
    }

    [Fact]
    public void SendPageDown_WithAlt_SendsCorrectVtSequenceToSession()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.SendPageDown(alt: true);

        // Assert
        Assert.Single(session.SentData);
        Assert.Equal("\u001b[6;3~", Encoding.UTF8.GetString(session.SentData[0]));
    }

    [Fact]
    public void ClearBuffer_ExecutesWithoutException()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session);

        // Act & Assert
        var ex = Record.Exception(() => vm.ClearBuffer());

        Assert.Null(ex);
    }

    [Fact]
    public void CheckAndRespondToOscColorQueries_WhenDarkTheme_RepliesWithDarkRgbBackground()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);
        vm.UpdateTheme(isDark: true);

        // Act - Simulate incoming OSC 11 background query from Neovim
        vm.CheckAndRespondToOscColorQueries("\x1b]11;?\x07");

        // Assert
        Assert.Single(session.SentData);
        var reply = Encoding.ASCII.GetString(session.SentData[0]);
        Assert.Equal("\x1b]11;rgb:0e0e/0f0f/1515\x1b\\", reply);
    }

    [Fact]
    public void CheckAndRespondToOscColorQueries_WhenLightTheme_RepliesWithLightRgbBackground()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);
        vm.UpdateTheme(isDark: false);

        // Act - Simulate incoming OSC 11 background query from Neovim
        vm.CheckAndRespondToOscColorQueries("\x1b]11;?\x1b\\");

        // Assert
        Assert.Single(session.SentData);
        var reply = Encoding.ASCII.GetString(session.SentData[0]);
        Assert.Equal("\x1b]11;rgb:f8f8/f9f9/fcfc\x1b\\", reply);
    }

    [Fact]
    public void CheckAndRespondToOscColorQueries_Osc10ForegroundQuery_RepliesWithRgbForeground()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);
        vm.UpdateTheme(isDark: true);

        // Act - Simulate incoming OSC 10 foreground query
        vm.CheckAndRespondToOscColorQueries("\x1b]10;?\x07");

        // Assert
        Assert.Single(session.SentData);
        var reply = Encoding.ASCII.GetString(session.SentData[0]);
        Assert.Equal("\x1b]10;rgb:c0c0/caca/f5f5\x1b\\", reply);
    }

    [Fact]
    public void FlushPendingUiFeed_WhenBufferEmpty_DoesNotThrowOrCorruptState()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session);

        // Act & Assert (should be completely safe no-op)
        vm.FlushPendingUiFeed();
        Assert.NotNull(vm.TerminalModel);
    }

    [Fact]
    public void OnSessionDataReceived_WithRapidConsecutiveChunks_BatchesAndPreservesAllText()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session);

        // Act: simulate 3 consecutive rapid chunks representing a spinner and text
        // e.g. chunk 1: clear line (\x1b[2K\r), chunk 2: spinner frame, chunk 3: label text
        var chunk1 = Encoding.UTF8.GetBytes("\x1b[2K\r");
        var chunk2 = Encoding.UTF8.GetBytes("⠋ ");
        var chunk3 = Encoding.UTF8.GetBytes("Thinking...\r\n");

        session.SimulateDataReceived(chunk1);
        session.SimulateDataReceived(chunk2);
        session.SimulateDataReceived(chunk3);

        // Assert: In test context, data feeds through to TerminalModel cleanly
        Assert.NotNull(vm.TerminalModel);
        // Verify buffer has the line content
        var terminalText = vm.TerminalModel.Terminal.Buffer.GetLine(0)?.TranslateToString(true) ?? string.Empty;
        Assert.Contains("Thinking...", terminalText);
    }

    [Fact]
    public void OnSessionDataReceived_BatchedAnsiFormatting_PreservesTextAndColorAttributes()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session);

        // Act: ANSI colored output split across multiple micro-chunks
        session.SimulateDataReceived(Encoding.UTF8.GetBytes("\x1b[32m"));
        session.SimulateDataReceived(Encoding.UTF8.GetBytes("SUCCESS: "));
        session.SimulateDataReceived(Encoding.UTF8.GetBytes("\x1b[0mAll done\r\n"));

        // Assert
        var line = vm.TerminalModel.Terminal.Buffer.GetLine(0)?.TranslateToString(true) ?? string.Empty;
        Assert.Contains("SUCCESS: All done", line);
    }

    [Fact]
    public void TwoTabs_SamePath_ShareAndSyncHistoryDynamically()
    {
        // Arrange
        var historyService = new PathCommandHistoryService();
        var sharedPath = @"C:\projekte\repo";

        var session1 = new MockPowerShellSession("Tab 1", sharedPath);
        using var tab1 = new TerminalTabViewModel(session1, pathCommandHistoryService: historyService);

        var session2 = new MockPowerShellSession("Tab 2", sharedPath);
        using var tab2 = new TerminalTabViewModel(session2, pathCommandHistoryService: historyService);

        // Act - Tab 1 executes a command
        session1.SimulateCommandExecuted("git status");

        // Assert - Both Tab 1 and Tab 2 immediately reflect the command
        Assert.Single(tab1.CommandHistory);
        Assert.Equal("git status", tab1.CommandHistory[0]);
        Assert.Single(tab1.FilteredCommandHistory);
        Assert.Equal("git status", tab1.FilteredCommandHistory[0]);

        Assert.Single(tab2.CommandHistory);
        Assert.Equal("git status", tab2.CommandHistory[0]);
        Assert.Single(tab2.FilteredCommandHistory);
        Assert.Equal("git status", tab2.FilteredCommandHistory[0]);

        // Act - Tab 2 executes another command
        session2.SimulateCommandExecuted("dotnet test");

        // Assert - Both tabs have both commands in same reverse chronological (newest first) order
        Assert.Equal(2, tab1.CommandHistory.Count);
        Assert.Equal(new[] { "dotnet test", "git status" }, tab1.CommandHistory);
        Assert.Equal(2, tab2.CommandHistory.Count);
        Assert.Equal(new[] { "dotnet test", "git status" }, tab2.CommandHistory);
    }

    [Fact]
    public void Tab_ChangesWorkingDirectory_SwitchesToNewPathHistory()
    {
        // Arrange
        var historyService = new PathCommandHistoryService();
        var pathA = @"C:\projekte\frontend";
        var pathB = @"C:\projekte\backend";

        historyService.RecordCommand(pathA, "npm run dev");
        historyService.RecordCommand(pathB, "dotnet run");

        var session = new MockPowerShellSession("Tab", pathA);
        using var tab = new TerminalTabViewModel(session, pathCommandHistoryService: historyService);

        // Assert initially on pathA
        Assert.Single(tab.CommandHistory);
        Assert.Equal("npm run dev", tab.CommandHistory[0]);

        // Act - directory changes to pathB
        session.SimulateDirectoryChange(pathB);

        // Assert dynamically switched to pathB's history
        Assert.Single(tab.CommandHistory);
        Assert.Equal("dotnet run", tab.CommandHistory[0]);
        Assert.Single(tab.FilteredCommandHistory);
        Assert.Equal("dotnet run", tab.FilteredCommandHistory[0]);
    }

    [Fact]
    public void UserInput_CdCommand_SavesUnderSourceDirectory_NotTargetDirectory()
    {
        // Arrange
        var historyService = new PathCommandHistoryService();
        var dirA = @"C:\projekte\folderA";
        var dirB = @"C:\projekte\folderB";

        var session = new MockPowerShellSession("Tab", dirA);
        using var tab = new TerminalTabViewModel(session, pathCommandHistoryService: historyService);
        tab.StartSession();

        // Act - User types 'cd C:\projekte\folderB' and presses Enter while in dirA
        tab.SendInput(Encoding.UTF8.GetBytes($"cd {dirB}\r"));

        // Shell completes execution: directory changes to dirB and OSC command notification arrives
        session.SimulateDirectoryChange(dirB);
        session.SimulateCommandExecuted($"cd {dirB}");

        // Assert
        var historyA = historyService.GetHistory(dirA);
        var historyB = historyService.GetHistory(dirB);

        // Command must be recorded under dirA (where it was executed)
        Assert.Single(historyA);
        Assert.Equal($"cd {dirB}", historyA[0]);

        // Command must NOT be recorded under dirB
        Assert.Empty(historyB);

        // Tab is now in dirB, so its active history reflects dirB (empty)
        Assert.Empty(tab.CommandHistory);
    }

    [Fact]
    public void TerminalTabViewModel_Disposal_UnsubscribesFromLocalizationService()
    {
        // Arrange
        var loc = new LocalizationService();
        var session = new MockPowerShellSession("Test", @"C:\Test");
        var tab = new TerminalTabViewModel(session, localizationService: loc);

        // Act
        tab.Dispose();

        // Trigger property changed on the shared localization service
        var exception = Record.Exception(() =>
        {
            loc.SetLanguage("de");
            loc.SetLanguage("en");
        });

        // Assert - No exception and tab was safely detached
        Assert.Null(exception);
    }

    [Fact]
    public void FindIncompleteEscapeSequenceIndex_WhenOscStringTerminatorIsSplit_BuffersFromOscStart()
    {
        // Arrange: OSC sequence cut between \x1b and \ in the ST terminator
        string chunkWithSplitSt = "Hello \x1b]9;9;\"C:\\Projects\"\x1b";

        // Act
        int index = TerminalTabViewModel.FindIncompleteEscapeSequenceIndex(chunkWithSplitSt);

        // Assert: Must buffer starting at the beginning of the OSC sequence (index 6), not just trailing \x1b
        Assert.Equal(6, index);
    }

    [Fact]
    public void FindIncompleteEscapeSequenceIndex_WhenDcsStringTerminatorIsSplit_BuffersFromDcsStart()
    {
        // Arrange: DCS sequence cut between \x1b and \ in the ST terminator
        string chunkWithSplitSt = "Output \x1bP1$r\x1b";

        // Act
        int index = TerminalTabViewModel.FindIncompleteEscapeSequenceIndex(chunkWithSplitSt);

        // Assert: Must buffer starting at the beginning of the DCS sequence (index 7)
        Assert.Equal(7, index);
    }

    [Fact]
    public void CheckForDirectoryChangeCommand_CmdDriveChangeWithSlashD_UpdatesDirectory()
    {
        // Arrange
        var tempDir = System.IO.Path.GetTempPath().TrimEnd('\\');
        var parentDir = System.IO.Directory.GetParent(tempDir)?.FullName ?? @"C:\";
        var session = new MockPowerShellSession("CMD", parentDir) { ShellType = ShellType.CMD };
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        // Act: Send 'cd /d <tempDir>\r' via raw input
        vm.SendInput(Encoding.UTF8.GetBytes($"cd /d \"{tempDir}\"\r"));

        // Assert: Working directory must be updated to the target drive/directory
        Assert.Equal(tempDir, vm.WorkingDirectory);
    }

    [Fact]
    public void CheckForDirectoryChangeCommand_CmdCdDotDot_UpdatesDirectory()
    {
        // Arrange
        var tempDir = System.IO.Path.GetTempPath().TrimEnd('\\');
        var parentDir = System.IO.Directory.GetParent(tempDir)?.FullName;
        if (string.IsNullOrEmpty(parentDir)) return; // Skip if root
        var session = new MockPowerShellSession("CMD", tempDir) { ShellType = ShellType.CMD };
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        // Act: Send 'cd..\r'
        vm.SendInput(Encoding.UTF8.GetBytes("cd..\r"));

        // Assert
        Assert.Equal(parentDir, vm.WorkingDirectory);
    }

    [Fact]
    public void TrackInputBuffer_StandaloneLinefeed_AppendsNewlineWithoutSubmittingCommand()
    {
        // Arrange
        var session = new MockPowerShellSession("CMD", @"C:\Test") { ShellType = ShellType.CMD };
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        // Act: Simulate Shift+Enter (standalone \n)
        vm.SendInput(Encoding.UTF8.GetBytes("function test()"));
        vm.SendInput([0x0A]); // standalone \n
        vm.SendInput(Encoding.UTF8.GetBytes(" { echo 1; }\r"));

        // Assert: Entire multi-line command should be recorded on \r submission
        Assert.Single(vm.CommandHistory);
        Assert.Contains("function test()", vm.CommandHistory[0]);
        Assert.Contains("{ echo 1; }", vm.CommandHistory[0]);
    }

    [Fact]
    public void TerminalTabViewModel_Disposal_ClearsEventSubscriptionsAndRejectsDataReceived()
    {
        // Arrange
        var session = new MockPowerShellSession("Test", @"C:\Test");
        var tab = new TerminalTabViewModel(session);
        bool closeCalled = false;
        tab.CloseRequested += _ => closeCalled = true;

        // Act
        tab.Dispose();
        session.SimulateDataReceived(Encoding.UTF8.GetBytes("data after disposal\r\n"));
        tab.RequestClose();

        // Assert: Disposed tab must not invoke events or crash
        Assert.False(closeCalled);
    }

    [Fact]
    public void FormatMiddleEllipsis_WhenLeafTruncated_DoesNotExceedMaxLength()
    {
        // Arrange: Path with long leaf where leaf must be truncated
        string path = @"C:\Users\tester\multishell_extremely_long_repository_name";
        int maxLen = 22;

        // Act
        string result = TerminalTabViewModel.FormatMiddleEllipsis(path, maxLen);

        // Assert: Length must strictly not exceed maxLength
        Assert.True(result.Length <= maxLen, $"Result '{result}' length {result.Length} exceeded {maxLen}");
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void FormatMiddleEllipsis_WhenUncPath_PreservesDoubleBackslash()
    {
        // Arrange
        string uncPath = @"\\fileserver\share\project\subfolder\file.txt";

        // Act
        string result = TerminalTabViewModel.FormatMiddleEllipsis(uncPath, 25);

        // Assert: UNC root must start with \\
        Assert.StartsWith(@"\\fileserver\", result);
    }

    [Fact]
    public void HasTabColor_WhenInvalidColorString_ReturnsFalse()
    {
        // Arrange
        var session = new MockPowerShellSession("Test");
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.SetTabColor("not-a-valid-color");

        // Assert
        Assert.False(vm.HasTabColor);
        Assert.Null(vm.TabColorBrush);
    }

    [Fact]
    public void StartRenaming_WhenAlreadyRenaming_PreservesEditedBuffer()
    {
        // Arrange
        var session = new MockPowerShellSession("Test");
        using var vm = new TerminalTabViewModel(session) { Title = "Initial Title" };

        // Act: Start renaming and type modifications into the buffer
        vm.StartRenaming();
        vm.RenameBuffer = "In-progress title edit";

        // Re-trigger StartRenaming (e.g. repeated F2)
        vm.StartRenaming();

        // Assert: Buffer must not be reset to initial title
        Assert.True(vm.IsRenaming);
        Assert.Equal("In-progress title edit", vm.RenameBuffer);
    }

    [Fact]
    public void TabTooltip_WhenCustomTitleEqualsWorkingDirectory_DoesNotDuplicatePath()
    {
        // Arrange
        string path = @"C:\projects\multishell";
        var session = new MockPowerShellSession("Test", path);
        using var vm = new TerminalTabViewModel(session) { CustomTitle = path };

        // Assert: Must not format as 'C:\projects\multishell (C:\projects\multishell)'
        Assert.Equal(path, vm.TabTooltip);
    }

    [Fact]
    public void TabTooltip_WhenCustomTitleHasTrailingSlashAndEqualsWorkingDirectory_DoesNotDuplicatePath()
    {
        // Arrange
        string path = @"C:\projects\multishell";
        var session = new MockPowerShellSession("Test", path);
        using var vm = new TerminalTabViewModel(session) { CustomTitle = path + @"\" };

        // Assert: Trailing separator differences must be normalized
        Assert.Equal(path + @"\", vm.TabTooltip);
    }

    [Fact]
    public void SetTabColor_WhenInvalidHex_SetsNull()
    {
        // Arrange
        var session = new MockPowerShellSession("Test");
        using var vm = new TerminalTabViewModel(session);

        // Act
        vm.SetTabColor("not-a-color-hex");

        // Assert
        Assert.Null(vm.TabColor);
        Assert.False(vm.HasTabColor);
    }

    [Fact]
    public void FormatMiddleEllipsis_WithLongRootAndShortLeaf_NeverExceedsMaxLength()
    {
        // Arrange: Path where root+leaf compact format exceeds maxLength, but leaf is short
        string path = @"\\verylongnasstoragehostname\sharename\project\src\app";
        int maxLen = 22;

        // Act
        string result = TerminalTabViewModel.FormatMiddleEllipsis(path, maxLen);

        // Assert: Length must strictly not exceed maxLength
        Assert.True(result.Length <= maxLen, $"Result '{result}' length {result.Length} exceeded {maxLen}");
    }

    [Theory]
    [InlineData("abcdefgh", 0, "")]
    [InlineData("abcdefgh", -1, "")]
    [InlineData("abcdefgh", 2, "ab")]
    [InlineData("abcdefgh", 3, "abc")]
    [InlineData("abcdefgh", 4, "abcd")]
    [InlineData("abcdefgh", 5, "a...h")]
    public void FormatMiddleEllipsis_WithSmallMaxLength_NeverExceedsMaxLength(string text, int maxLen, string expected)
    {
        // Act
        string result = TerminalTabViewModel.FormatMiddleEllipsis(text, maxLen);

        // Assert
        Assert.Equal(expected, result);
        if (maxLen > 0)
        {
            Assert.True(result.Length <= maxLen);
        }
    }

    [Fact]
    public void CheckForDirectoryChangeCommand_CmdDriveSwitch_UpdatesWorkingDirectory()
    {
        // Arrange
        var session = new MockPowerShellSession("CMD", @"C:\Windows") { ShellType = ShellType.CMD };
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        // Act: Send 'C:\r' to switch drive
        vm.SendInput(Encoding.UTF8.GetBytes("C:\r"));

        // Assert: Working directory should be normalized to C:\
        Assert.Equal(@"C:\", vm.WorkingDirectory);
    }

    [Fact]
    public void CheckForDirectoryChangeCommand_WslRootPath_UpdatesWorkingDirectory()
    {
        // Arrange
        var session = new MockPowerShellSession("WSL", "/home/user") { ShellType = ShellType.WSL };
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        // Act: Send 'cd /var/log\r'
        vm.SendInput(Encoding.UTF8.GetBytes("cd /var/log\r"));

        // Assert: Working directory should update to /var/log
        Assert.Equal("/var/log", vm.WorkingDirectory);
    }

    [Fact]
    public void ExecuteHistoryCommand_NonPowerShellShell_TriggersSessionCommandExecuted()
    {
        // Arrange
        var session = new MockPowerShellSession("CMD", @"C:\Test") { ShellType = ShellType.CMD };
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        // Act
        vm.ExecuteHistoryCommand("dir /w");

        // Assert: Command history should record command even without OSC 133;E
        Assert.Contains("dir /w", vm.CommandHistory);
    }

    [Fact]
    public void NavigateToHistoryDirectory_NonPowerShellShell_UpdatesWorkingDirectory()
    {
        // Arrange
        var session = new MockPowerShellSession("WSL", "/home/user") { ShellType = ShellType.WSL };
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        // Act
        vm.NavigateToHistoryDirectory("/etc/nginx");

        // Assert: Working directory should update to /etc/nginx
        Assert.Equal("/etc/nginx", vm.WorkingDirectory);
    }
}



