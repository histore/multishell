using System;
using System.IO;

namespace MultiShell.Services;

/// <summary>
/// Provides unified path normalization and validation logic across all services and platforms.
/// </summary>
public static class PathNormalizer
{
    /// <summary>
    /// Normalizes a directory path for consistent comparison and display.
    /// Resolves full paths where possible, trims whitespace, and strips non-root trailing directory separators.
    /// If <paramref name="fallbackToCurrentDirectory"/> is true and path is null/whitespace, returns the normalized current working directory.
    /// Otherwise returns string.Empty.
    /// </summary>
    public static string Normalize(string? path, bool fallbackToCurrentDirectory = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (!fallbackToCurrentDirectory)
            {
                return string.Empty;
            }

            var current = Directory.GetCurrentDirectory();
            var currentRoot = Path.GetPathRoot(current);
            if (!string.Equals(current, currentRoot, StringComparison.OrdinalIgnoreCase))
            {
                current = current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return current;
        }

        var trimmed = path.Trim();
        if (trimmed.StartsWith('/'))
        {
            var normalizedPosix = trimmed.TrimEnd('/');
            return string.IsNullOrEmpty(normalizedPosix) ? "/" : normalizedPosix;
        }

        try
        {
            var fullPath = Path.GetFullPath(trimmed);
            var root = Path.GetPathRoot(fullPath);
            if (!string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            {
                fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return fullPath;
        }
        catch
        {
            return trimmed.TrimEnd('\\', '/');
        }
    }

    /// <summary>
    /// Checks if a directory path exists on disk, or if it represents a POSIX or WSL path that cannot be resolved via standard Windows filesystem APIs.
    /// </summary>
    public static bool DirectoryExistsOrNonWindows(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        // Do not prune WSL / POSIX style paths on Windows
        if (path.StartsWith('/') || path.StartsWith(@"\\wsl", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return Directory.Exists(path);
    }
}
