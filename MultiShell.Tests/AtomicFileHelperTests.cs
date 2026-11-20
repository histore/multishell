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
}
