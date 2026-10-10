using System;
using System.Collections.Generic;
using MultiShell.Models;

namespace MultiShell.Services;

/// <summary>
/// Parses directory path strings across Windows, UNC, and Unix styles into structured breadcrumb segments (REQ-TERM-013).
/// </summary>
public static class PathBreadcrumbParser
{
    public static IReadOnlyList<PathBreadcrumbSegment> Parse(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Array.Empty<PathBreadcrumbSegment>();
        }

        var normalized = path.Trim();
        var segments = new List<PathBreadcrumbSegment>();

        // 1. Windows Drive Path: e.g. C:\... or C:/...
        if (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':')
        {
            string driveName = normalized[..2]; // e.g. "C:"
            string driveRoot = driveName + "\\"; // "C:\"
            string remainder = normalized.Length > 2 ? normalized[2..].TrimStart('\\', '/') : string.Empty;

            var parts = remainder.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                segments.Add(new PathBreadcrumbSegment(driveName, driveRoot, true));
                return segments;
            }

            segments.Add(new PathBreadcrumbSegment(driveName, driveRoot, false));
            string currentPath = driveRoot.TrimEnd('\\');
            for (int i = 0; i < parts.Length; i++)
            {
                currentPath = currentPath + "\\" + parts[i];
                bool isCurrent = i == parts.Length - 1;
                segments.Add(new PathBreadcrumbSegment(parts[i], currentPath, isCurrent));
            }
            return segments;
        }

        // 2. UNC Path: \\server\share\...
        if (normalized.StartsWith("\\\\") || normalized.StartsWith("//"))
        {
            var rawParts = normalized.TrimStart('\\', '/').Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
            if (rawParts.Length <= 2)
            {
                segments.Add(new PathBreadcrumbSegment(normalized, normalized, true));
                return segments;
            }

            string shareRoot = "\\\\" + rawParts[0] + "\\" + rawParts[1];
            segments.Add(new PathBreadcrumbSegment(shareRoot, shareRoot, false));
            string currentPath = shareRoot;
            for (int i = 2; i < rawParts.Length; i++)
            {
                currentPath = currentPath + "\\" + rawParts[i];
                bool isCurrent = i == rawParts.Length - 1;
                segments.Add(new PathBreadcrumbSegment(rawParts[i], currentPath, isCurrent));
            }
            return segments;
        }

        // 3. Unix Absolute Path: /home/user/...
        if (normalized.StartsWith('/'))
        {
            var parts = normalized.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                segments.Add(new PathBreadcrumbSegment("/", "/", true));
                return segments;
            }

            segments.Add(new PathBreadcrumbSegment("/", "/", false));
            string currentPath = "";
            for (int i = 0; i < parts.Length; i++)
            {
                currentPath = currentPath + "/" + parts[i];
                bool isCurrent = i == parts.Length - 1;
                segments.Add(new PathBreadcrumbSegment(parts[i], currentPath, isCurrent));
            }
            return segments;
        }

        // 4. Other / Relative / Tilde
        char sep = normalized.Contains('/') ? '/' : '\\';
        var fallbackParts = normalized.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (fallbackParts.Length <= 1)
        {
            segments.Add(new PathBreadcrumbSegment(normalized, normalized, true));
            return segments;
        }

        string cumPath = "";
        for (int i = 0; i < fallbackParts.Length; i++)
        {
            cumPath = string.IsNullOrEmpty(cumPath) ? fallbackParts[i] : cumPath + sep + fallbackParts[i];
            bool isCurrent = i == fallbackParts.Length - 1;
            segments.Add(new PathBreadcrumbSegment(fallbackParts[i], cumPath, isCurrent));
        }
        return segments;
    }
}
