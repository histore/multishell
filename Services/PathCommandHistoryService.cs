using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MultiShell.Services;

/// <summary>
/// Thread-safe in-memory service maintaining command history grouped by normalized directory paths.
/// Enforces a maximum of 100 entries per path with FIFO pruning, immediate MRU deduplication,
/// and exit-time validation against the local filesystem.
/// </summary>
public class PathCommandHistoryService : IPathCommandHistoryService
{
    public const int MaxHistoryPerPath = 100;

    private readonly object _lock = new();
    private readonly Dictionary<string, List<string>> _pathHistories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _globalCommands = new();

    /// <inheritdoc />
    public event Action<string>? HistoryChangedForPath;

    /// <summary>
    /// Normalizes a directory path for consistent lookups across shells and platforms.
    /// Resolves full paths, normalizes directory separators, and strips non-root trailing separators.
    /// </summary>
    public static string NormalizePath(string? path) => PathNormalizer.Normalize(path, fallbackToCurrentDirectory: true);

    /// <inheritdoc />
    public IReadOnlyList<string> GetHistory(string? path)
    {
        var normalizedPath = NormalizePath(path);
        if (string.IsNullOrEmpty(normalizedPath))
        {
            return Array.Empty<string>();
        }

        lock (_lock)
        {
            if (_pathHistories.TryGetValue(normalizedPath, out var list))
            {
                return list.ToArray();
            }
        }

        return Array.Empty<string>();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAllCommands()
    {
        lock (_lock)
        {
            var result = new List<string>(_globalCommands.Count);
            for (var i = _globalCommands.Count - 1; i >= 0; i--)
            {
                result.Add(_globalCommands[i]);
            }
            return result;
        }
    }

    /// <inheritdoc />
    public void RecordCommand(string? path, string command)
    {
        if (string.IsNullOrWhiteSpace(command) || ShellCommandFilter.IsInternalConfigurationCommand(command))
        {
            return;
        }

        var normalizedPath = NormalizePath(path);
        if (string.IsNullOrEmpty(normalizedPath))
        {
            return;
        }

        lock (_lock)
        {
            if (!_pathHistories.TryGetValue(normalizedPath, out var list))
            {
                list = new List<string>();
                _pathHistories[normalizedPath] = list;
            }

            // Remove existing instance to push this entry to the newest position (MRU)
            list.Remove(command);
            list.Add(command);

            _globalCommands.Remove(command);
            _globalCommands.Add(command);

            // Cap at MaxHistoryPerPath (FIFO pruning: oldest entries are removed first)
            while (list.Count > MaxHistoryPerPath)
            {
                list.RemoveAt(0);
            }
        }

        // Notify subscribers outside lock to avoid deadlocks
        HistoryChangedForPath?.Invoke(normalizedPath);
    }

    /// <inheritdoc />
    public void PruneNonExistentPaths()
    {
        lock (_lock)
        {
            var keysToRemove = _pathHistories.Keys
                .Where(p => string.IsNullOrWhiteSpace(p) || !DirectoryHistoryService.DirectoryExistsOrNonWindows(p))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _pathHistories.Remove(key);
            }

            var remainingCmds = new HashSet<string>(_pathHistories.Values.SelectMany(l => l), StringComparer.Ordinal);
            _globalCommands.RemoveAll(cmd => !remainingCmds.Contains(cmd));
        }
    }

    /// <inheritdoc />
    public Dictionary<string, List<string>> ExportAll()
    {
        lock (_lock)
        {
            var export = new Dictionary<string, List<string>>(_pathHistories.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var (key, list) in _pathHistories)
            {
                export[key] = new List<string>(list);
            }
            return export;
        }
    }

    /// <inheritdoc />
    public void ImportAll(IDictionary<string, List<string>>? data)
    {
        if (data == null || data.Count == 0)
        {
            return;
        }

        lock (_lock)
        {
            foreach (var (path, commands) in data)
            {
                var normalizedPath = NormalizePath(path);
                if (string.IsNullOrEmpty(normalizedPath) || commands == null)
                {
                    continue;
                }

                if (!_pathHistories.TryGetValue(normalizedPath, out var list))
                {
                    list = new List<string>();
                    _pathHistories[normalizedPath] = list;
                }

                foreach (var cmd in commands)
                {
                    if (string.IsNullOrWhiteSpace(cmd) || ShellCommandFilter.IsInternalConfigurationCommand(cmd))
                    {
                        continue;
                    }

                    list.Remove(cmd);
                    list.Add(cmd);

                    _globalCommands.Remove(cmd);
                    _globalCommands.Add(cmd);

                    while (list.Count > MaxHistoryPerPath)
                    {
                        list.RemoveAt(0);
                    }
                }
            }
        }
    }
}
