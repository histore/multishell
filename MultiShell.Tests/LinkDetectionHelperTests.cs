using System;
using System.IO;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class LinkDetectionHelperTests
{
    [Theory]
    [InlineData("https://github.com/histore/multishell", "https://github.com/histore/multishell")]
    [InlineData("http://localhost:8080/api/v1?query=test#hash", "http://localhost:8080/api/v1?query=test#hash")]
    [InlineData("See https://dot.net/ for info.", "https://dot.net/")]
    [InlineData("Check (https://example.com/test), please", "https://example.com/test")]
    [InlineData("Visit [https://example.com/readme] now", "https://example.com/readme")]
    public void ExtractAllLinks_ExtractsAndCleansWebUrls(string input, string expectedUrl)
    {
        // Act
        var links = LinkDetectionHelper.ExtractAllLinks(input);

        // Assert
        Assert.Contains(links, l => l.IsUrl && l.ResolvedTarget == expectedUrl);
    }

    [Fact]
    public void ExtractLinkAtColumn_HitTestsCorrectLink()
    {
        // Arrange
        var line = "Prefix https://github.com/histore/multishell suffix https://google.com end";

        // Act 1: Click inside first link (index 10)
        var hit1 = LinkDetectionHelper.ExtractLinkAtColumn(line, 10);
        Assert.NotNull(hit1);
        Assert.Equal("https://github.com/histore/multishell", hit1!.ResolvedTarget);

        // Act 2: Click on prefix (index 2) -> null
        var hit2 = LinkDetectionHelper.ExtractLinkAtColumn(line, 2);
        Assert.Null(hit2);

        // Act 3: Click inside second link (index 56)
        var hit3 = LinkDetectionHelper.ExtractLinkAtColumn(line, 56);
        Assert.NotNull(hit3);
        Assert.Equal("https://google.com", hit3!.ResolvedTarget);
    }

    [Fact]
    public void TryResolveFilePath_ResolvesExistingFile_RelativeToWorkingDirectory()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var testFile = Path.Combine(tempDir, "config.json");
        File.WriteAllText(testFile, "{}");

        try
        {
            // Act 1: Direct relative path
            var success1 = LinkDetectionHelper.TryResolveFilePath("config.json", tempDir, out var resolvedPath1);
            Assert.True(success1);
            Assert.Equal(Path.GetFullPath(testFile), resolvedPath1);

            // Act 2: With line number suffix :42
            var success2 = LinkDetectionHelper.TryResolveFilePath("config.json:42", tempDir, out var resolvedPath2);
            Assert.True(success2);
            Assert.Equal(Path.GetFullPath(testFile), resolvedPath2);

            // Act 3: Non-existent file
            var success3 = LinkDetectionHelper.TryResolveFilePath("non_existent_file.xyz", tempDir, out _);
            Assert.False(success3);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("Program.cs:42", "Program.cs")]
    [InlineData("Services/Test.cs:100:5", "Services/Test.cs")]
    [InlineData(@"C:\Repo\File.txt", @"C:\Repo\File.txt")]
    public void StripLineNumberSuffix_StripsOffsetsCorrectly(string input, string expected)
    {
        var actual = LinkDetectionHelper.StripLineNumberSuffix(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ExtractAllLinks_DetectsExistingRelativeFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var testFile = Path.Combine(tempDir, "build.ps1");
        File.WriteAllText(testFile, "# test");

        try
        {
            var line = "Execute ./build.ps1 or build.ps1:10 to start";

            // Act
            var links = LinkDetectionHelper.ExtractAllLinks(line, tempDir);

            // Assert
            Assert.NotEmpty(links);
            Assert.Contains(links, l => !l.IsUrl && l.ResolvedTarget.EndsWith("build.ps1"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void OpenTarget_UnverifiedNonExistentFileOrBinary_ReturnsFalse()
    {
        // Act
        var result = LinkDetectionHelper.OpenTarget("malicious_command.exe");

        // Assert - Disallows launching unverified arbitrary binaries
        Assert.False(result);
    }

    [Fact]
    public void OpenTarget_EmptyOrWhitespace_ReturnsFalse()
    {
        Assert.False(LinkDetectionHelper.OpenTarget(""));
        Assert.False(LinkDetectionHelper.OpenTarget("   "));
    }
}
