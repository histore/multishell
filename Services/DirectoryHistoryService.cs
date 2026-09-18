using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MultiShell.Services;

/// <summary>
/// Thread-safe in-memory service maintaining a unified directory history across all terminal tabs.
/// Caps history to 100 entries, evicts older items using FIFO, removes duplicates by moving
/// re-visited paths to the newest position (MRU), and supports filesystem-backed exit pruning.
/// </summary>
public class DirectoryHistoryService : IDirectoryHistoryService
{
    /// <summary>
    /// Maximum number of directory history entries retained in the unified list.
    /// </summary>
    public const int MaxHistoryCount = 100;

    private readonly object _lock = new();
    private readonly List<string> _directories = new();

    /// <inheritdoc />
    public event Action? HistoryChanged;

    /// <summary>
    /// Normalizes a directory path for consistent comparison and display.
    /// Resolves full paths where possible, trims whitespace, and strips non-root trailing directory separators.
    /// </summary>
    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        try
        {
            var fullPath = Path.GetFullPath(trimmed);
            var root = Path.GetPathRoot(fullPath);
            if (!string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            {
                fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return fullPath;
        }
        catch
        {
            return trimmed.TrimEnd('\\', '/');
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetHistory()
    {
        lock (_lock)
        {
            return _directories.ToArray();
        }
    }

    /// <inheritdoc />
    public void RecordDirectory(string? directory)
    {
        var normalized = NormalizePath(directory);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        lock (_lock)
        {
            // Remove existing occurrence to ensure MRU ordering (newest at the end, no duplicates)
            var existingIndex = _directories.FindIndex(d => string.Equals(d, normalized, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                _directories.RemoveAt(existingIndex);
            }

            _directories.Add(normalized);

            // Cap at MaxHistoryCount using FIFO eviction
            while (_directories.Count > MaxHistoryCount)
            {
                _directories.RemoveAt(0);
            }
        }

        // Notify subscribers outside lock to avoid deadlocks
        HistoryChanged?.Invoke();
    }

    /// <inheritdoc />
    public void PruneNonExistentDirectories()
    {
        bool changed = false;
        lock (_lock)
        {
            var countBefore = _directories.Count;
            _directories.RemoveAll(d => string.IsNullOrWhiteSpace(d) || !Directory.Exists(d));
            changed = _directories.Count != countBefore;
        }

        if (changed)
        {
            HistoryChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public List<string> ExportAll()
    {
        lock (_lock)
        {
            return new List<string>(_directories);
        }
    }

    /// <inheritdoc />
    public void ImportAll(IEnumerable<string>? directories)
    {
        if (directories == null) return;

        bool changed = false;
        lock (_lock)
        {
            foreach (var dir in directories)
            {
                var normalized = NormalizePath(dir);
                if (string.IsNullOrEmpty(normalized)) continue;

                var existingIndex = _directories.FindIndex(d => string.Equals(d, normalized, StringComparison.OrdinalIgnoreCase));
                if (existingIndex >= 0)
                {
                    _directories.RemoveAt(existingIndex);
                }

                _directories.Add(normalized);
                changed = true;

                while (_directories.Count > MaxHistoryCount)
                {
                    _directories.RemoveAt(0);
                }
            }
        }

        if (changed)
        {
            HistoryChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        bool changed;
        lock (_lock)
        {
            changed = _directories.Count > 0;
            _directories.Clear();
        }

        if (changed)
        {
            HistoryChanged?.Invoke();
        }
    }
}
