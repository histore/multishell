using System;
using MultiShell.Services;

namespace MultiShell.Models;

/// <summary>
/// Defines a configurable terminal profile for launching shells.
/// </summary>
public record TerminalProfile(
    Guid Id,
    string Name,
    string ExecutablePath,
    string? Arguments = null,
    string? WorkingDirectory = null,
    string IconTag = "PS",
    ShellType ShellType = ShellType.PowerShell,
    bool IsBuiltIn = false);
