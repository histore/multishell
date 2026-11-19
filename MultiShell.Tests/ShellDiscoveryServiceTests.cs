using System.Linq;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class ShellDiscoveryServiceTests
{
    [Fact]
    public void GetAvailableShells_ReturnsExpectedShellTypes()
    {
        // Arrange
        var service = new ShellDiscoveryService();

        // Act
        var shells = service.GetAvailableShells();

        // Assert
        Assert.NotNull(shells);
        Assert.Equal(4, shells.Count);

        var shellTypes = shells.Select(s => s.ShellType).ToList();
        Assert.Contains(ShellType.PowerShell, shellTypes);
        Assert.Contains(ShellType.NuShell, shellTypes);
        Assert.Contains(ShellType.WSL, shellTypes);
        Assert.Contains(ShellType.CMD, shellTypes);
    }

    [Fact]
    public void GetAvailableShells_AlwaysHasPowerShellAndCmdAvailableOnWindows()
    {
        // Arrange
        var service = new ShellDiscoveryService();

        // Act
        var shells = service.GetAvailableShells();

        // Assert
        var ps = shells.FirstOrDefault(s => s.ShellType == ShellType.PowerShell);
        Assert.NotNull(ps);
        Assert.Equal("PS", ps.IconTag);
        Assert.True(ps.IsAvailable);

        var cmd = shells.FirstOrDefault(s => s.ShellType == ShellType.CMD);
        Assert.NotNull(cmd);
        Assert.Equal("CMD", cmd.IconTag);
        Assert.True(cmd.IsAvailable);
    }

    [Fact]
    public void GetAvailableShells_WithLocalization_UsesLocalizedNames()
    {
        // Arrange
        var locService = new LocalizationService("de", isUserSelection: true);
        var service = new ShellDiscoveryService(locService);

        // Act
        var shells = service.GetAvailableShells();

        // Assert
        var cmd = shells.FirstOrDefault(s => s.ShellType == ShellType.CMD);
        Assert.NotNull(cmd);
        Assert.Equal("Eingabeaufforderung (CMD)", cmd.DisplayName);
    }

    [Fact]
    public void ResolveExecutable_PrefersSystem32OverSpoofedPathEntryForBuiltInShells()
    {
        if (!System.OperatingSystem.IsWindows()) return;

        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"multishell_path_spoof_{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(tempDir);
        var spoofedCmd = System.IO.Path.Combine(tempDir, "cmd.exe");
        System.IO.File.WriteAllText(spoofedCmd, "spoof");

        var originalPath = System.Environment.GetEnvironmentVariable("PATH");
        try
        {
            System.Environment.SetEnvironmentVariable("PATH", $"{tempDir};{originalPath}");

            var resolvedCmd = ShellDiscoveryService.ResolveExecutable("cmd.exe");
            Assert.NotNull(resolvedCmd);
            Assert.NotEqual(spoofedCmd, resolvedCmd, System.StringComparer.OrdinalIgnoreCase);
            Assert.StartsWith(System.Environment.GetFolderPath(System.Environment.SpecialFolder.System), resolvedCmd!, System.StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("PATH", originalPath);
            System.IO.Directory.Delete(tempDir, recursive: true);
        }
    }
}
