using System.Text.RegularExpressions;

namespace MultiShell.Services;

/// <summary>
/// Filters internal shell configuration commands, setup commands, and prompt hooks from command history,
/// and provides masking for sensitive credentials (passwords, tokens, bearer headers) before storage.
/// </summary>
public static class ShellCommandFilter
{
    private static readonly Regex InternalConfigCommandRegex = new(
        @"^(chcp(\s+.*)?|\[Console\]::.*|\$OutputEncoding.*|\$function:prompt.*|Set-PSReadLineOption.*|__multishell_.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GitUrlCredentialsRegex = new(
        @"(https?://[^:\s]+:)[^@\s]+(@)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SensitiveFlagRegex = new(
        @"(?i)(--(?:password|token|secret|api[_-]?key)(?:=|\s+))([^\s""']+|""[^""]*""|'[^']*)",
        RegexOptions.Compiled);

    private static readonly Regex SensitiveHeaderRegex = new(
        @"(?i)(authorization:\s*bearer\s+|bearer\s+)([^\s""']+|""[^""]*""|'[^']*)",
        RegexOptions.Compiled);

    /// <summary>
    /// Checks if a command is an internal configuration, setup, or prompt-hook command that should be excluded from CommandHistory.
    /// </summary>
    public static bool IsInternalConfigurationCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return true;
        return InternalConfigCommandRegex.IsMatch(command.Trim());
    }

    /// <summary>
    /// Masks sensitive credentials (passwords, tokens, bearer headers, embedded URL basic auth)
    /// in a command string so that sensitive information is not persisted in plain text in command history.
    /// </summary>
    public static string SanitizeCommandForHistory(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return string.Empty;

        var result = GitUrlCredentialsRegex.Replace(command, "${1}******${2}");
        result = SensitiveFlagRegex.Replace(result, "${1}******");
        result = SensitiveHeaderRegex.Replace(result, "${1}******");

        return result;
    }
}
