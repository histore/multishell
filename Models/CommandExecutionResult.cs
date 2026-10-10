using System;

namespace MultiShell.Models;

/// <summary>
/// Immutable record capturing the result and timing of an executed shell command.
/// </summary>
public sealed record CommandExecutionResult(
    string Command,
    int ExitCode,
    TimeSpan Duration,
    ProcessExecutionState State);
