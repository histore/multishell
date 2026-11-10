using System.Text;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public partial class TerminalTabViewModelTests
{
    [Fact]
    public void CheckForDirectoryChangeCommand_CmdDriveChangeWithSlashD_UpdatesDirectory()
    {
        var tempDir = System.IO.Path.GetTempPath().TrimEnd('\\');
        var parentDir = System.IO.Directory.GetParent(tempDir)?.FullName ?? @"C:\";
        var session = new MockPowerShellSession("CMD", parentDir) { ShellType = ShellType.CMD };
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        vm.SendInput(Encoding.UTF8.GetBytes($"cd /d \"{tempDir}\"\r"));

        Assert.Equal(tempDir, vm.WorkingDirectory);
    }

    [Fact]
    public void CheckForDirectoryChangeCommand_CmdCdDotDot_UpdatesDirectory()
    {
        var tempDir = System.IO.Path.GetTempPath().TrimEnd('\\');
        var parentDir = System.IO.Directory.GetParent(tempDir)?.FullName;
        if (string.IsNullOrEmpty(parentDir)) return;
        var session = new MockPowerShellSession("CMD", tempDir) { ShellType = ShellType.CMD };
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        vm.SendInput(Encoding.UTF8.GetBytes("cd..\r"));

        Assert.Equal(parentDir, vm.WorkingDirectory);
    }

    [Fact]
    public void TrackInputBuffer_StandaloneLinefeed_AppendsNewlineWithoutSubmittingCommand()
    {
        var session = new MockPowerShellSession("CMD", @"C:\Test") { ShellType = ShellType.CMD };
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        vm.SendInput(Encoding.UTF8.GetBytes("function test()"));
        vm.SendInput([0x0A]);
        vm.SendInput(Encoding.UTF8.GetBytes(" { echo 1; }\r"));

        Assert.Single(vm.CommandHistory);
        Assert.Contains("function test()", vm.CommandHistory[0]);
        Assert.Contains("{ echo 1; }", vm.CommandHistory[0]);
    }

    [Fact]
    public void TerminalTabViewModel_Disposal_ClearsEventSubscriptionsAndRejectsDataReceived()
    {
        var session = new MockPowerShellSession("Test", @"C:\Test");
        var tab = new TerminalTabViewModel(session);
        bool closeCalled = false;
        tab.CloseRequested += _ => closeCalled = true;

        tab.Dispose();
        session.SimulateDataReceived(Encoding.UTF8.GetBytes("data after disposal\r\n"));
        tab.RequestClose();

        Assert.False(closeCalled);
    }

    [Fact]
    public void FormatMiddleEllipsis_WhenLeafTruncated_DoesNotExceedMaxLength()
    {
        string path = @"C:\Users\tester\multishell_extremely_long_repository_name";
        int maxLen = 22;

        string result = TerminalTabViewModel.FormatMiddleEllipsis(path, maxLen);

        Assert.True(result.Length <= maxLen, $"Result '{result}' length {result.Length} exceeded {maxLen}");
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void FormatMiddleEllipsis_WhenUncPath_PreservesDoubleBackslash()
    {
        string uncPath = @"\\fileserver\share\project\subfolder\file.txt";

        string result = TerminalTabViewModel.FormatMiddleEllipsis(uncPath, 25);

        Assert.StartsWith(@"\\fileserver\", result);
    }

    [Fact]
    public void HasTabColor_WhenInvalidColorString_ReturnsFalse()
    {
        var session = new MockPowerShellSession("Test");
        using var vm = new TerminalTabViewModel(session);

        vm.SetTabColor("not-a-valid-color");

        Assert.False(vm.HasTabColor);
        Assert.Null(vm.TabColorBrush);
    }

    [Fact]
    public void StartRenaming_WhenAlreadyRenaming_PreservesEditedBuffer()
    {
        var session = new MockPowerShellSession("Test");
        using var vm = new TerminalTabViewModel(session) { Title = "Initial Title" };

        vm.StartRenaming();
        vm.RenameBuffer = "In-progress title edit";
        vm.StartRenaming();

        Assert.True(vm.IsRenaming);
        Assert.Equal("In-progress title edit", vm.RenameBuffer);
    }

    [Fact]
    public void TabTooltip_WhenCustomTitleEqualsWorkingDirectory_DoesNotDuplicatePath()
    {
        string path = @"C:\projects\multishell";
        var session = new MockPowerShellSession("Test", path);
        using var vm = new TerminalTabViewModel(session) { CustomTitle = path };

        Assert.Equal(path, vm.TabTooltip);
    }

    [Fact]
    public void TabTooltip_WhenCustomTitleHasTrailingSlashAndEqualsWorkingDirectory_DoesNotDuplicatePath()
    {
        string path = @"C:\projects\multishell";
        var session = new MockPowerShellSession("Test", path);
        using var vm = new TerminalTabViewModel(session) { CustomTitle = path + @"\" };

        Assert.Equal(path + @"\", vm.TabTooltip);
    }

    [Fact]
    public void SetTabColor_WhenInvalidHex_SetsNull()
    {
        var session = new MockPowerShellSession("Test");
        using var vm = new TerminalTabViewModel(session);

        vm.SetTabColor("not-a-color-hex");

        Assert.Null(vm.TabColor);
        Assert.False(vm.HasTabColor);
    }

    [Fact]
    public void FormatMiddleEllipsis_WithLongRootAndShortLeaf_NeverExceedsMaxLength()
    {
        string path = @"\\verylongnasstoragehostname\sharename\project\src\app";
        int maxLen = 22;

        string result = TerminalTabViewModel.FormatMiddleEllipsis(path, maxLen);

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
        string result = TerminalTabViewModel.FormatMiddleEllipsis(text, maxLen);

        Assert.Equal(expected, result);
        if (maxLen > 0)
        {
            Assert.True(result.Length <= maxLen);
        }
    }

    [Fact]
    public void CheckForDirectoryChangeCommand_CmdDriveSwitch_UpdatesWorkingDirectory()
    {
        var session = new MockPowerShellSession("CMD", @"C:\Windows") { ShellType = ShellType.CMD };
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        vm.SendInput(Encoding.UTF8.GetBytes("C:\r"));

        Assert.Equal(@"C:\", vm.WorkingDirectory);
    }

    [Fact]
    public void CheckForDirectoryChangeCommand_WslRootPath_UpdatesWorkingDirectory()
    {
        var session = new MockPowerShellSession("WSL", "/home/user") { ShellType = ShellType.WSL };
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        vm.SendInput(Encoding.UTF8.GetBytes("cd /var/log\r"));

        Assert.Equal("/var/log", vm.WorkingDirectory);
    }

    [Fact]
    public void ExecuteHistoryCommand_NonPowerShellShell_TriggersSessionCommandExecuted()
    {
        var session = new MockPowerShellSession("CMD", @"C:\Test") { ShellType = ShellType.CMD };
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        vm.ExecuteHistoryCommand("dir /w");

        Assert.Contains("dir /w", vm.CommandHistory);
    }

    [Fact]
    public void NavigateToHistoryDirectory_NonPowerShellShell_UpdatesWorkingDirectory()
    {
        var session = new MockPowerShellSession("WSL", "/home/user") { ShellType = ShellType.WSL };
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        vm.NavigateToHistoryDirectory("/etc/nginx");

        Assert.Equal("/etc/nginx", vm.WorkingDirectory);
    }
}
