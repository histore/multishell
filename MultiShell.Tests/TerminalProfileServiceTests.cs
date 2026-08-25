using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MultiShell.Models;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class TerminalProfileServiceTests : IDisposable
{
    private readonly string _tempFile;

    public TerminalProfileServiceTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"multishell_profiles_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    [Fact]
    public async Task LoadProfilesAsync_WhenFileDoesNotExist_InitializesDefaults()
    {
        // Arrange
        var service = new TerminalProfileService(_tempFile);

        // Act
        await service.LoadProfilesAsync();
        var profiles = service.GetProfiles();

        // Assert
        Assert.NotEmpty(profiles);
        Assert.Contains(profiles, p => p.ShellType == ShellType.PowerShell);
    }

    [Fact]
    public async Task AddProfileAsync_AddsProfileAndPersists()
    {
        // Arrange
        var service = new TerminalProfileService(_tempFile);
        await service.LoadProfilesAsync();
        var initialCount = service.GetProfiles().Count;

        var customProfile = new TerminalProfile(
            Guid.NewGuid(),
            "Git Bash",
            @"C:\Program Files\Git\bin\bash.exe",
            Arguments: "--login -i",
            IconTag: "GIT",
            ShellType: ShellType.PowerShell,
            IsBuiltIn: false);

        // Act
        await service.AddProfileAsync(customProfile);

        // Assert
        var updated = service.GetProfiles();
        Assert.Equal(initialCount + 1, updated.Count);
        var added = service.GetProfile(customProfile.Id);
        Assert.NotNull(added);
        Assert.Equal("Git Bash", added.Name);
        Assert.Equal(@"C:\Program Files\Git\bin\bash.exe", added.ExecutablePath);
        Assert.Equal("--login -i", added.Arguments);
        Assert.Equal("GIT", added.IconTag);

        // Reload fresh from file
        var reloadedService = new TerminalProfileService(_tempFile);
        await reloadedService.LoadProfilesAsync();
        var reloadedProfile = reloadedService.GetProfile(customProfile.Id);
        Assert.NotNull(reloadedProfile);
        Assert.Equal("Git Bash", reloadedProfile.Name);
    }

    [Fact]
    public async Task UpdateProfileAsync_ModifiesExistingProfileAndPersists()
    {
        // Arrange
        var service = new TerminalProfileService(_tempFile);
        await service.LoadProfilesAsync();
        var profile = service.GetProfiles().First();

        var updated = profile with { Name = "Renamed Shell", ExecutablePath = @"C:\custom\shell.exe" };

        // Act
        await service.UpdateProfileAsync(updated);

        // Assert
        var fetched = service.GetProfile(profile.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Renamed Shell", fetched.Name);
        Assert.Equal(@"C:\custom\shell.exe", fetched.ExecutablePath);
    }

    [Fact]
    public async Task DeleteProfileAsync_RemovesProfileAndPersists()
    {
        // Arrange
        var service = new TerminalProfileService(_tempFile);
        await service.LoadProfilesAsync();

        var customProfile = new TerminalProfile(
            Guid.NewGuid(),
            "Temp Shell",
            "cmd.exe",
            IconTag: "TMP",
            ShellType: ShellType.CMD);
        await service.AddProfileAsync(customProfile);

        // Act
        var deleted = await service.DeleteProfileAsync(customProfile.Id);

        // Assert
        Assert.True(deleted);
        Assert.Null(service.GetProfile(customProfile.Id));
    }

    [Fact]
    public async Task ResetToDefaultsAsync_RestoresDefaultProfiles()
    {
        // Arrange
        var service = new TerminalProfileService(_tempFile);
        await service.LoadProfilesAsync();

        var customProfile = new TerminalProfile(Guid.NewGuid(), "Custom", "pwsh.exe");
        await service.AddProfileAsync(customProfile);

        // Act
        await service.ResetToDefaultsAsync();

        // Assert
        Assert.Null(service.GetProfile(customProfile.Id));
        var defaults = service.GetProfiles();
        Assert.NotEmpty(defaults);
        Assert.All(defaults, p => Assert.True(p.IsBuiltIn));
    }
}
