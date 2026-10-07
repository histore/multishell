using System;
using System.Collections.Generic;
using System.Text;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class AnsiStreamProcessorTests
{
    [Fact]
    public void ProcessChunk_WithEmptyArray_ReturnsNull()
    {
        var processor = new AnsiStreamProcessor();
        var result = processor.ProcessChunk([]);
        Assert.Null(result);
    }

    [Fact]
    public void ProcessChunk_WithSimpleText_ReturnsSameText()
    {
        var processor = new AnsiStreamProcessor();
        var bytes = Encoding.UTF8.GetBytes("Hello, MultiShell!\r\n");
        var result = processor.ProcessChunk(bytes);
        Assert.Equal("Hello, MultiShell!\r\n", result);
    }

    [Fact]
    public void ProcessChunk_WithSplitMultibyteUtf8_AssemblesCorrectlyAcrossChunks()
    {
        var processor = new AnsiStreamProcessor();
        // Euro sign '€' is 3 bytes: 0xE2, 0x82, 0xAC
        byte[] chunk1 = [0xE2, 0x82];
        byte[] chunk2 = [0xAC, (byte)'!'];

        var result1 = processor.ProcessChunk(chunk1);
        var result2 = processor.ProcessChunk(chunk2);

        // First chunk may return null or empty because UTF-8 character is incomplete
        Assert.Null(result1);
        Assert.Equal("€!", result2);
    }

    [Fact]
    public void ProcessChunk_WithIncompleteCsiSequence_BuffersUntilCompleted()
    {
        var processor = new AnsiStreamProcessor();
        // "\x1b[31" is incomplete CSI sequence; 'm' completes it
        var chunk1 = Encoding.UTF8.GetBytes("Text\x1b[31");
        var chunk2 = Encoding.UTF8.GetBytes("mRedText");

        var result1 = processor.ProcessChunk(chunk1);
        var result2 = processor.ProcessChunk(chunk2);

        Assert.Equal("Text", result1);
        Assert.Equal("\x1b[31mRedText", result2);
    }

    [Fact]
    public void ProcessChunk_WithCompleteCsiSequence_PassesThrough()
    {
        var processor = new AnsiStreamProcessor();
        var bytes = Encoding.UTF8.GetBytes("\x1b[32mSuccess\x1b[0m\r\n");
        var result = processor.ProcessChunk(bytes);
        Assert.Equal("\x1b[32mSuccess\x1b[0m\r\n", result);
    }

    [Fact]
    public void ProcessChunk_WithUnsupportedOscSequence_StripsSequence()
    {
        var processor = new AnsiStreamProcessor();
        // OSC 133 shell integration sequence: \x1b]133;A\x07
        var bytes = Encoding.UTF8.GetBytes("Prompt: \x1b]133;A\x07Ready");
        var result = processor.ProcessChunk(bytes);
        Assert.Equal("Prompt: Ready", result);
    }

    [Theory]
    [InlineData("10", true, "\x1b]10;rgb:c0c0/caca/f5f5\x1b\\")]
    [InlineData("10", false, "\x1b]10;rgb:1a1a/1d1d/2b2b\x1b\\")]
    [InlineData("11", true, "\x1b]11;rgb:0e0e/0f0f/1515\x1b\\")]
    [InlineData("11", false, "\x1b]11;rgb:f8f8/f9f9/fcfc\x1b\\")]
    public void DispatchOscColorQueries_DetectsQueriesAndTriggersCallback(string queryType, bool isDarkTheme, string expectedResponse)
    {
        var responses = new List<string>();
        string query = $"\x1b]{queryType};?\x07";

        AnsiStreamProcessor.DispatchOscColorQueries($"Prefix{query}Suffix", isDarkTheme, r => responses.Add(r));

        Assert.Single(responses);
        Assert.Equal(expectedResponse, responses[0]);
    }

    [Fact]
    public void FindIncompleteEscapeSequenceIndex_WithTrailingEsc_ReturnsLastEscIndex()
    {
        string text = "Hello\x1b";
        int index = AnsiStreamProcessor.FindIncompleteEscapeSequenceIndex(text);
        Assert.Equal(5, index);
    }

    [Fact]
    public void FindIncompleteEscapeSequenceIndex_WithIncompleteCsi_ReturnsEscIndex()
    {
        string text = "Hello\x1b[1;3";
        int index = AnsiStreamProcessor.FindIncompleteEscapeSequenceIndex(text);
        Assert.Equal(5, index);
    }

    [Fact]
    public void FindIncompleteEscapeSequenceIndex_WithCompleteCsi_ReturnsNegativeOne()
    {
        string text = "Hello\x1b[1;34m";
        int index = AnsiStreamProcessor.FindIncompleteEscapeSequenceIndex(text);
        Assert.Equal(-1, index);
    }
}
