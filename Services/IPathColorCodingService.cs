using System.Collections.Generic;
using Avalonia.Media;
using MultiShell.Models;

namespace MultiShell.Services;

/// <summary>
/// Service responsible for computing deterministic folder color stripes for terminal tabs,
/// dynamically omitting the common root folders shared across all open tabs (REQ-TAB-025).
/// </summary>
public interface IPathColorCodingService
{
    /// <summary>
    /// Computes the path color stripes for a given directory path.
    /// Displays all folder levels deterministically without dynamic cross-tab omission (REQ-TAB-025).
    /// </summary>
    /// <param name="path">The target directory path for the current tab.</param>
    /// <param name="allPaths">Optional active directory paths (retained for backward compatibility, not used for dynamic trimming).</param>
    /// <returns>A list of color stripes representing each directory level from left to right.</returns>
    IReadOnlyList<PathColorStripe> GetStripesForPath(string? path, IEnumerable<string?>? allPaths = null);

    /// <summary>
    /// Splits a directory path into normalized directory segments (e.g. ["C:", "dt", "multishell", "Services"]).
    /// </summary>
    /// <param name="path">The raw directory path.</param>
    /// <returns>A list of directory segment names.</returns>
    IReadOnlyList<string> GetPathSegments(string? path);

    /// <summary>
    /// Computes the length of the common prefix across all provided segment lists.
    /// </summary>
    /// <param name="allSegments">List of directory segment lists.</param>
    /// <returns>The number of initial segments shared by all paths.</returns>
    int GetCommonPrefixLength(IReadOnlyList<IReadOnlyList<string>> allSegments);

    /// <summary>
    /// Generates a deterministic, aesthetically pleasing color for a given folder name.
    /// </summary>
    /// <param name="folderName">The folder name.</param>
    /// <returns>The calculated Avalonia Color.</returns>
    Color GetColorForFolderName(string folderName);
}
