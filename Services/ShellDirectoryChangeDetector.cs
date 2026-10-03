using System;
using System.IO;

namespace MultiShell.Services;

/// <summary>
/// Detects directory change commands (cd, Set-Location, drive switches) in shell input streams
/// and resolves the target working directory against the active filesystem.
/// </summary>
public static class ShellDirectoryChangeDetector
{
    /// <summary>
    /// Attempts to parse a command string for directory navigation intent and resolves the resulting target directory.
    /// </summary>
    /// <param name="command">The command string entered by the user.</param>
    /// <param name="currentWorkingDirectory">The active working directory of the shell session.</param>
    /// <param name="resolvedDirectory">The resolved target directory if valid and existing; otherwise null.</param>
    /// <returns>True if a directory change was detected and resolved; otherwise false.</returns>
    public static bool TryDetectDirectoryChange(string? command, string? currentWorkingDirectory, out string? resolvedDirectory)
    {
        resolvedDirectory = null;
        if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(currentWorkingDirectory))
        {
            return false;
        }

        var trimmed = command.Trim();
        string? targetPath = null;

        if (trimmed.StartsWith("cd ", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = trimmed[3..].Trim().Trim('"', '\'');
            if (targetPath.StartsWith("/d ", StringComparison.OrdinalIgnoreCase))
            {
                targetPath = targetPath[3..].Trim().Trim('"', '\'');
            }
        }
        else if (trimmed.Equals("cd..", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = "..";
        }
        else if (trimmed.Equals("cd\\", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("cd/", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = "\\";
        }
        else if (trimmed.StartsWith("Set-Location ", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = trimmed[13..].Trim().Trim('"', '\'');
            if (targetPath.StartsWith("-LiteralPath ", StringComparison.OrdinalIgnoreCase))
            {
                targetPath = targetPath[13..].Trim().Trim('"', '\'');
            }
            else if (targetPath.StartsWith("-Path ", StringComparison.OrdinalIgnoreCase))
            {
                targetPath = targetPath[6..].Trim().Trim('"', '\'');
            }
        }
        else if ((trimmed.Length == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':') ||
                 (trimmed.Length == 3 && char.IsLetter(trimmed[0]) && trimmed[1] == ':' && (trimmed[2] == '\\' || trimmed[2] == '/')))
        {
            targetPath = trimmed[..2] + "\\";
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return false;
        }

        try
        {
            if (targetPath == "..")
            {
                var parent = Directory.GetParent(currentWorkingDirectory)?.FullName;
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                {
                    resolvedDirectory = parent;
                    return true;
                }
            }
            else if (targetPath == "\\" || targetPath == "/")
            {
                var root = Path.GetPathRoot(currentWorkingDirectory);
                if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
                {
                    resolvedDirectory = root;
                    return true;
                }
            }
            else if (targetPath.StartsWith('/'))
            {
                resolvedDirectory = targetPath;
                return true;
            }
            else if (Path.IsPathRooted(targetPath))
            {
                if (Directory.Exists(targetPath))
                {
                    resolvedDirectory = Path.GetFullPath(targetPath);
                    return true;
                }
            }
            else
            {
                var combined = Path.Combine(currentWorkingDirectory, targetPath);
                if (Directory.Exists(combined))
                {
                    resolvedDirectory = Path.GetFullPath(combined);
                    return true;
                }
            }
        }
        catch
        {
            // Ignore path resolution errors
        }

        return false;
    }
}
