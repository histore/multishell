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
    /// Checks if a path candidate exists on disk (absolute or relative to working directory).
    /// Strips line-number suffixes such as :42 or :10:5 before checking file existence.
    /// </summary>
    public static bool TryResolveFilePath(string pathCandidate, string? workingDirectory, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(pathCandidate)) return false;

        var cleanPath = StripLineNumberSuffix(pathCandidate);
        if (string.IsNullOrWhiteSpace(cleanPath)) return false;

        try
        {
            // 1. Direct path check (absolute or relative to current process)
            if (File.Exists(cleanPath) || Directory.Exists(cleanPath))
            {
                resolvedPath = Path.GetFullPath(cleanPath);
                return true;
            }

            // 2. Relative to tab working directory
            if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
            {
                var combined = Path.Combine(workingDirectory, cleanPath);
                if (File.Exists(combined) || Directory.Exists(combined))
                {
                    resolvedPath = Path.GetFullPath(combined);
                    return true;
                }
            }
        }
        catch
        {
            // Path contains invalid characters or security restriction
        }

        return false;
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

    /// <summary>
    /// Launches the given URL in the default browser or opens the resolved file path in the system default application.
    /// </summary>
    public static bool OpenTarget(string target, string? workingDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;

        try
        {
            // 1. Web URL
            if (Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFtp))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = true
                });
                return true;
            }

            // 2. Local File / Directory Path
            if (TryResolveFilePath(target, workingDirectory, out var resolvedPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = resolvedPath,
                    UseShellExecute = true
                });
                return true;
            }

            // 3. Fallback for shell-executable URIs (e.g. mailto: or custom protocols)
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
