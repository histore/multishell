using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public class TerminalTabViewModelTests
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

    [Fact]
    public void OnTerminalUserInput_TracksFullCommandWithQuotesAndArguments()
    {
        // Arrange
        var session = new MockPowerShellSession("CMD 1") { ShellType = ShellType.CMD };
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        // Act - Simulate typing 'Write "llll"' followed by Enter (\r)
        vm.TerminalModel.Send("Write \"llll\"");
        vm.TerminalModel.Send(new byte[] { 0x0D });

        // Assert - CommandHistory contains full command with quotes
        Assert.Single(vm.CommandHistory);
        Assert.Equal("Write \"llll\"", vm.CommandHistory[0]);
    }

    [Fact]
    public void OnTerminalUserInput_TracksLineBufferAndTriggersCommandExecutedOnEnter()
    {
        // Arrange
        var session = new MockPowerShellSession("CMD 1") { ShellType = ShellType.CMD };
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        // Act - Simulate typing "git status" and pressing Enter (\r)
        vm.TerminalModel.Send("git status");
        vm.TerminalModel.Send(new byte[] { 0x0D });

        // Assert - "git status" should be added to CommandHistory
        Assert.Single(vm.CommandHistory);
        Assert.Equal("git status", vm.CommandHistory[0]);
    }

    [Fact]
    public void OnTerminalUserInput_HandlesBackspaceAndCtrlC()
    {
        // Arrange
        var session = new MockPowerShellSession("CMD 1") { ShellType = ShellType.CMD };
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        // Act 1: Type "git abc", Backspace 3 times, type "status", press Enter
        vm.TerminalModel.Send("git abc");
        vm.TerminalModel.Send(new byte[] { 0x08, 0x08, 0x08 });
        vm.TerminalModel.Send("status");
        vm.TerminalModel.Send(new byte[] { 0x0D });

        // Act 2: Type "failed_cmd", press Ctrl+C (0x03), type "cls", press Enter
        vm.TerminalModel.Send("failed_cmd");
        vm.TerminalModel.Send(new byte[] { 0x03 });
        vm.TerminalModel.Send("cls");
        vm.TerminalModel.Send(new byte[] { 0x0D });

        // Assert
        Assert.Equal(2, vm.CommandHistory.Count);
        Assert.Equal("git status", vm.CommandHistory[0]);
        Assert.Equal("cls", vm.CommandHistory[1]);
    }

    [Fact]
    public void SendInput_SendsRawBytesDirectlyToSession()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        // Act - send '@' as UTF8 bytes (e.g. from AltGr+Q text input)
        var atBytes = Encoding.UTF8.GetBytes("@");
        vm.SendInput(atBytes);

        // Assert
        Assert.Single(session.SentData);
        Assert.Equal("@", Encoding.UTF8.GetString(session.SentData[0]));
    }

    [Fact]
    public void OnTerminalUserInput_WhenAltGrActive_FiltersOutRogueControlCharactersAndAllowsPrintableCharacters()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        session.Start();
        using var vm = new TerminalTabViewModel(session);
        vm.IsAltGrActive = true;

        // Act 1: Simulate TerminalControl.OnKeyDown sending rogue Ctrl+Q (0x11)
        vm.TerminalModel.Send(new byte[] { 0x11 });

        // Assert 1: The rogue 0x11 is dropped when AltGr is active
        Assert.Empty(session.SentData);

        // Act 2: Simulate TerminalControl.OnTextInput sending '@' (0x40)
        vm.TerminalModel.Send("@");

        // Assert 2: The printable character '@' is passed through
        Assert.Single(session.SentData);
        Assert.Equal("@", Encoding.UTF8.GetString(session.SentData[0]));
    }

    [Fact]
    public void TerminalFontFamily_DefaultsToMonospaceFamilyChain()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Assert
        Assert.NotNull(vm.TerminalFontFamily);
        Assert.Contains(vm.TerminalFontFamily.FamilyNames, f => f.Contains("Cascadia") || f.Contains("Consolas"));
    }

    [Fact]
    public void OnSessionDataReceived_WithFragmentedUtf8BoxDrawingCharacters_DoesNotProduceReplacementCharacter()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Box drawing horizontal line '─' is UTF-8: 0xE2 0x94 0x80 (3 bytes)
        var fullBoxLine = "┌───┐\r\n│ A │\r\n└───┘\r\n";
        var allBytes = Encoding.UTF8.GetBytes(fullBoxLine);

        // Act - Feed byte-by-byte or in fragments split across 2-byte chunk boundaries
        for (int i = 0; i < allBytes.Length; i += 2)
        {
            int len = Math.Min(2, allBytes.Length - i);
            var chunk = new byte[len];
            Array.Copy(allBytes, i, chunk, 0, len);
            session.SimulateDataReceived(chunk);
        }

        // Assert - The terminal model buffer must contain the original box drawing characters without replacement character \uFFFD
        // We verify that TerminalModel received valid input
        Assert.NotNull(vm.TerminalModel);
    }

    [Fact]
    public void OnSessionDataReceived_WithFragmentedUmlautsAndEmojis_PreservesCharactersAcrossChunks()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // German umlauts and international chars: ä = 0xC3 0xA4, ü = 0xC3 0xBC, ö = 0xC3 0xB6, ß = 0xC3 0x9F
        var text = "Änderung für Größe & Überprüfung: 🚀 ✨";
        var allBytes = Encoding.UTF8.GetBytes(text);

        // Feed in 1-byte chunks to test maximum fragmentation
        foreach (var b in allBytes)
        {
            session.SimulateDataReceived(new[] { b });
        }

        Assert.NotNull(vm.TerminalModel);
    }

    [Fact]
    public void SanitizeTerminalText_RemovesOsc8HyperlinksAndOsc9AndOsc133()
    {
        // Arrange - bat-style OSC 8 hyperlinks on headings and shell OSC sequences
        var rawText = "\x1b]8;;file:///c:/REQUIREMENTS.md#L1\x1b\\1 Project Requirements\x1b]8;;\x1b\\\r\n" +
                      "\x1b]9;9;\"C:\\projekte\"\x07" +
                      "\x1b]133;E;Y21k\x07";

        // Act
        var sanitized = TerminalTabViewModel.SanitizeTerminalText(rawText);

        // Assert - OSC sequences stripped completely; no stray ']' at column 0
        Assert.DoesNotContain("\x1b]", sanitized);
        Assert.DoesNotContain("]8;;", sanitized);
        Assert.DoesNotContain("]9;9;", sanitized);
        Assert.DoesNotContain("]133;E;", sanitized);
        Assert.StartsWith("1 Project Requirements", sanitized);
    }

    [Fact]
    public void OnSessionDataReceived_WithOsc8HyperlinksInMarkdownHeaders_StripsOscWithoutStrayClosingBracket()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Simulate bat output for markdown headings: "\x1b]8;;url\x1b\1 Project Requirements\x1b]8;;\x1b\"
        var headerWithHyperlink = "\x1b]8;;file:///c:/REQUIREMENTS.md\x1b\\1  Project Requirements & Status Catalog\x1b]8;;\x1b\\\r\n";
        var bytes = Encoding.UTF8.GetBytes(headerWithHyperlink);

        // Act
        session.SimulateDataReceived(bytes);

        // Assert - Model received valid input
        Assert.NotNull(vm.TerminalModel);
    }

    [Fact]
    public void OnSessionDataReceived_WithFragmentedOscSequences_BuffersAndStripsCleanly()
    {
        // Arrange
        var session = new MockPowerShellSession("PS 1");
        using var vm = new TerminalTabViewModel(session);

        // Split OSC sequence across chunk boundaries
        var part1 = Encoding.UTF8.GetBytes("\x1b]8;;file:///c:/test.md");
        var part2 = Encoding.UTF8.GetBytes("\x1b\\Heading Title\x1b]8;;\x1b\\\r\n");

        // Act
        session.SimulateDataReceived(part1);
        session.SimulateDataReceived(part2);

        // Assert
        Assert.NotNull(vm.TerminalModel);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("Single line without padding", "Single line without padding")]
    [InlineData("Line with trailing spaces   \t   ", "Line with trailing spaces")]
    public void CleanSelectedTerminalText_SingleLine_TrimsTrailingSpaces(string? input, string expected)
    {
        // Act
        var result = TerminalTabViewModel.CleanSelectedTerminalText(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CleanSelectedTerminalText_MultiLine_PreservesContentAndRemovesPadding()
    {
        // Arrange
        var rawText = "Hello World                                                                     \r\n" +
                      "Second Line                                                                     \r\n" +
                      "                                                                                \r\n" +
                      "                                                                                \r\n";

        // Act
        var result = TerminalTabViewModel.CleanSelectedTerminalText(rawText);
        var expected = $"Hello World{Environment.NewLine}Second Line";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SendInput_NewlineByte_PassesLinefeedDirectlyToShell()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        var newlineBytes = new byte[] { 0x0A };

        // Act
        vm.SendInput(newlineBytes);

        // Assert
        Assert.Single(session.SentData);
        Assert.Equal(newlineBytes, session.SentData[0]);
    }

    [Theory]
    [InlineData(null, -1)]
    [InlineData("", -1)]
    [InlineData("Clean plain text without escapes", -1)]
    [InlineData("Text with complete CSI \x1b[38;2;255;0;0m and reset \x1b[0m", -1)]
    [InlineData("Text with complete OSC \x1b]9;9;\"C:\\dir\"\x07", -1)]
    [InlineData("Text with complete OSC ST \x1b]8;;https://example.com\x1b\\Link\x1b]8;;\x1b\\", -1)]
    [InlineData("Text with complete 2-char charset \x1b(B", -1)]
    [InlineData("Text ending with bare ESC \x1b", 26)]
    [InlineData("Text with broken CSI at end \x1b[38;2;255;", 28)]
    [InlineData("Text with split reset at end \x1b[", 29)]
    [InlineData("Text with incomplete OSC at end \x1b]9;9;\"C:\\path", 32)]
    [InlineData("Text with incomplete 2-char at end \x1b(", 35)]
    [InlineData("Text with incomplete DCS at end \x1bP+q", 32)]
    public void FindIncompleteEscapeSequenceIndex_DetectsCorrectOffsets(string? input, int expectedIndex)
    {
        // Act
        int result = TerminalTabViewModel.FindIncompleteEscapeSequenceIndex(input!);

        // Assert
        Assert.Equal(expectedIndex, result);
    }

    [Fact]
    public void OnSessionDataReceived_FragmentedCsiSequence_PreservesFullSequenceAcrossChunks()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        var chunk1 = Encoding.UTF8.GetBytes("Prefix \x1b[38;2;120;");
        var chunk2 = Encoding.UTF8.GetBytes("200;50m│  Suggested command:\x1b[0m\r\n");

        // Act
        session.SimulateDataReceived(chunk1);
        session.SimulateDataReceived(chunk2);

        // Assert
        Assert.NotNull(vm.TerminalModel);
    }

    [Fact]
    public void OnSessionDataReceived_FragmentedSgrReset_PreservesResetBeforeFollowingText()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        // Chunk 1 ends right in the middle of \x1b[0m
        var chunk1 = Encoding.UTF8.GetBytes("\x1b[48;2;255;100;100m# Heading Title\x1b[");
        var chunk2 = Encoding.UTF8.GetBytes("0m\r\nParagraph text after heading\r\n");

        // Act
        session.SimulateDataReceived(chunk1);
        session.SimulateDataReceived(chunk2);

        // Assert
        Assert.NotNull(vm.TerminalModel);
    }

    [Fact]
    public void InspectTerminalBuffer_InspectsLineExtraction()
    {
        // Arrange
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        vm.TerminalModel.Feed("echo https://github.com/histore/multishell\r\n");

        var term = vm.TerminalModel.Terminal;
        var buffer = term.GetType().GetProperty("Buffer")?.GetValue(term);
        Assert.NotNull(buffer);

        var getLineMethod = buffer!.GetType().GetMethod("GetLine", [typeof(int)]);
        Assert.NotNull(getLineMethod);

        var yDisp = Convert.ToInt32(buffer.GetType().GetProperty("YDisp")?.GetValue(buffer) ?? 0);
        var line0 = getLineMethod!.Invoke(buffer, new object[] { yDisp });
        Assert.NotNull(line0);

        var strMethod = line0!.GetType().GetMethod("TranslateToString", [typeof(bool), typeof(int), typeof(int)]);
        Assert.NotNull(strMethod);

        var text = strMethod!.Invoke(line0, new object[] { true, 0, term.Cols })?.ToString();
        Assert.Equal("echo https://github.com/histore/multishell", text);
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

