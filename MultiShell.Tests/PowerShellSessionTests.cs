using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

[CollectionDefinition("PowerShellSessionTests", DisableParallelization = true)]
public class PowerShellSessionTestCollection { }

/// <summary>
/// Integration tests for PowerShellSession using ConPTY.
/// </summary>
[Collection("PowerShellSessionTests")]
public class PowerShellSessionTests
{
    [Fact]
    public void Start_InitializesAndSetsIsRunning()
    {
        if (!OperatingSystem.IsWindows()) return;

        using var session = new ShellSession("TestSession");
        Assert.False(session.IsRunning);

        session.Start();
        Assert.True(session.IsRunning);
    }

    [Fact]
    public async Task Send_PipesCommandAndReceivesData()
    {
        if (!OperatingSystem.IsWindows()) return;

        using var session = new ShellSession("SendTestSession");
        var receivedBytes = 0;
        using var signal = new ManualResetEventSlim(false);

        session.DataReceived += data =>
        {
            Interlocked.Add(ref receivedBytes, data.Length);
            signal.Set();
        };

        session.Start();

        // Send a simple echo command
        var cmd = Encoding.UTF8.GetBytes("Write-Output 'PSTestSignal'\r\n");
        session.Send(cmd);

        // Wait up to 5 seconds for response from shell
        var received = await Task.Run(() => signal.Wait(TimeSpan.FromSeconds(5)));

        Assert.True(received, "Expected terminal output from PowerShell within timeout.");
        Assert.True(receivedBytes > 0);
    }

