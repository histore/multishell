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
}
