using System;

namespace MultiShell.Services;

/// <summary>
/// Represents an isolated interactive PowerShell ConPTY terminal session.
/// </summary>
public interface IPowerShellSession : IDisposable
{
    /// <summary>
    /// Unique identifier for this session.
    /// </summary>
    Guid SessionId { get; }

    /// <summary>
    /// Tab/Session title.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Current working directory tracked from the shell, or null if unknown.
    /// </summary>
    string? WorkingDirectory { get; }

    /// <summary>
    /// Whether the underlying shell process is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Triggered when output data is received from the shell.
    /// </summary>
    event Action<byte[]>? DataReceived;

    /// <summary>
    /// Triggered when the shell process exits.
    /// </summary>
    event Action<int>? Exited;

    /// <summary>
    /// Triggered when the current working directory changes via OSC escape sequence.
    /// </summary>
    event Action<string>? WorkingDirectoryChanged;

    /// <summary>
    /// Triggered when a command execution completes and is reported by the shell prompt hook.
    /// </summary>
    event Action<string>? CommandExecuted;

    /// <summary>
    /// Starts the underlying ConPTY PowerShell session.
    /// </summary>
    void Start();

    /// <summary>
    /// Sends user input bytes to the shell stdin.
    /// </summary>
    void Send(byte[] input);

    /// <summary>
    /// Resizes the pseudo console buffer dimensions.
    /// </summary>
    void Resize(int cols, int rows);
}
