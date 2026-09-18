using System;
using System.Collections.Generic;

namespace MultiShell.Services;

/// <summary>
/// Service maintaining a shared, unified history of visited directories across all terminal tabs.
/// Enforces a maximum of 100 entries, FIFO eviction of older items, and MRU deduplication.
/// </summary>
public interface IDirectoryHistoryService
{
    /// <summary>
    /// Event triggered when the shared directory history is updated.
    /// </summary>
    event Action? HistoryChanged;

    /// <summary>
    /// Gets the current list of visited directories in chronological order (oldest to newest).
    /// </summary>
    IReadOnlyList<string> GetHistory();

    /// <summary>
    /// Records a visited directory into the shared history.
    /// If the path already exists, it is moved to the newest position (MRU).
    /// If the total entries exceed 100, the oldest entries are evicted (FIFO).
    /// </summary>
    /// <param name="directory">The directory path to record.</param>
    void RecordDirectory(string? directory);

    /// <summary>
    /// Prunes non-existent directories from the shared history using filesystem validation.
    /// </summary>
    void PruneNonExistentDirectories();

    /// <summary>
    /// Exports all currently tracked directories for persistence.
    /// </summary>
    List<string> ExportAll();

    /// <summary>
    /// Imports directories from saved workspace state, preserving MRU order and the 100-entry cap.
    /// </summary>
    /// <param name="directories">The directories to import.</param>
    void ImportAll(IEnumerable<string>? directories);

    /// <summary>
    /// Clears all recorded directories from the shared history.
    /// </summary>
    void Clear();
}
