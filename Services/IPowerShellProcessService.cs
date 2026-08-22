namespace MultiShell.Services;

/// <summary>
/// Factory service for creating isolated PowerShell ConPTY sessions.
/// </summary>
public interface IPowerShellProcessService
{
    /// <summary>
    /// Creates a new isolated PowerShell session instance.
    /// </summary>
    /// <param name="title">Initial title for the session.</param>
    /// <param name="workingDirectory">Optional initial working directory.</param>
    /// <returns>A new IPowerShellSession instance.</returns>
    IPowerShellSession CreateSession(string title, string? workingDirectory = null);
}
