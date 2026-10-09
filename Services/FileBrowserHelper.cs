using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MultiShell.Services;

/// <summary>
/// Helper for launching the platform-specific native file browser (Windows Explorer, macOS Finder, Linux file manager)
/// for a specified directory or file path, completely decoupled from the active terminal session.
/// </summary>
public static class FileBrowserHelper
{
    /// <summary>
    /// Opens the native file browser with the specified directory, or falls back to the user's home directory.
    /// If an existing file path is passed, the containing directory is opened and the file highlighted where supported.
    /// </summary>
    /// <param name="path">The directory or file path to open.</param>
    /// <returns>True if the process was successfully launched; otherwise, false.</returns>
    public static bool OpenInFileBrowser(string? path)
    {
        try
        {
            var targetPath = ResolveTargetPath(path);
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return false;
            }

            // 1. Direct directory opening via platform-native shell execution
            if (Directory.Exists(targetPath))
            {
                return OpenDirectory(targetPath);
            }

            // 2. File reveal inside its parent folder
            if (File.Exists(targetPath))
            {
                return RevealFile(targetPath);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves the target path candidate to a verified absolute directory or file path,
    /// normalizing trailing separators and falling back to user profile or current directory.
    /// </summary>
    public static string? ResolveTargetPath(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                var full = Path.GetFullPath(path);
                if (Directory.Exists(full) || File.Exists(full))
                {
                    // Platform-independently trims redundant trailing separators while preserving root drives (e.g. C:\ or /)
                    return Path.TrimEndingDirectorySeparator(full);
                }
            }
            catch
            {
                // Fall back to user profile on path format errors
            }
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile) && Directory.Exists(userProfile))
        {
            return Path.TrimEndingDirectorySeparator(userProfile);
        }

        return Path.TrimEndingDirectorySeparator(Directory.GetCurrentDirectory());
    }

    private static bool OpenDirectory(string directoryPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows ShellExecuteEx natively handles directories without CLI quote-escaping pitfalls
            Process.Start(new ProcessStartInfo
            {
                FileName = directoryPath,
                UseShellExecute = true
            });
            return true;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var psi = new ProcessStartInfo
            {
                FileName = "open",
                UseShellExecute = false
            };
            psi.ArgumentList.Add(directoryPath);
            Process.Start(psi);
            return true;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var psi = new ProcessStartInfo
            {
                FileName = "xdg-open",
                UseShellExecute = false
            };
            psi.ArgumentList.Add(directoryPath);
            Process.Start(psi);
            return true;
        }

        return false;
    }

    private static bool RevealFile(string filePath)
    {
        var psi = new ProcessStartInfo { UseShellExecute = false };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi.FileName = "explorer.exe";
            // ArgumentList automatically applies Win32 escaping rules
            psi.ArgumentList.Add($"/select,{filePath}");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            psi.FileName = "open";
            psi.ArgumentList.Add("-R");
            psi.ArgumentList.Add(filePath);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            psi.FileName = "xdg-open";
            var parentDir = Path.GetDirectoryName(filePath) ?? filePath;
            psi.ArgumentList.Add(parentDir);
        }
        else
        {
            return false;
        }

        Process.Start(psi);
        return true;
    }
}
