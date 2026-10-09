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

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return OpenWindows(targetPath);
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return OpenMac(targetPath);
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return OpenLinux(targetPath);
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
    /// falling back to user profile or current directory if the candidate does not exist.
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
                    return full;
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
            return userProfile;
        }

        return Directory.GetCurrentDirectory();
    }

    private static bool OpenWindows(string path)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{path}\"",
            UseShellExecute = false
        };
        Process.Start(psi);
        return true;
    }

    private static bool OpenMac(string path)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "open",
            Arguments = File.Exists(path) ? $"-R \"{path}\"" : $"\"{path}\"",
            UseShellExecute = false
        };
        Process.Start(psi);
        return true;
    }

    private static bool OpenLinux(string path)
    {
        var targetDir = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            targetDir = path;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "xdg-open",
            Arguments = $"\"{targetDir}\"",
            UseShellExecute = false
        };
        Process.Start(psi);
        return true;
    }
}
