using System;
using System.IO;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class FileBrowserHelperTests
{
    [Fact]
    public void ResolveTargetPath_WhenNullOrEmpty_ReturnsUserProfileOrCurrentDirectory()
    {
        // Act
        var resolved = FileBrowserHelper.ResolveTargetPath(null);

        // Assert
        Assert.NotNull(resolved);
        Assert.True(Directory.Exists(resolved));
    }

    [Fact]
    public void ResolveTargetPath_WhenExistingDirectoryProvided_ReturnsSameDirectory()
    {
        // Arrange
        var tempDir = Path.GetTempPath();

        // Act
        var resolved = FileBrowserHelper.ResolveTargetPath(tempDir);

        // Assert
        Assert.NotNull(resolved);
        Assert.True(Directory.Exists(resolved));
    }

    [Fact]
    public void ResolveTargetPath_WhenNonExistentDirectory_FallsBackToExistingFallback()
    {
        // Arrange
        var nonExistent = Path.Combine(Path.GetTempPath(), $"multishell_test_{Guid.NewGuid():N}");

        // Act
        var resolved = FileBrowserHelper.ResolveTargetPath(nonExistent);

        // Assert
        Assert.NotNull(resolved);
        Assert.True(Directory.Exists(resolved));
    }

    [Fact]
    public void ResolveTargetPath_WhenExistingFileProvided_ReturnsFilePath()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            var resolved = FileBrowserHelper.ResolveTargetPath(tempFile);

            // Assert
            Assert.NotNull(resolved);
            Assert.True(File.Exists(resolved));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
