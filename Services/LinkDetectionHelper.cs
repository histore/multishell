using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace MultiShell.Services;

/// <summary>
/// Service for detecting, extracting, and launching Web URLs and local file paths from terminal text.
/// Supports absolute Windows/Linux paths, relative paths against active working directories, and line-number suffixes.
/// </summary>
public static class LinkDetectionHelper
{
    private static readonly Regex UrlRegex = new(
        """(?:https?|ftp|file)://[^\s<>"'\(\)\[\]\{\}]+""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex WindowsAbsolutePathRegex = new(
        """(?:[a-zA-Z]:[\\/]|\\\\)[^\s<>"'|?*]+(?::\d+(?::\d+)?)?""",
        RegexOptions.Compiled);

    private static readonly Regex RelativePathOrFileNameRegex = new(
        """(?:\.{1,2}[\\/]|[a-zA-Z0-9_\-\.]+[\\/])*[a-zA-Z0-9_\-\.]+\.[a-zA-Z0-9]{1,8}(?::\d+(?::\d+)?)?""",
        RegexOptions.Compiled);

    private static readonly char[] TrailingPunctuation = ['.', ',', ';', ':', '!', '?', ')', ']', '>', '"', '\''];

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (bool Exists, string FullPath, long Timestamp)> PathResolutionCache = new(StringComparer.OrdinalIgnoreCase);
    private const long CacheTtlMs = 2000;

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".vbs", ".js", ".msi", ".com", ".scr", ".pif",
        ".ps1", ".psm1", ".psd1", ".hta", ".wsf", ".wsh", ".vbe", ".jse",
        ".reg", ".lnk", ".jar", ".cpl", ".msc", ".inf", ".application", ".appref-ms",
        ".scrobj", ".sct", ".url", ".scf", ".chm", ".py", ".pyw", ".appx", ".msix",
        ".appxbundle", ".msixbundle", ".iso", ".vhd", ".vhdx", ".img",
        ".settingcontent-ms", ".library-ms", ".search-ms", ".jnlp",
        ".msh", ".msh1", ".msh2", ".mshxml", ".msh1xml", ".msh2xml",
        ".docm", ".xlsm", ".pptm"
    };

    internal static Func<ProcessStartInfo, bool>? ProcessLauncher { get; set; }

    /// <summary>
    /// Represents a detected link or file path candidate within a line of text.
    /// </summary>
    public sealed record DetectedLink(string RawTarget, string ResolvedTarget, bool IsUrl, int StartIndex, int Length);

    /// <summary>
    /// Cleans trailing punctuation that might have been captured at the end of a URL or file path.
    /// </summary>
    public static string CleanTrailingPunctuation(string candidate)
    {
        if (string.IsNullOrEmpty(candidate)) return candidate;
        return candidate.TrimEnd(TrailingPunctuation);
    }

