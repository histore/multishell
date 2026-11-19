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
    internal static Func<ProcessStartInfo, bool>? ProcessLauncher { get; set; }

    private static bool StartProcess(ProcessStartInfo psi)
    {
        if (ProcessLauncher != null)
        {
            return ProcessLauncher(psi);
        }

        using var proc = Process.Start(psi);
        return proc != null;
    }
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
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        {
            System.Diagnostics.Debug.WriteLine($"[FileBrowserHelper.OpenInFileBrowser] Failed to open '{path}': {ex.Message}");
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
            catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException or UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                // Fall back to user profile on path format errors
                System.Diagnostics.Debug.WriteLine($"[FileBrowserHelper.ResolveTargetPath] Invalid path '{path}': {ex.Message}");
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
            return StartProcess(new ProcessStartInfo
            {
                FileName = directoryPath,
                UseShellExecute = true
            });
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var psi = new ProcessStartInfo
            {
                FileName = "open",
                UseShellExecute = false
            };
            psi.ArgumentList.Add(directoryPath);
            return StartProcess(psi);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var psi = new ProcessStartInfo
            {
                FileName = "xdg-open",
                UseShellExecute = false
            };
            psi.ArgumentList.Add(directoryPath);
            return StartProcess(psi);
        }

        return false;
    }

    private static bool RevealFile(string filePath)
    {
        var psi = new ProcessStartInfo { UseShellExecute = false };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var explorerPath = !string.IsNullOrWhiteSpace(winDir) ? Path.Combine(winDir, "explorer.exe") : "explorer.exe";
            psi.FileName = File.Exists(explorerPath) ? explorerPath : "explorer.exe";
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

        return StartProcess(psi);
    }
}
