using System;
using System.IO;
using System.Threading.Tasks;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class SingleInstanceServiceTests
{
    [Fact]
    public void IsFirstInstance_ReturnsTrueForFirstInstance_AndFalseForSecondInstance()
    {
        // Arrange
        var testGuid = Guid.NewGuid().ToString("N");
        var mutexName = $@"Local\MultiShell_Test_Mutex_{testGuid}";
        var pipeName = $"MultiShell_Test_Pipe_{testGuid}";

        using var first = new SingleInstanceService(mutexName, pipeName);
        using var second = new SingleInstanceService(mutexName, pipeName);

        // Assert
        Assert.True(first.IsFirstInstance);
        Assert.False(second.IsFirstInstance);
    }

    [Fact]
    public async Task SendArgsToFirstInstanceAsync_DeliversResolvedDirectoryToServer()
    {
        // Arrange
        var testGuid = Guid.NewGuid().ToString("N");
        var mutexName = $@"Local\MultiShell_Test_Mutex_{testGuid}";
        var pipeName = $"MultiShell_Test_Pipe_{testGuid}";

        var tempDir = Path.Combine(Path.GetTempPath(), $"multishell_ipc_test_{testGuid}");
        Directory.CreateDirectory(tempDir);

        using var server = new SingleInstanceService(mutexName, pipeName);
        using var client = new SingleInstanceService(mutexName, pipeName);

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.DirectoryOpenRequested += path => tcs.TrySetResult(path);
        server.StartServer();

        try
        {
            // Act
            var success = await client.SendArgsToFirstInstanceAsync(new[] { tempDir }, Environment.CurrentDirectory);

            // Assert
            Assert.True(success);

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(3000));
            Assert.Equal(tcs.Task, completedTask);
            Assert.Equal(Path.GetFullPath(tempDir), await tcs.Task);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SendArgsToFirstInstanceAsync_DeliversNull_WhenNoValidPathPassed()
    {
        // Arrange
        var testGuid = Guid.NewGuid().ToString("N");
        var mutexName = $@"Local\MultiShell_Test_Mutex_{testGuid}";
        var pipeName = $"MultiShell_Test_Pipe_{testGuid}";

        using var server = new SingleInstanceService(mutexName, pipeName);
        using var client = new SingleInstanceService(mutexName, pipeName);

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.DirectoryOpenRequested += path => tcs.TrySetResult(path);
        server.StartServer();

        // Act - no arguments passed
        var success = await client.SendArgsToFirstInstanceAsync(Array.Empty<string>(), Environment.CurrentDirectory);

        // Assert
        Assert.True(success);

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.Equal(tcs.Task, completedTask);
        Assert.Null(await tcs.Task);
    }
}
