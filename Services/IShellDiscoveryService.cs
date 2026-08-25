using System.Collections.Generic;

namespace MultiShell.Services;

/// <summary>
/// Information about a detected shell available on the host machine.
/// </summary>
public record ShellOptionInfo(
    ShellType ShellType,
    string DisplayName,
    string IconTag,
    string? ExecutablePath,
    bool IsAvailable);

/// <summary>
/// Service that discovers and queries shells installed on the system.
/// </summary>
public interface IShellDiscoveryService
{
    /// <summary>
    /// Gets all detected shells and their availability on the current system.
    /// </summary>
    IReadOnlyList<ShellOptionInfo> GetAvailableShells();
}
