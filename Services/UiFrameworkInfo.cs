using System;
using Avalonia;

namespace MultiShell.Services;

/// <summary>
/// Provides UI framework metadata and pure hex color validation helpers
/// so ViewModels do not need to reference Avalonia types.
/// </summary>
public static class UiFrameworkInfo
{
    /// <summary>
    /// Returns a formatted string describing the UI framework and ConPTY backend stack.
    /// </summary>
    public static string GetUiTerminalStack()
    {
        var avaloniaVersion = typeof(Application).Assembly.GetName().Version;
        var versionStr = avaloniaVersion != null ? $"{avaloniaVersion.Major}.{avaloniaVersion.Minor}" : "12.1";
        return $"Avalonia UI {versionStr} / ConPTY";
    }

    /// <summary>
    /// Validates whether a string is a valid CSS/Avalonia hex color (#RGB, #ARGB, #RRGGBB, or #AARRGGBB).
    /// </summary>
    public static bool IsValidHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var span = value.AsSpan().Trim();
        if (span.Length < 4 || span[0] != '#') return false;

        var hexDigits = span[1..];
        if (hexDigits.Length is not (3 or 4 or 6 or 8)) return false;

        foreach (var ch in hexDigits)
        {
            if (!Uri.IsHexDigit(ch))
            {
                return false;
            }
        }

        return true;
    }
}
