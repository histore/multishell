using System;
using System.IO;
using System.Threading.Tasks;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class AtomicFileHelperTests
{
    [Fact]
    public async Task WriteAllTextAtomicAsync_WritesAndOverwritesTargetWithoutLeavingTempFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"multishell_atomic_{Guid.NewGuid():N}");
        var targetFile = Path.Combine(tempDir, "nested", "state.json");

        try
        {
            await AtomicFileHelper.WriteAllTextAtomicAsync(targetFile, "{\"v\":1}");
            Assert.True(File.Exists(targetFile));
            Assert.Equal("{\"v\":1}", await File.ReadAllTextAsync(targetFile));

            await AtomicFileHelper.WriteAllTextAtomicAsync(targetFile, "{\"v\":2}");
            Assert.Equal("{\"v\":2}", await File.ReadAllTextAsync(targetFile));

            var leftovers = Directory.GetFiles(Path.GetDirectoryName(targetFile)!, "*.tmp");
            Assert.Empty(leftovers);
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
    public void WindowsPlatformService_OpenUrl_RejectsUnsafeSchemes()
    {
        Assert.False(WindowsPlatformService.Instance.OpenUrl(""));
        Assert.False(WindowsPlatformService.Instance.OpenUrl("file:///C:/Windows/System32/calc.exe"));
        Assert.False(WindowsPlatformService.Instance.OpenUrl("\\\\attacker\\share\\payload.exe"));
    }

    [Fact]
    public void KeyboardStateHelper_QueryCapsLockState_CompletesSafely()
    {
        var state = KeyboardStateHelper.QueryCapsLockState();
        Assert.IsType<bool>(state);
    }

    [Fact]
    public void SanitizeBracketedPastePayload_StripsNestedAndLeadingZeroMarkersAndControlBytes()
    {
        var payload = "hello\x1b[20\x1b[201~1~\x1b[0201~\u009b0200~\x03\x15\x1bworld\n";
        var sanitized = MultiShell.ViewModels.TerminalPaneViewModel.SanitizeBracketedPastePayload(payload);
        Assert.Equal("helloworld\n", sanitized);
    }

    [Fact]
    public void AnsiStreamProcessor_StripsOscWithEmbeddedEscapeAnd8BitC1()
    {
        var text = "ok\x1b]9;9;\"C:\\dir\x1bfoo\"\x07\u009d133;A\u009cdone";
        var sanitized = AnsiStreamProcessor.SanitizeTerminalText(text);
        Assert.Equal("okdone", sanitized);
    }
}
