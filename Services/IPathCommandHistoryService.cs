using System;
using System.Collections.Generic;

namespace MultiShell.Services;

/// <summary>
/// Service interface for path-bound command history management,
/// providing real-time synchronization across terminal tabs, 100-entry capacity capping,
/// and exit-time validation of existing directories.
/// </summary>
public interface IPathCommandHistoryService
{
    /// <summary>
    /// Event raised when the command history for a specific normalized path is modified.
    /// The parameter is the normalized directory path.
    /// </summary>
    event Action<string>? HistoryChangedForPath;

    /// <summary>
    /// Gets the list of commands for the specified path in chronological order (oldest to newest),
    /// or an empty list if none exist.
    /// </summary>
    IReadOnlyList<string> GetHistory(string? path);

    /// <summary>
    /// Gets the list of commands for the specified path paired with their last used UTC timestamps.
    /// </summary>
    IReadOnlyList<(string Command, DateTime LastUsedAt)> GetHistoryWithTimestamps(string? path);

    /// <summary>
    /// Gets all unique commands recorded across all tracked directory paths in reverse chronological (MRU) order.
    /// </summary>
    IReadOnlyList<string> GetAllCommands();

    /// <summary>
    /// Gets all unique commands recorded across all tracked directory paths in reverse chronological (MRU) order,
    /// paired with their last used UTC timestamps.
    /// </summary>
    IReadOnlyList<(string Command, DateTime LastUsedAt)> GetAllCommandsWithTimestamps();

    /// <summary>
    /// Records an executed command for the given path.
    /// Moves existing commands to the end (MRU), caps the total count at 100 entries per path,
    /// and triggers <see cref="HistoryChangedForPath"/>.
    /// </summary>
    void RecordCommand(string? path, string command);

    /// <summary>
    /// Prunes history entries for directories that no longer exist on disk.
    /// Typically invoked during application shutdown.
    /// </summary>
    void PruneNonExistentPaths();

    /// <summary>
    /// Removes a recorded command from the history of the specified directory path.
    /// If the command is no longer recorded in any directory path, it is also evicted from the global history.
    /// Triggers <see cref="HistoryChangedForPath"/> if the entry was removed.
    /// </summary>
    /// <param name="path">The directory path from which to remove the command.</param>
    /// <param name="command">The command string to remove.</param>
    /// <returns><c>true</c> if the command was found and removed; otherwise, <c>false</c>.</returns>
    bool RemoveCommand(string? path, string command);

    /// <summary>
    /// Removes a recorded command from all directory paths as well as the global history.
    /// Triggers <see cref="HistoryChangedForPath"/> for each affected path.
    /// </summary>
    /// <param name="command">The command string to remove globally.</param>
    /// <returns><c>true</c> if the command was removed from at least one path or global history; otherwise, <c>false</c>.</returns>
    bool RemoveFromAllPaths(string command);

    /// <summary>
    /// Exports all path histories for workspace persistence.
    /// </summary>
    Dictionary<string, List<string>> ExportAll();

    /// <summary>
    /// Imports existing path histories from workspace persistence.
    /// </summary>
    void ImportAll(IDictionary<string, List<string>>? data);
}
