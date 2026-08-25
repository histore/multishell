using System;

namespace MultiShell.Services;

/// <summary>
/// Represents an isolated interactive shell ConPTY terminal session.
/// </summary>
public interface IShellSession : IDisposable
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
    /// Type of shell being hosted in this session.
    /// </summary>
    ShellType ShellType { get; }

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
    /// Starts the underlying shell session.
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
