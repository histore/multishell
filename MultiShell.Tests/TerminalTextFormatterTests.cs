using System;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class TerminalTextFormatterTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void FormatMiddleEllipsis_WithNullOrWhitespace_ReturnsEmpty(string? input, string expected)
    {
        var result = TerminalTextFormatter.FormatMiddleEllipsis(input, 20);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatMiddleEllipsis_WithShorterThanMaxLength_ReturnsUnchanged()
    {
        string text = @"C:\short\path";
        var result = TerminalTextFormatter.FormatMiddleEllipsis(text, 25);
        Assert.Equal(text, result);
    }

    [Fact]
    public void FormatMiddleEllipsis_WithZeroOrNegativeMaxLength_ReturnsEmpty()
    {
        var result = TerminalTextFormatter.FormatMiddleEllipsis(@"C:\long\path", 0);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FormatMiddleEllipsis_WithDeepDirectory_FormatsWithMiddleEllipsis()
    {
        string text = @"C:\Users\username\Documents\Projects\MyAwesomeApplication";
        var result = TerminalTextFormatter.FormatMiddleEllipsis(text, 25);

        Assert.True(result.Length <= 25);
        Assert.Contains("...", result);
        Assert.StartsWith("C:", result);
    }

    [Fact]
    public void FormatMiddleEllipsis_WithPosixPath_FormatsWithSlashSeparator()
    {
        string text = "/home/developer/workspace/deep/nested/project";
        var result = TerminalTextFormatter.FormatMiddleEllipsis(text, 20);

        Assert.True(result.Length <= 20);
        Assert.Contains("...", result);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void CleanSelectedTerminalText_WithNullOrEmpty_ReturnsEmpty(string? input, string expected)
    {
        var result = TerminalTextFormatter.CleanSelectedTerminalText(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CleanSelectedTerminalText_TrimsTrailingSpacesPerLineAndTrailingBlankLines()
    {
        string raw = "Line 1   \r\nLine 2      \r\nLine 3\r\n   \r\n   ";
        var result = TerminalTextFormatter.CleanSelectedTerminalText(raw);

        var lines = result.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        Assert.Equal(3, lines.Length);
        Assert.Equal("Line 1", lines[0]);
        Assert.Equal("Line 2", lines[1]);
        Assert.Equal("Line 3", lines[2]);
    }
}
