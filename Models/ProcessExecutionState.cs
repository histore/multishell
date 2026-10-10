namespace MultiShell.Models;

/// <summary>
/// Represents the execution state of an interactive shell session / tab.
/// </summary>
public enum ProcessExecutionState
{
    /// <summary>
    /// Shell is idle and waiting at prompt for user input.
    /// </summary>
    Idle,

    /// <summary>
    /// A command or process is actively running in the terminal.
    /// </summary>
    Running,

    /// <summary>
    /// The last executed command finished successfully (exit code 0).
    /// </summary>
    Success,

    /// <summary>
    /// The last executed command failed (non-zero exit code).
    /// </summary>
    Failed
}