    [Fact]
    public void Resize_DoesNotThrow()
    {
        if (!OperatingSystem.IsWindows()) return;

        using var session = new ShellSession("ResizeSession");
        session.Start();

        var exception = Record.Exception(() =>
        {
            session.Resize(80, 24);
            session.Resize(120, 40);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_TerminatesProcessAndCleansUp()
    {
        if (!OperatingSystem.IsWindows()) return;

        var session = new ShellSession("DisposeSession");
        session.Start();
        Assert.True(session.IsRunning);

        session.Dispose();
        Assert.False(session.IsRunning);
    }

    [Fact]
    public void Constructor_SetsInitialWorkingDirectory()
    {
        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        using var session = new ShellSession("InitDirSession", tempDir);

        Assert.Equal(tempDir, session.WorkingDirectory);
    }

    [Fact]
    public void GenerateShellCommandLine_ConfiguresUtf8ConsoleEncoding()
    {
        using var session = new ShellSession("Utf8TestSession", shellType: ShellType.PowerShell);

        // Access GenerateShellCommandLine via reflection to verify the hookScript contains UTF-8 encoding commands
        var method = typeof(ShellSession).GetMethod("GenerateShellCommandLine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var cmdLine = (string?)method.Invoke(session, new object?[] { null });
        Assert.NotNull(cmdLine);
        Assert.Contains("-EncodedCommand", cmdLine);

        // Extract Base64 encoded payload
        var parts = cmdLine.Split(" -EncodedCommand ");
        Assert.Equal(2, parts.Length);
        var decodedScript = Encoding.Unicode.GetString(Convert.FromBase64String(parts[1]));

        Assert.Contains("chcp 65001 >$null", decodedScript);
        Assert.Contains("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8", decodedScript);
        Assert.Contains("[Console]::InputEncoding = [System.Text.Encoding]::UTF8", decodedScript);
        Assert.Contains("$OutputEncoding = [System.Text.Encoding]::UTF8", decodedScript);
    }

    [Fact]
    public void CheckForOscSequences_FiresCommandExecuted_ForConsecutiveIdenticalCommands()
    {
        using var session = new ShellSession("OscTest");
        var executed = new System.Collections.Generic.List<string>();
        session.CommandExecuted += cmd => executed.Add(cmd);

        var base64Cmd = Convert.ToBase64String(Encoding.UTF8.GetBytes("dotnet test"));
        var packet = Encoding.UTF8.GetBytes($"\x1b]133;E;{base64Cmd}\x07");

        // First execution
        session.CheckForOscSequences(packet);
        // Second identical execution
        session.CheckForOscSequences(packet);

        Assert.Equal(2, executed.Count);
        Assert.Equal("dotnet test", executed[0]);
        Assert.Equal("dotnet test", executed[1]);
    }

    [Theory]
    [InlineData(0, "\x1b]133;D;0\x07")]
    [InlineData(1, "\x1b]133;D;1\x07")]
    [InlineData(127, "\x1b]133;D;127\x1b\\")]
    [InlineData(-1, "\x1b]133;D;-1\x07")]
    public void CheckForOscSequences_FiresCommandCompleted_WithExitCode(int expectedExitCode, string oscPacket)
    {
        using var session = new ShellSession("OscExitTest");
        var exitCodes = new System.Collections.Generic.List<int>();
        session.CommandCompleted += code => exitCodes.Add(code);

        var packet = Encoding.UTF8.GetBytes(oscPacket);
        session.CheckForOscSequences(packet);

        Assert.Single(exitCodes);
        Assert.Equal(expectedExitCode, exitCodes[0]);
    }

    [Fact]
    public void GenerateShellCommandLine_PromptHook_PrioritizesCommandSuccessOverStaleLastExitCode()
    {
        using var session = new ShellSession("HookTestSession", shellType: ShellType.PowerShell);

        var method = typeof(ShellSession).GetMethod("GenerateShellCommandLine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var cmdLine = (string?)method.Invoke(session, new object?[] { null });
        Assert.NotNull(cmdLine);

        var parts = cmdLine.Split(" -EncodedCommand ");
        Assert.Equal(2, parts.Length);
        var decodedScript = Encoding.Unicode.GetString(Convert.FromBase64String(parts[1]));

        // Prompt must capture $? immediately before running any cmdlets
        Assert.Contains("$success = $?", decodedScript);
        Assert.Contains("$code = $LASTEXITCODE", decodedScript);
        Assert.Contains("$lastExit = if ($success) { 0 } else { if ($code -ne $null -and $code -ne 0) { $code } else { 1 } }", decodedScript);
        Assert.Contains("$global:__multishell_last_history_id = $h.Id", decodedScript);
    }

    [Fact]
    public void BuildWslCommandLine_DriveRootWorkingDirectory_DoublesTrailingBackslash()
    {
        // Act
        var cmdLine = ShellSession.BuildWslCommandLine("wsl.exe", @"C:\");

        // Assert
        Assert.Equal("\"wsl.exe\" --cd \"C:\\\\\"", cmdLine);
    }

    [Fact]
    public void BuildWslCommandLine_SubdirectoryWithTrailingSlash_TrimsSeparator()
    {
        // Act
        var cmdLine = ShellSession.BuildWslCommandLine("wsl.exe", @"C:\projekte\multishell\", " -d Ubuntu");

        // Assert
        Assert.Equal("\"wsl.exe\" --cd \"C:\\projekte\\multishell\" -d Ubuntu", cmdLine);
    }

    [Fact]
    public void FormatWin32QuotedArgument_EmbeddedQuotesAndBackslashes_EscapesProperly()
    {
        // Act
        var formatted = ShellSession.FormatWin32QuotedArgument(@"C:\dir\" + "\"name\"\\");

        // Assert
        Assert.Equal("\"C:\\dir\\\\\\\"name\\\"\\\\\"", formatted);
    }

    [Fact]
    public async Task ConcurrentResizeAndDispose_DoesNotThrowOrCorruptHandles()
    {
        if (!OperatingSystem.IsWindows()) return;

        using var session = new ShellSession("ConcurrentResizeDisposeSession");
        session.Start();

        var resizeTask = Task.Run(() =>
        {
            for (int i = 0; i < 40; i++)
            {
                session.Resize(80 + (i % 20), 24 + (i % 10));
            }
        });

        var disposeTask = Task.Run(() =>
        {
            session.Dispose();
            session.Dispose(); // Idempotent double-dispose
        });

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(resizeTask, disposeTask));
        Assert.Null(exception);
        Assert.False(session.IsRunning);
        Assert.Throws<ObjectDisposedException>(() => session.Start());
    }

    [Fact]
    public void CheckForOscSequences_FiresDirectoryChangedWithoutHoldingLock()
    {
        using var session = new ShellSession("OscReentrancyTest");
        string? observedDir = null;
        bool nestedCheckSucceeded = false;

        session.WorkingDirectoryChanged += dir =>
        {
            observedDir = dir;
            // Verify invoking CheckForOscSequences from another thread while inside the handler does not deadlock
            var task = Task.Run(() => session.CheckForOscSequences("\x1b]133;D;0\x07"u8.ToArray()));
            nestedCheckSucceeded = task.Wait(TimeSpan.FromSeconds(2));
        };

        session.CheckForOscSequences("\x1b]9;9;\"C:\\Projects\\MultiShell\"\x07"u8.ToArray());

        Assert.Equal(@"C:\Projects\MultiShell", observedDir);
        Assert.True(nestedCheckSucceeded);
    }

    [Fact]
    public void GenerateShellCommandLine_CustomExecutableWithEmbeddedQuotes_UsesWin32Escaping()
    {
        using var session = new ShellSession(
            "CustomEscaped",
            shellType: ShellType.NuShell,
            customExecutable: @"C:\tools\nu""shell\.exe",
            customArguments: "--login");

        var cmdLine = session.GenerateShellCommandLine(null);
        Assert.Equal("\"C:\\tools\\nu\\\"shell\\.exe\" --login", cmdLine);
    }

    [Fact]
    public void CheckForOscSequences_ParsesOsc7UriUnescaped_AndSuppressesDuplicateUpdates()
    {
        using var session = new ShellSession("Osc7");
        var updates = new System.Collections.Generic.List<string>();
        session.WorkingDirectoryChanged += dir => updates.Add(dir);

        session.CheckForOscSequences("\x1b]7;file://localhost/C:/Projects/My%20App\x1b\\"u8.ToArray());
        session.CheckForOscSequences("\x1b]7;file://localhost/c:/projects/my%20app\x07"u8.ToArray());
        session.CheckForOscSequences("\x1b]9;9;\"C:/PROJECTS/MY APP\"\x07"u8.ToArray());

        Assert.Single(updates);
        Assert.Equal("C:/Projects/My App", updates[0]);
        Assert.Equal("C:/Projects/My App", session.WorkingDirectory);
    }

    [Fact]
    public void CheckForOscSequences_HandlesEscFastPathAndSplitMultiByteUtf8AcrossChunks()
    {
        using var session = new ShellSession("SplitUtf8");
        string? observedDir = null;
        session.WorkingDirectoryChanged += dir => observedDir = dir;

        // ESC-free plain text exercises the fast-path before OSC arrives
        session.CheckForOscSequences("plain terminal output without escape sequences\r\n"u8.ToArray());
        Assert.Null(observedDir);

        // Split a 3-byte UTF-8 sequence ("€" = E2 82 AC) inside an OSC 9;9 path across two reads
        byte[] fullOsc = System.Text.Encoding.UTF8.GetBytes("\x1b]9;9;\"C:\\€uro\\Dir\"\x07");
        int splitIndex = Array.IndexOf(fullOsc, (byte)0x82);
        Assert.True(splitIndex > 0);

        session.CheckForOscSequences(fullOsc[..splitIndex]);
        Assert.Null(observedDir);

        session.CheckForOscSequences(fullOsc[splitIndex..]);
        Assert.Equal(@"C:\€uro\Dir", observedDir);

        // Non-OSC CSI color sequences and trailing text must not leave stale data in _oscBuffer
        // and split ESC + ']' introducer across two chunks must still be reassembled cleanly.
        session.CheckForOscSequences("\x1b[32mGreen output\x1b[0m\r\n"u8.ToArray());
        session.CheckForOscSequences("plain chunk after CSI\r\n\x1b"u8.ToArray());
        session.CheckForOscSequences("]9;9;\"C:\\AfterSplitEsc\"\x07trailing plain text"u8.ToArray());
        Assert.Equal(@"C:\AfterSplitEsc", observedDir);
    }

    [Fact]
    public void GenerateShellCommandLine_BuildsExpectedCommandsForShellTypesAndCustomProfiles()
    {
        using var cmdSession = new ShellSession("Cmd", shellType: ShellType.CMD);
        Assert.Contains("/K \"chcp 65001 >nul\"", cmdSession.GenerateShellCommandLine(null));

        using var nuSession = new ShellSession("Nu", shellType: ShellType.NuShell);
        Assert.Contains("nu.exe", nuSession.GenerateShellCommandLine(null), StringComparison.OrdinalIgnoreCase);

        using var psSession = new ShellSession("PS", shellType: ShellType.PowerShell);
        var psCmd = psSession.GenerateShellCommandLine(null);
        Assert.Contains("-NoLogo -NoExit -EncodedCommand ", psCmd);

        using var wslWithDir = new ShellSession("WslDir", shellType: ShellType.WSL);
        Assert.Contains("--cd \"C:\\Projects\\Repo\"", wslWithDir.GenerateShellCommandLine(@"C:\Projects\Repo\"));

        using var wslNoDir = new ShellSession("WslNoDir", shellType: ShellType.WSL);
        Assert.DoesNotContain("--cd", wslNoDir.GenerateShellCommandLine(null));

        using var customWslAutoCd = new ShellSession("CustomWsl", shellType: ShellType.WSL, customExecutable: "wsl.exe", customArguments: "-d Ubuntu");
        Assert.Equal("\"wsl.exe\" --cd \"C:\\Work\" -d Ubuntu", customWslAutoCd.GenerateShellCommandLine(@"C:\Work"));

        using var customWslExplicitCd = new ShellSession("CustomWslCd", shellType: ShellType.WSL, customExecutable: "wsl.exe", customArguments: "-d Ubuntu --cd ~");
        Assert.Equal("\"wsl.exe\" -d Ubuntu --cd ~", customWslExplicitCd.GenerateShellCommandLine(@"C:\Work"));

        // Profile-launched PowerShell with customExecutable and "-NoLogo" must still inject the OSC prompt hook
        using var profilePs = new ShellSession("ProfilePS", shellType: ShellType.PowerShell, customExecutable: "pwsh.exe", customArguments: "-NoLogo -NoProfile");
        var profilePsCmd = profilePs.GenerateShellCommandLine(null);
        Assert.StartsWith("\"pwsh.exe\" -NoProfile -NoLogo -NoExit -EncodedCommand ", profilePsCmd);

        // Explicit -Command in customArguments must be preserved without appending -EncodedCommand
        using var explicitPs = new ShellSession("ExplicitPS", shellType: ShellType.PowerShell, customExecutable: "pwsh.exe", customArguments: "-NoLogo -Command Get-Date");
        Assert.Equal("\"pwsh.exe\" -NoLogo -Command Get-Date", explicitPs.GenerateShellCommandLine(null));

        // Profile-launched CMD with customExecutable and null arguments must still inject UTF-8 chcp initialization
        using var profileCmd = new ShellSession("ProfileCmd", shellType: ShellType.CMD, customExecutable: "cmd.exe", customArguments: null);
        Assert.Equal("\"cmd.exe\" /K \"chcp 65001 >nul\"", profileCmd.GenerateShellCommandLine(null));
    }
}

