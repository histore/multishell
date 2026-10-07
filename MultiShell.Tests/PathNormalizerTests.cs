using System;
using System.IO;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class PathNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_WithNullOrWhitespaceAndNoFallback_ReturnsEmpty(string? path)
    {
        var result = PathNormalizer.Normalize(path, fallbackToCurrentDirectory: false);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Normalize_WithNullAndFallback_ReturnsCurrentDirectory()
    {
        var current = Directory.GetCurrentDirectory();
        var currentRoot = Path.GetPathRoot(current);
        var expected = string.Equals(current, currentRoot, StringComparison.OrdinalIgnoreCase)
            ? current
            : current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var result = PathNormalizer.Normalize(null, fallbackToCurrentDirectory: true);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_WithWindowsPathTrailingSlash_StripsTrailingSlash()
    {
        var tempDir = Path.GetTempPath();
        var withTrailing = tempDir.TrimEnd('\\') + "\\";
        var result = PathNormalizer.Normalize(withTrailing);

        Assert.False(result.EndsWith('\\') && result.Length > 3);
        Assert.Equal(Path.GetFullPath(tempDir).TrimEnd('\\'), result);
    }

    [Fact]
    public void Normalize_WithDriveRoot_PreservesDriveRoot()
    {
        var result = PathNormalizer.Normalize(@"C:\");
        Assert.Equal(@"C:\", result);
    }

    [Theory]
    [InlineData("/home/user/", "/home/user")]
    [InlineData("/home/user", "/home/user")]
    [InlineData("/", "/")]
    public void Normalize_WithPosixPaths_NormalizesCorrectly(string input, string expected)
    {
        var result = PathNormalizer.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("/home/developer", true)]
    [InlineData(@"\\wsl$\Ubuntu\home", true)]
    [InlineData(@"\\wsl.localhost\Ubuntu\home", true)]
    public void DirectoryExistsOrNonWindows_HandlesPosixAndWslPaths(string? path, bool expected)
    {
        var result = PathNormalizer.DirectoryExistsOrNonWindows(path);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DirectoryExistsOrNonWindows_WithExistingDirectory_ReturnsTrue()
    {
        var result = PathNormalizer.DirectoryExistsOrNonWindows(Environment.CurrentDirectory);
        Assert.True(result);
    }

    [Fact]
    public void DirectoryExistsOrNonWindows_WithNonExistentLocalDirectory_ReturnsFalse()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), "NonExistent_" + Guid.NewGuid().ToString("N"));
        var result = PathNormalizer.DirectoryExistsOrNonWindows(nonExistent);
        Assert.False(result);
    }
}
