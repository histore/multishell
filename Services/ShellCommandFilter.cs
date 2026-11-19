using System.Text.RegularExpressions;

namespace MultiShell.Services;

/// <summary>
/// Filters internal shell configuration commands, setup commands, and prompt hooks from command history,
/// and provides masking for sensitive credentials (passwords, tokens, bearer headers) before storage.
/// </summary>
public static partial class ShellCommandFilter
{
    [GeneratedRegex(@"^(chcp(\s+.*)?|\[Console\]::.*|\$OutputEncoding.*|\$function:prompt.*|Set-PSReadLineOption.*|__multishell_.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex GetInternalConfigCommandRegex();

    [GeneratedRegex(@"(https?://[^:\s]+:)[^@\s]+(@)", RegexOptions.IgnoreCase)]
    private static partial Regex GetGitUrlCredentialsRegex();

    [GeneratedRegex(@"(?i)(--(?:password|token|secret|api[_-]?key)(?:=|\s+))([^\s""']+|""[^""]*""|'[^']*')")]
    private static partial Regex GetSensitiveFlagRegex();

    [GeneratedRegex(@"(?i)(authorization:\s*bearer\s+|bearer\s+)([^\s""']+|""[^""]*""|'[^']*')")]
    private static partial Regex GetSensitiveHeaderRegex();

    /// <summary>
    /// Checks if a command is an internal configuration, setup, or prompt-hook command that should be excluded from CommandHistory.
    /// </summary>
    public static bool IsInternalConfigurationCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return true;
        return GetInternalConfigCommandRegex().IsMatch(command.Trim());
    }

    /// <summary>
    /// Masks sensitive credentials (passwords, tokens, bearer headers, embedded URL basic auth)
    /// in a command string so that sensitive information is not persisted in plain text in command history.
    /// </summary>
    public static string SanitizeCommandForHistory(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return string.Empty;

        var result = GetGitUrlCredentialsRegex().Replace(command, "${1}******${2}");
        result = GetSensitiveFlagRegex().Replace(result, "${1}******");
        result = GetSensitiveHeaderRegex().Replace(result, "${1}******");

        return result;
    }
}
