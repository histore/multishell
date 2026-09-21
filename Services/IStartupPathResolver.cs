namespace MultiShell.Services;

/// <summary>
/// Service contract for resolving startup command-line arguments (file or directory paths)
/// into a validated working directory.
/// </summary>
public interface IStartupPathResolver
{
    /// <summary>
    /// Resolves a single file or directory path into an absolute working directory path.
    /// If the path points to a file, returns the directory containing the file.
    /// If the path points to a directory, returns the normalized directory path.
    /// </summary>
    /// <param name="inputPath">Input file or folder path.</param>
    /// <param name="baseDirectory">Optional base directory for resolving relative paths (defaults to current directory).</param>
    /// <returns>Normalized absolute directory path, or null if unresolvable.</returns>
    string? ResolveWorkingDirectory(string? inputPath, string? baseDirectory = null);

    /// <summary>
    /// Resolves the first valid directory from an array of command-line arguments.
    /// </summary>
    /// <param name="args">Command-line argument array.</param>
    /// <param name="baseDirectory">Optional base directory for resolving relative paths.</param>
    /// <returns>Normalized absolute directory path, or null if no valid path was passed.</returns>
    string? ResolveFromArgs(string[]? args, string? baseDirectory = null);
}
