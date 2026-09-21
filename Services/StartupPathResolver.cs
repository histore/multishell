using System;
using System.IO;

namespace MultiShell.Services;

/// <summary>
/// Resolves command-line path arguments (file or directory) into valid absolute working directory paths.
/// </summary>
public class StartupPathResolver : IStartupPathResolver
{
    public string? ResolveWorkingDirectory(string? inputPath, string? baseDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return null;
        }

        var trimmed = inputPath.Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        string fullPath;
        try
        {
            var baseDir = !string.IsNullOrWhiteSpace(baseDirectory) && Directory.Exists(baseDirectory)
                ? baseDirectory
                : Environment.CurrentDirectory;

            fullPath = Path.IsPathRooted(trimmed)
                ? Path.GetFullPath(trimmed)
                : Path.GetFullPath(Path.Combine(baseDir, trimmed));
        }
        catch
        {
            return null;
        }

        if (Directory.Exists(fullPath))
        {
            return fullPath;
        }

        if (File.Exists(fullPath))
        {
            var dirName = Path.GetDirectoryName(fullPath);
            return !string.IsNullOrWhiteSpace(dirName) && Directory.Exists(dirName) ? dirName : null;
        }

        // Check if parent directory exists (e.g. path to a non-existent or newly created file)
        try
        {
            var parentDir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parentDir) && Directory.Exists(parentDir))
            {
                return parentDir;
            }
        }
        catch
        {
            // Ignore invalid path syntax
        }

        return null;
    }

    public string? ResolveFromArgs(string[]? args, string? baseDirectory = null)
    {
        if (args == null || args.Length == 0)
        {
            return null;
        }

        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg)) continue;
            // Ignore potential CLI flags
            if (arg.StartsWith("-") || arg.StartsWith("/")) continue;

            var resolved = ResolveWorkingDirectory(arg, baseDirectory);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return null;
    }
}
