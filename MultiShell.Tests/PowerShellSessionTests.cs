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
}

