using System;
using System.IO;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class ShellDirectoryChangeDetectorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryDetectDirectoryChange_WithNullOrEmpty_ReturnsFalse(string? command)
    {
        var result = ShellDirectoryChangeDetector.TryDetectDirectoryChange(command, @"C:\Test", out var resolved);
        Assert.False(result);
        Assert.Null(resolved);
    }

    [Theory]
    [InlineData("git status")]
    [InlineData("dir")]
    [InlineData("ls -la")]
    [InlineData("echo cd")]
    public void TryDetectDirectoryChange_WithNonDirectoryCommand_ReturnsFalse(string command)
    {
        var result = ShellDirectoryChangeDetector.TryDetectDirectoryChange(command, @"C:\Test", out var resolved);
        Assert.False(result);
        Assert.Null(resolved);
    }

    [Fact]
    public void TryDetectDirectoryChange_WithCdParent_ResolvesParentDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MultiShellDetectorTest_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "Sub");
        Directory.CreateDirectory(subDir);

        try
        {
            var result = ShellDirectoryChangeDetector.TryDetectDirectoryChange("cd..", subDir, out var resolved);
            Assert.True(result);
            Assert.Equal(Path.GetFullPath(tempDir), resolved);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void TryDetectDirectoryChange_WithCdRoot_ResolvesDriveRoot()
    {
        var current = Environment.CurrentDirectory;
        var expectedRoot = Path.GetPathRoot(current);

        var result = ShellDirectoryChangeDetector.TryDetectDirectoryChange("cd\\", current, out var resolved);
        Assert.True(result);
        Assert.Equal(expectedRoot, resolved);
    }

    [Fact]
    public void TryDetectDirectoryChange_WithSetLocation_ResolvesExistingDirectory()
    {
        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = Environment.CurrentDirectory;

        var result = ShellDirectoryChangeDetector.TryDetectDirectoryChange($"Set-Location '{tempDir}'", current, out var resolved);
        Assert.True(result);
        Assert.NotNull(resolved);
        Assert.Equal(Path.GetFullPath(tempDir), resolved);
    }

    [Fact]
    public void TryDetectDirectoryChange_WithPosixPath_ResolvesDirectly()
    {
        var result = ShellDirectoryChangeDetector.TryDetectDirectoryChange("cd /home/user", @"C:\Test", out var resolved);
        Assert.True(result);
        Assert.Equal("/home/user", resolved);
    }

    [Fact]
    public void TryDetectDirectoryChange_WithNonExistentPath_ReturnsFalse()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), "NonExistent_" + Guid.NewGuid().ToString("N"));
        var result = ShellDirectoryChangeDetector.TryDetectDirectoryChange($"cd \"{nonExistent}\"", Environment.CurrentDirectory, out var resolved);
        Assert.False(result);
        Assert.Null(resolved);
    }
}
