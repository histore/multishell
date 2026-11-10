using System.Text;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public partial class TerminalTabViewModelTests
{
    [Fact]
    public void PageUp_And_PageDown_CanBeCalledWithoutException()
    {
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session);

        var exUp = Record.Exception(() => vm.PageUp());
        var exDown = Record.Exception(() => vm.PageDown());

        Assert.Null(exUp);
        Assert.Null(exDown);
    }

    [Fact]
    public void SendPageUp_SendsCorrectVtSequenceToSession()
    {
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        vm.SendPageUp(alt: false);

        Assert.Single(session.SentData);
        Assert.Equal("\u001b[5~", Encoding.UTF8.GetString(session.SentData[0]));
    }

    [Fact]
    public void SendPageUp_WithAlt_SendsCorrectVtSequenceToSession()
    {
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        vm.SendPageUp(alt: true);

        Assert.Single(session.SentData);
        Assert.Equal("\u001b[5;3~", Encoding.UTF8.GetString(session.SentData[0]));
    }

    [Fact]
    public void SendPageDown_SendsCorrectVtSequenceToSession()
    {
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        vm.SendPageDown(alt: false);

        Assert.Single(session.SentData);
        Assert.Equal("\u001b[6~", Encoding.UTF8.GetString(session.SentData[0]));
    }

    [Fact]
    public void SendPageDown_WithAlt_SendsCorrectVtSequenceToSession()
    {
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);

        vm.SendPageDown(alt: true);

        Assert.Single(session.SentData);
        Assert.Equal("\u001b[6;3~", Encoding.UTF8.GetString(session.SentData[0]));
    }

    [Fact]
    public void ClearBuffer_ExecutesWithoutException()
    {
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session);

        var ex = Record.Exception(() => vm.ClearBuffer());

        Assert.Null(ex);
    }

    [Fact]
    public void CheckAndRespondToOscColorQueries_WhenDarkTheme_RepliesWithDarkRgbBackground()
    {
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);
        vm.UpdateTheme(isDark: true);

        vm.CheckAndRespondToOscColorQueries("\x1b]11;?\x07");

        Assert.Single(session.SentData);
        var reply = Encoding.ASCII.GetString(session.SentData[0]);
        Assert.Equal("\x1b]11;rgb:0e0e/0f0f/1515\x1b\\", reply);
    }

    [Fact]
    public void CheckAndRespondToOscColorQueries_WhenLightTheme_RepliesWithLightRgbBackground()
    {
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);
        vm.UpdateTheme(isDark: false);

        vm.CheckAndRespondToOscColorQueries("\x1b]11;?\x1b\\");

        Assert.Single(session.SentData);
        var reply = Encoding.ASCII.GetString(session.SentData[0]);
        Assert.Equal("\x1b]11;rgb:f8f8/f9f9/fcfc\x1b\\", reply);
    }

    [Fact]
    public void CheckAndRespondToOscColorQueries_Osc10ForegroundQuery_RepliesWithRgbForeground()
    {
        var session = new MockPowerShellSession("Test Tab");
        session.Start();
        using var vm = new TerminalTabViewModel(session);
        vm.UpdateTheme(isDark: true);

        vm.CheckAndRespondToOscColorQueries("\x1b]10;?\x07");

        Assert.Single(session.SentData);
        var reply = Encoding.ASCII.GetString(session.SentData[0]);
        Assert.Equal("\x1b]10;rgb:c0c0/caca/f5f5\x1b\\", reply);
    }

    [Fact]
    public void FlushPendingUiFeed_WhenBufferEmpty_DoesNotThrowOrCorruptState()
    {
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session);

        vm.FlushPendingUiFeed();
        Assert.NotNull(vm.TerminalModel);
    }

    [Fact]
    public void OnSessionDataReceived_WithRapidConsecutiveChunks_BatchesAndPreservesAllText()
    {
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session);

        var chunk1 = Encoding.UTF8.GetBytes("\x1b[2K\r");
        var chunk2 = Encoding.UTF8.GetBytes("⠋ ");
        var chunk3 = Encoding.UTF8.GetBytes("Thinking...\r\n");

        session.SimulateDataReceived(chunk1);
        session.SimulateDataReceived(chunk2);
        session.SimulateDataReceived(chunk3);

        Assert.NotNull(vm.TerminalModel);
        var terminalText = vm.TerminalModel.Terminal.Buffer.GetLine(0)?.TranslateToString(true) ?? string.Empty;
        Assert.Contains("Thinking...", terminalText);
    }

    [Fact]
    public void OnSessionDataReceived_BatchedAnsiFormatting_PreservesTextAndColorAttributes()
    {
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session);

        session.SimulateDataReceived(Encoding.UTF8.GetBytes("\x1b[32m"));
        session.SimulateDataReceived(Encoding.UTF8.GetBytes("SUCCESS: "));
        session.SimulateDataReceived(Encoding.UTF8.GetBytes("\x1b[0mAll done\r\n"));

        var line = vm.TerminalModel.Terminal.Buffer.GetLine(0)?.TranslateToString(true) ?? string.Empty;
        Assert.Contains("SUCCESS: All done", line);
    }

    [Fact]
    public void TwoTabs_SamePath_ShareAndSyncHistoryDynamically()
    {
        var historyService = new PathCommandHistoryService();
        var sharedPath = @"C:\projekte\repo";

        var session1 = new MockPowerShellSession("Tab 1", sharedPath);
        using var tab1 = new TerminalTabViewModel(session1, pathCommandHistoryService: historyService);

        var session2 = new MockPowerShellSession("Tab 2", sharedPath);
        using var tab2 = new TerminalTabViewModel(session2, pathCommandHistoryService: historyService);

        session1.SimulateCommandExecuted("git status");

        Assert.Single(tab1.CommandHistory);
        Assert.Equal("git status", tab1.CommandHistory[0]);
        Assert.Single(tab1.FilteredCommandHistory);
        Assert.Equal("git status", tab1.FilteredCommandHistory[0]);

        Assert.Single(tab2.CommandHistory);
        Assert.Equal("git status", tab2.CommandHistory[0]);
        Assert.Single(tab2.FilteredCommandHistory);
        Assert.Equal("git status", tab2.FilteredCommandHistory[0]);

        session2.SimulateCommandExecuted("dotnet test");

        Assert.Equal(2, tab1.CommandHistory.Count);
        Assert.Equal(new[] { "dotnet test", "git status" }, tab1.CommandHistory);
        Assert.Equal(2, tab2.CommandHistory.Count);
        Assert.Equal(new[] { "dotnet test", "git status" }, tab2.CommandHistory);
    }

    [Fact]
    public void Tab_ChangesWorkingDirectory_SwitchesToNewPathHistory()
    {
        var historyService = new PathCommandHistoryService();
        var pathA = @"C:\projekte\frontend";
        var pathB = @"C:\projekte\backend";

        historyService.RecordCommand(pathA, "npm run dev");
        historyService.RecordCommand(pathB, "dotnet run");

        var session = new MockPowerShellSession("Tab", pathA);
        using var tab = new TerminalTabViewModel(session, pathCommandHistoryService: historyService);

        Assert.Single(tab.CommandHistory);
        Assert.Equal("npm run dev", tab.CommandHistory[0]);

        session.SimulateDirectoryChange(pathB);

        Assert.Single(tab.CommandHistory);
        Assert.Equal("dotnet run", tab.CommandHistory[0]);
        Assert.Single(tab.FilteredCommandHistory);
        Assert.Equal("dotnet run", tab.FilteredCommandHistory[0]);
    }

    [Fact]
    public void UserInput_CdCommand_SavesUnderSourceDirectory_NotTargetDirectory()
    {
        var historyService = new PathCommandHistoryService();
        var dirA = @"C:\projekte\folderA";
        var dirB = @"C:\projekte\folderB";

        var session = new MockPowerShellSession("Tab", dirA);
        using var tab = new TerminalTabViewModel(session, pathCommandHistoryService: historyService);
        tab.StartSession();

        tab.SendInput(Encoding.UTF8.GetBytes($"cd {dirB}\r"));

        session.SimulateDirectoryChange(dirB);
        session.SimulateCommandExecuted($"cd {dirB}");

        var historyA = historyService.GetHistory(dirA);
        var historyB = historyService.GetHistory(dirB);

        Assert.Single(historyA);
        Assert.Equal($"cd {dirB}", historyA[0]);
        Assert.Empty(historyB);
        Assert.Empty(tab.CommandHistory);
    }

    [Fact]
    public void TerminalTabViewModel_Disposal_UnsubscribesFromLocalizationService()
    {
        var loc = new LocalizationService();
        var session = new MockPowerShellSession("Test", @"C:\Test");
        var tab = new TerminalTabViewModel(session, localizationService: loc);

        tab.Dispose();

        var exception = Record.Exception(() =>
        {
            loc.SetLanguage("de");
            loc.SetLanguage("en");
        });

        Assert.Null(exception);
    }

    [Fact]
    public void FindIncompleteEscapeSequenceIndex_WhenOscStringTerminatorIsSplit_BuffersFromOscStart()
    {
        string chunkWithSplitSt = "Hello \x1b]9;9;\"C:\\Projects\"\x1b";

        int index = TerminalTabViewModel.FindIncompleteEscapeSequenceIndex(chunkWithSplitSt);

        Assert.Equal(6, index);
    }

    [Fact]
    public void FindIncompleteEscapeSequenceIndex_WhenDcsStringTerminatorIsSplit_BuffersFromDcsStart()
    {
        string chunkWithSplitSt = "Output \x1bP1$r\x1b";

        int index = TerminalTabViewModel.FindIncompleteEscapeSequenceIndex(chunkWithSplitSt);

        Assert.Equal(7, index);
    }
}
