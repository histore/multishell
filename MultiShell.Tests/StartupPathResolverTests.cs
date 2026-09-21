using System;
using System.IO;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class StartupPathResolverTests
{
    private readonly StartupPathResolver _resolver = new();

    [Fact]
    public void ResolveWorkingDirectory_ReturnsDirectoryPath_WhenInputIsExistingDirectory()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"multishell_test_dir_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var resolved = _resolver.ResolveWorkingDirectory(tempDir);

            // Assert
            Assert.NotNull(resolved);
            Assert.Equal(Path.GetFullPath(tempDir), resolved);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveWorkingDirectory_ReturnsParentDirectory_WhenInputIsExistingFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"multishell_test_file_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var testFile = Path.Combine(tempDir, "sample.txt");
        File.WriteAllText(testFile, "test content");

        try
        {
            // Act
            var resolved = _resolver.ResolveWorkingDirectory(testFile);

            // Assert
            Assert.NotNull(resolved);
            Assert.Equal(Path.GetFullPath(tempDir), resolved);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveWorkingDirectory_HandlesRelativePathWithBaseDirectory()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), $"multishell_test_base_{Guid.NewGuid():N}");
        var subDir = Path.Combine(baseDir, "nested");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "document.docx");
        File.WriteAllText(testFile, "hello");

        try
        {
            // Act - relative path "nested/document.docx"
            var resolved = _resolver.ResolveWorkingDirectory("nested" + Path.DirectorySeparatorChar + "document.docx", baseDir);

            // Assert
            Assert.NotNull(resolved);
            Assert.Equal(Path.GetFullPath(subDir), resolved);
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void ResolveWorkingDirectory_HandlesDotAndDoubleDot()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), $"multishell_test_dots_{Guid.NewGuid():N}");
        var childDir = Path.Combine(baseDir, "child");
        Directory.CreateDirectory(childDir);

        try
        {
            // Act & Assert .
            var resolvedDot = _resolver.ResolveWorkingDirectory(".", childDir);
            Assert.Equal(Path.GetFullPath(childDir), resolvedDot);

            // Act & Assert ..
            var resolvedDoubleDot = _resolver.ResolveWorkingDirectory("..", childDir);
            Assert.Equal(Path.GetFullPath(baseDir), resolvedDoubleDot);
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void ResolveWorkingDirectory_TrimsQuotes()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"multishell_test_quotes_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var resolved = _resolver.ResolveWorkingDirectory($"\"{tempDir}\"");

            // Assert
            Assert.Equal(Path.GetFullPath(tempDir), resolved);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    public void ResolveWorkingDirectory_ReturnsNull_WhenInputIsNullOrWhitespace(string? input)
    {
        var resolved = _resolver.ResolveWorkingDirectory(input);
        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveFromArgs_ReturnsFirstValidDirectory_IgnoringFlags()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"multishell_test_args_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var args = new[] { "--debug", "-v", tempDir, "extra_arg" };

            // Act
            var resolved = _resolver.ResolveFromArgs(args);

            // Assert
            Assert.Equal(Path.GetFullPath(tempDir), resolved);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveFromArgs_ReturnsNull_WhenNoArgsPassedOrAllInvalid()
    {
        Assert.Null(_resolver.ResolveFromArgs(null));
        Assert.Null(_resolver.ResolveFromArgs(Array.Empty<string>()));
        Assert.Null(_resolver.ResolveFromArgs(new[] { "--flag", "-x" }));
    }
}
