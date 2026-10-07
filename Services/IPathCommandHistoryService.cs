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
    /// Exports all path histories for workspace persistence.
    /// </summary>
    Dictionary<string, List<string>> ExportAll();

    /// <summary>
    /// Imports existing path histories from workspace persistence.
    /// </summary>
    void ImportAll(IDictionary<string, List<string>>? data);
}
