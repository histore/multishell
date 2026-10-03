using System;
using System.Linq;

namespace MultiShell.Services;

/// <summary>
/// Provides utility methods for formatting tab titles and cleaning text copied from terminal buffers.
/// </summary>
public static class TerminalTextFormatter
{
    /// <summary>
    /// Formats the tab title with a middle-ellipsis (e.g. C:\...\multishell) when space is limited.
    /// </summary>
    public static string FormatMiddleEllipsis(string? text, int maxLength = 22)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        if (maxLength <= 0) return string.Empty;
        if (text.Length <= maxLength) return text;

        if (maxLength <= 3)
        {
            return text[..maxLength];
        }

        char sep = text.Contains('/') && !text.Contains('\\') ? '/' : '\\';
        var parts = text.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length <= 2)
        {
            if (maxLength < 5)
            {
                return text[..maxLength];
            }

            int keep = (maxLength - 3) / 2;
            int suffixKeep = maxLength - 3 - keep;
            return text[..keep] + "..." + text[^suffixKeep..];
        }

        string root = text.StartsWith('/')
            ? ""
            : (text.StartsWith(@"\\") ? $@"\\{parts[0]}" : parts[0]);
        string leaf = parts[^1];

        // Format: C:\...\multishell or \\server\...\multishell or /.../multishell
        string compact = $"{root}{sep}...{sep}{leaf}";
        if (compact.Length <= maxLength)
        {
            return compact;
        }

        // If leaf itself can fit within the budget with a trailing ellipsis
        int rootLength = root.Length + 1; // root + sep
        int availableForLeaf = maxLength - rootLength - 5; // minus ...\ and trailing …
        if (availableForLeaf >= 3 && leaf.Length > availableForLeaf)
        {
            string truncatedLeafCompact = $"{root}{sep}...{sep}{leaf[..availableForLeaf]}…";
            if (truncatedLeafCompact.Length <= maxLength)
            {
                return truncatedLeafCompact;
            }
        }

        // If root itself or compact form still exceeds maxLength, fallback to strict middle ellipsis
        if (maxLength < 5)
        {
            return text[..maxLength];
        }

        int generalKeep = (maxLength - 3) / 2;
        int generalSuffixKeep = maxLength - 3 - generalKeep;
        return text[..generalKeep] + "..." + text[^generalSuffixKeep..];
    }

    /// <summary>
    /// Cleans raw text copied from the terminal buffer by trimming trailing spaces from each line
    /// and removing trailing blank lines caused by fixed rectangular buffer selection.
    /// </summary>
    public static string CleanSelectedTerminalText(string? rawSelectedText)
    {
        if (string.IsNullOrEmpty(rawSelectedText)) return string.Empty;
        var lines = rawSelectedText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var trimmedLines = lines.Select(l => l.TrimEnd()).ToList();
        while (trimmedLines.Count > 0 && string.IsNullOrEmpty(trimmedLines[^1]))
        {
            trimmedLines.RemoveAt(trimmedLines.Count - 1);
        }
        return string.Join(Environment.NewLine, trimmedLines);
    }
}