    /// <summary>
    /// Attempts to extract a clickable URL or verified local file path at the specified column index within a line of text.
    /// </summary>
    public static DetectedLink? ExtractLinkAtColumn(string lineText, int columnIndex, string? workingDirectory = null)
    {
        if (string.IsNullOrEmpty(lineText) || columnIndex < 0 || columnIndex >= lineText.Length)
        {
            return null;
        }

        var allLinks = ExtractAllLinks(lineText, workingDirectory);
        foreach (var link in allLinks)
        {
            if (columnIndex >= link.StartIndex && columnIndex < link.StartIndex + link.Length)
            {
                return link;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts all valid URLs and resolvable local file paths found in a line of text.
    /// </summary>
    public static IReadOnlyList<DetectedLink> ExtractAllLinks(string lineText, string? workingDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(lineText)) return Array.Empty<DetectedLink>();

        var results = new List<DetectedLink>();

        // 1. Scan for Web URLs
        var urlMatches = UrlRegex.Matches(lineText);
        foreach (Match match in urlMatches)
        {
            var raw = match.Value;
            var clean = CleanTrailingPunctuation(raw);
            if (clean.Length > 0 && Uri.TryCreate(clean, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFtp || uri.Scheme == Uri.UriSchemeFile))
            {
                results.Add(new DetectedLink(clean, clean, IsUrl: true, match.Index, clean.Length));
            }
        }

        // 2. Scan for Absolute Windows Paths
        var absMatches = WindowsAbsolutePathRegex.Matches(lineText);
        foreach (Match match in absMatches)
        {
            var raw = match.Value;
            var clean = CleanTrailingPunctuation(raw);
            if (TryResolveFilePath(clean, workingDirectory, out var resolvedPath))
            {
                // Ensure not overlapping with an existing URL
                if (!IsOverlapping(results, match.Index, clean.Length))
                {
                    results.Add(new DetectedLink(clean, resolvedPath, IsUrl: false, match.Index, clean.Length));
                }
            }
        }

        // 3. Scan for Relative Paths and File Names
        var relMatches = RelativePathOrFileNameRegex.Matches(lineText);
        foreach (Match match in relMatches)
        {
            var raw = match.Value;
            var clean = CleanTrailingPunctuation(raw);
            if (TryResolveFilePath(clean, workingDirectory, out var resolvedPath))
            {
                if (!IsOverlapping(results, match.Index, clean.Length))
                {
                    results.Add(new DetectedLink(clean, resolvedPath, IsUrl: false, match.Index, clean.Length));
                }
            }
        }

        return results;
    }

    private static bool IsOverlapping(List<DetectedLink> list, int start, int length)
    {
        int end = start + length;
        foreach (var item in list)
        {
            int itemEnd = item.StartIndex + item.Length;
            if (start < itemEnd && end > item.StartIndex)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks whether the candidate path is safe to query locally without initiating an outbound remote SMB connection
    /// that could leak NetNTLM credentials or query untrusted remote file shares.
    /// </summary>
    public static bool IsSafeLocalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var trimmed = path.Trim();
        // Normalize forward/mixed UNC prefixes (e.g. //server/share, \/server/share, /\server/share)
        if (trimmed.Length >= 2 &&
            (trimmed[0] == '\\' || trimmed[0] == '/') &&
            (trimmed[1] == '\\' || trimmed[1] == '/'))
        {
            var normalized = trimmed.Replace('/', '\\');

            // Allow only local WSL UNC paths: \\wsl$\ or \\wsl.localhost\
            if (normalized.StartsWith(@"\\wsl$\", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(@"\\wsl.localhost\", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if a path candidate exists on disk (absolute or relative to working directory).
    /// Strips line-number suffixes such as :42 or :10:5 before checking file existence.
    /// </summary>
    public static bool TryResolveFilePath(string pathCandidate, string? workingDirectory, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(pathCandidate)) return false;

        var cleanPath = StripLineNumberSuffix(pathCandidate);
        if (string.IsNullOrWhiteSpace(cleanPath) || !IsSafeLocalPath(cleanPath)) return false;

        var cacheKey = $"{workingDirectory}|{cleanPath}";
        var now = Environment.TickCount64;
        if (PathResolutionCache.TryGetValue(cacheKey, out var cached) && now - cached.Timestamp < CacheTtlMs)
        {
            resolvedPath = cached.FullPath;
            return cached.Exists;
        }

        try
        {
            bool isRooted = Path.IsPathRooted(cleanPath);

            // 1. For relative paths, prefer resolving against the active tab's working directory first
            if (!isRooted && !string.IsNullOrWhiteSpace(workingDirectory) && IsSafeLocalPath(workingDirectory) && Directory.Exists(workingDirectory))
            {
                var combined = Path.Combine(workingDirectory, cleanPath);
                if (IsSafeLocalPath(combined) && (File.Exists(combined) || Directory.Exists(combined)))
                {
                    resolvedPath = Path.GetFullPath(combined);
                    CacheResolution(cacheKey, true, resolvedPath, now);
                    return true;
                }
            }

            // 2. Direct path check (rooted path, or fallback relative to current process when no tab CWD matched)
            if (File.Exists(cleanPath) || Directory.Exists(cleanPath))
            {
                resolvedPath = Path.GetFullPath(cleanPath);
                CacheResolution(cacheKey, true, resolvedPath, now);
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            System.Diagnostics.Debug.WriteLine($"[LinkDetectionHelper.TryResolveFilePath] Resolution failed for '{cleanPath}': {ex.Message}");
        }

        CacheResolution(cacheKey, false, string.Empty, now);
        return false;
    }

    private static void CacheResolution(string key, bool exists, string fullPath, long timestamp)
    {
        if (PathResolutionCache.Count > 1000)
        {
            PathResolutionCache.Clear();
        }
        PathResolutionCache[key] = (exists, fullPath, timestamp);
    }

    /// <summary>
    /// Strips trailing line/column suffixes like ':123' or ':12:5' from a file path.
    /// </summary>
    public static string StripLineNumberSuffix(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        var match = Regex.Match(path, @"^(.*?)(?::\d+)+$");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return path;
    }

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
    /// Launches the given URL in the default browser or opens the resolved file path in the system default application.
    /// </summary>
    public static bool OpenTarget(string target, string? workingDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;

        try
        {
            // 1. Web URL or safe local file:// URI
            if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFtp)
                {
                    return StartProcess(new ProcessStartInfo
                    {
                        FileName = target,
                        UseShellExecute = true
                    });
                }

                if (uri.Scheme == Uri.UriSchemeFile && uri.IsFile)
                {
                    var localPath = uri.LocalPath;
                    if (IsSafeLocalPath(localPath) && TryResolveFilePath(localPath, workingDirectory, out var resolvedFileUriPath))
                    {
                        return OpenResolvedLocalPath(resolvedFileUriPath);
                    }
                    return false;
                }
            }

            // 2. Local File / Directory Path
            if (TryResolveFilePath(target, workingDirectory, out var resolvedPath))
            {
                return OpenResolvedLocalPath(resolvedPath);
            }

            // 3. Fallback for safe known schemes (e.g. mailto: or tel:)
            if (Uri.TryCreate(target, UriKind.Absolute, out var customUri) &&
                (customUri.Scheme == "mailto" || customUri.Scheme == "tel"))
            {
                return StartProcess(new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = true
                });
            }

            return false;
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException or UriFormatException or ArgumentException)
        {
            System.Diagnostics.Debug.WriteLine($"[LinkDetectionHelper.OpenTarget] Failed to open '{target}': {ex.Message}");
            return false;
        }
    }

    private static bool OpenResolvedLocalPath(string resolvedPath)
    {
        if (File.Exists(resolvedPath) && ExecutableExtensions.Contains(Path.GetExtension(resolvedPath)))
        {
            var sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var notepadPath = Path.Combine(sysDir, "notepad.exe");
            if (!File.Exists(notepadPath)) notepadPath = "notepad.exe";

            var psi = new ProcessStartInfo
            {
                FileName = notepadPath,
                UseShellExecute = false
            };
            psi.ArgumentList.Add(resolvedPath);
            return StartProcess(psi);
        }

        return StartProcess(new ProcessStartInfo
        {
            FileName = resolvedPath,
            UseShellExecute = true
        });
    }
}
