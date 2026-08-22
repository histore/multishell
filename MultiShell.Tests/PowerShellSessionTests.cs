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

        using var session = new PowerShellSession("TestSession");
        Assert.False(session.IsRunning);

        session.Start();
        Assert.True(session.IsRunning);
    }

    [Fact]
    public async Task Send_PipesCommandAndReceivesData()
    {
        if (!OperatingSystem.IsWindows()) return;

        using var session = new PowerShellSession("SendTestSession");
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

        using var session = new PowerShellSession("ResizeSession");
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

        var session = new PowerShellSession("DisposeSession");
        session.Start();
        Assert.True(session.IsRunning);

        session.Dispose();
        Assert.False(session.IsRunning);
    }

    [Fact]
    public void Constructor_SetsInitialWorkingDirectory()
    {
        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        using var session = new PowerShellSession("InitDirSession", tempDir);

        Assert.Equal(tempDir, session.WorkingDirectory);
    }
}
