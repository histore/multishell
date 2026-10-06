using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

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

    private readonly Lock _lock = new();
    private readonly List<string> _directories = [];
    private readonly Dictionary<string, DateTime> _directoryTimestamps = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastRecordedTimestamp = DateTime.MinValue;

    /// <inheritdoc />
    public event Action? HistoryChanged;

    /// <summary>
    /// Generates a strictly monotonic UTC timestamp to ensure sequential order is preserved.
    /// </summary>
    private DateTime GetMonotonicTimestamp()
    {
        var now = DateTime.UtcNow;
        if (now <= _lastRecordedTimestamp)
        {
            now = _lastRecordedTimestamp.AddTicks(1);
        }
        _lastRecordedTimestamp = now;
        return now;
    }

    /// <summary>
    /// Normalizes a directory path for consistent comparison and display.
    /// Resolves full paths where possible, trims whitespace, and strips non-root trailing directory separators.
    /// </summary>
    public static string NormalizePath(string? path) => PathNormalizer.Normalize(path, fallbackToCurrentDirectory: false);

    /// <inheritdoc />
    public IReadOnlyList<string> GetHistory()
    {
        lock (_lock)
        {
            return [.. _directories];
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<(string Directory, DateTime LastUsedAt)> GetHistoryWithTimestamps()
    {
        lock (_lock)
        {
            var result = new List<(string Directory, DateTime LastUsedAt)>(_directories.Count);
            foreach (var d in _directories)
            {
                var time = _directoryTimestamps.TryGetValue(d, out var dt) ? dt : DateTime.MinValue;
                result.Add((d, time));
            }
            return result;
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
            _directoryTimestamps[normalized] = GetMonotonicTimestamp();

            // Cap at MaxHistoryCount using FIFO eviction
            while (_directories.Count > MaxHistoryCount)
            {
                var evicted = _directories[0];
                _directories.RemoveAt(0);
                _directoryTimestamps.Remove(evicted);
            }
        }

        // Notify subscribers outside lock to avoid deadlocks
        HistoryChanged?.Invoke();
    }

    /// <summary>
    /// Checks if a directory path exists on disk, or if it represents a POSIX or WSL path that cannot be resolved via standard Windows filesystem APIs.
    /// </summary>
    public static bool DirectoryExistsOrNonWindows(string? path) => PathNormalizer.DirectoryExistsOrNonWindows(path);

    /// <inheritdoc />
    public void PruneNonExistentDirectories()
    {
        bool changed = false;
        lock (_lock)
        {
            var countBefore = _directories.Count;
            _directories.RemoveAll(d => string.IsNullOrWhiteSpace(d) || !DirectoryExistsOrNonWindows(d));
            changed = _directories.Count != countBefore;
            if (changed)
            {
                var activeSet = new HashSet<string>(_directories, StringComparer.OrdinalIgnoreCase);
                var keysToRemove = _directoryTimestamps.Keys.Where(k => !activeSet.Contains(k)).ToList();
                foreach (var k in keysToRemove)
                {
                    _directoryTimestamps.Remove(k);
                }
            }
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
                _directoryTimestamps[normalized] = GetMonotonicTimestamp();
                changed = true;

                while (_directories.Count > MaxHistoryCount)
                {
                    var evicted = _directories[0];
                    _directories.RemoveAt(0);
                    _directoryTimestamps.Remove(evicted);
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
            _directoryTimestamps.Clear();
        }

        if (changed)
        {
            HistoryChanged?.Invoke();
        }
    }
}
