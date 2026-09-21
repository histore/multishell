using System;
using System.Threading.Tasks;

namespace MultiShell.Services;

/// <summary>
/// Service contract for managing single-instance application lifecycle and IPC communication.
/// </summary>
public interface ISingleInstanceService : IDisposable
{
    /// <summary>
    /// Gets whether this process is the primary (first) instance of the application.
    /// </summary>
    bool IsFirstInstance { get; }

    /// <summary>
    /// Event triggered on the primary instance when a secondary instance requests opening a directory.
    /// Payload is the normalized directory path, or null if invoked without path arguments.
    /// </summary>
    event Action<string?>? DirectoryOpenRequested;

    /// <summary>
    /// Starts the background IPC listener to receive commands from secondary instances.
    /// </summary>
    void StartServer();

    /// <summary>
    /// Sends command-line arguments from a secondary instance to the running primary instance.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the secondary instance.</param>
    /// <param name="clientCurrentDirectory">Current working directory of the secondary instance caller.</param>
    /// <param name="timeoutMs">Connection timeout in milliseconds.</param>
    /// <returns>True if message was delivered successfully; otherwise false.</returns>
    Task<bool> SendArgsToFirstInstanceAsync(string[] args, string clientCurrentDirectory, int timeoutMs = 2000);
}
