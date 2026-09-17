using System;
using System.IO;
using System.Linq;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class PathCommandHistoryServiceTests : IDisposable
{
    private readonly string _testTempDir;

    public PathCommandHistoryServiceTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), $"multishell_hist_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testTempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void NormalizePath_StandardizesPathsCorrectly()
    {
        // Trailing slashes
        var p1 = PathCommandHistoryService.NormalizePath(_testTempDir + @"\");
        var p2 = PathCommandHistoryService.NormalizePath(_testTempDir);
        Assert.Equal(p1, p2, ignoreCase: true);

        // Relative path resolution
        var relative = Path.Combine(_testTempDir, "sub", "..");
        var normalizedRelative = PathCommandHistoryService.NormalizePath(relative);
        Assert.Equal(p2, normalizedRelative, ignoreCase: true);

        // Null / whitespace handling
        var expectedCurrent = PathCommandHistoryService.NormalizePath(Directory.GetCurrentDirectory());
        Assert.Equal(expectedCurrent, PathCommandHistoryService.NormalizePath(null));
        Assert.Equal(expectedCurrent, PathCommandHistoryService.NormalizePath("   "));
    }

    [Fact]
    public void RecordCommand_AddsAndRetrievesHistory_CaseAndSlashInsensitive()
    {
        // Arrange
        var service = new PathCommandHistoryService();
        var dirLower = _testTempDir.ToLowerInvariant();
        var dirUpperWithSlash = _testTempDir.ToUpperInvariant() + @"\";

        // Act
        service.RecordCommand(dirLower, "git status");
        service.RecordCommand(dirUpperWithSlash, "dotnet build");

        // Assert
        var history = service.GetHistory(_testTempDir);
        Assert.Equal(2, history.Count);
        Assert.Equal("git status", history[0]);
        Assert.Equal("dotnet build", history[1]);
    }

    [Fact]
    public void RecordCommand_ExistingCommand_MovesToEndWithoutDuplicates()
    {
        // Arrange
        var service = new PathCommandHistoryService();

        // Act - execute A, B, A
        service.RecordCommand(_testTempDir, "git status");
        service.RecordCommand(_testTempDir, "npm test");
        service.RecordCommand(_testTempDir, "git status");

        // Assert
        var history = service.GetHistory(_testTempDir);
        Assert.Equal(2, history.Count);
        Assert.Equal("npm test", history[0]);
        Assert.Equal("git status", history[1]);
    }

    [Fact]
    public void RecordCommand_Exceeding100Entries_PrunesOldestFifo()
    {
        // Arrange
        var service = new PathCommandHistoryService();

        // Act - add 105 distinct commands
        for (int i = 1; i <= 105; i++)
        {
            service.RecordCommand(_testTempDir, $"cmd-{i}");
        }

        // Assert - exactly 100 entries, cmd-1 to cmd-5 dropped
        var history = service.GetHistory(_testTempDir);
        Assert.Equal(100, history.Count);
        Assert.Equal("cmd-6", history[0]);
        Assert.Equal("cmd-105", history[^1]);
        Assert.DoesNotContain("cmd-1", history);
        Assert.DoesNotContain("cmd-5", history);
    }

    [Fact]
    public void RecordCommand_IgnoresInternalAndWhitespaceCommands()
    {
        // Arrange
        var service = new PathCommandHistoryService();

        // Act
        service.RecordCommand(_testTempDir, "");
        service.RecordCommand(_testTempDir, "   ");
        service.RecordCommand(_testTempDir, "chcp 65001");
        service.RecordCommand(_testTempDir, "$OutputEncoding = [System.Text.Encoding]::UTF8");
        service.RecordCommand(_testTempDir, "__multishell_internal");
        service.RecordCommand(_testTempDir, "valid-command");

        // Assert
        var history = service.GetHistory(_testTempDir);
        Assert.Single(history);
        Assert.Equal("valid-command", history[0]);
    }

    [Fact]
    public void RecordCommand_FiresHistoryChangedForPath_WithNormalizedPath()
    {
        // Arrange
        var service = new PathCommandHistoryService();
        string? notifiedPath = null;
        service.HistoryChangedForPath += path => notifiedPath = path;

        // Act
        service.RecordCommand(_testTempDir + @"\", "ls");

        // Assert
        Assert.NotNull(notifiedPath);
        Assert.Equal(PathCommandHistoryService.NormalizePath(_testTempDir), notifiedPath, ignoreCase: true);
    }

    [Fact]
    public void PruneNonExistentPaths_RemovesDeletedDirectoriesOnly()
    {
        // Arrange
        var service = new PathCommandHistoryService();
        var dir1 = Path.Combine(_testTempDir, "dir1");
        var dir2 = Path.Combine(_testTempDir, "dir2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        service.RecordCommand(dir1, "cmd-in-dir1");
        service.RecordCommand(dir2, "cmd-in-dir2");

        // Delete dir1 from filesystem
        Directory.Delete(dir1, recursive: true);

        // Act
        service.PruneNonExistentPaths();

        // Assert
        Assert.Empty(service.GetHistory(dir1));
        var dir2History = service.GetHistory(dir2);
        Assert.Single(dir2History);
        Assert.Equal("cmd-in-dir2", dir2History[0]);
    }

    [Fact]
    public void ExportAll_And_ImportAll_PreservesStateAndLimits()
    {
        // Arrange
        var service1 = new PathCommandHistoryService();
        service1.RecordCommand(_testTempDir, "step 1");
        service1.RecordCommand(_testTempDir, "step 2");

        // Act
        var exported = service1.ExportAll();
        var service2 = new PathCommandHistoryService();
        service2.ImportAll(exported);

        // Assert
        var importedHistory = service2.GetHistory(_testTempDir);
        Assert.Equal(2, importedHistory.Count);
        Assert.Equal("step 1", importedHistory[0]);
        Assert.Equal("step 2", importedHistory[1]);
    }
}
