namespace MultiShell.Services;

/// <summary>
/// Shell types supported by the terminal.
/// </summary>
public enum ShellType
{
    PowerShell,
    NuShell,
    WSL,
    CMD
}

/// <summary>
/// Factory service for creating isolated shell sessions.
/// </summary>
public interface IShellProcessService
{
    /// <summary>
    /// Creates a new isolated shell session instance.
    /// </summary>
    /// <param name="title">Initial title for the session.</param>
    /// <param name="workingDirectory">Optional initial working directory.</param>
    /// <param name="shellType">The shell to execute (PowerShell by default).</param>
    /// <param name="customExecutable">Optional custom path to the executable.</param>
    /// <param name="customArguments">Optional startup arguments for the process.</param>
    /// <returns>A new IShellSession instance.</returns>
    IShellSession CreateSession(string title, string? workingDirectory = null, ShellType shellType = ShellType.PowerShell, string? customExecutable = null, string? customArguments = null);
}
