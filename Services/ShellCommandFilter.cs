using System.Text.RegularExpressions;

namespace MultiShell.Services;

/// <summary>
/// Filters internal shell configuration commands, setup commands, and prompt hooks from command history.
/// </summary>
public static class ShellCommandFilter
{
    private static readonly Regex InternalConfigCommandRegex = new(
        @"^(chcp(\s+.*)?|\[Console\]::.*|\$OutputEncoding.*|\$function:prompt.*|Set-PSReadLineOption.*|__multishell_.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Checks if a command is an internal configuration, setup, or prompt-hook command that should be excluded from CommandHistory.
    /// </summary>
    public static bool IsInternalConfigurationCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return true;
        return InternalConfigCommandRegex.IsMatch(command.Trim());
    }
}
