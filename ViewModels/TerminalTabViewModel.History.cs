using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Models;
using MultiShell.Services;

namespace MultiShell.ViewModels;

/// <summary>
/// Partial class for tab command history, visited directory tracking, global unified history,
/// fuzzy filtering, and command/navigation execution.
/// </summary>
public partial class TerminalTabViewModel
{
    [ObservableProperty]
    private string _commandFilterQuery = string.Empty;

    [ObservableProperty]
    private string _directoryFilterQuery = string.Empty;

    [ObservableProperty]
    private string _globalFilterQuery = string.Empty;

    /// <summary>
    /// Event triggered when command or directory history changes.
    /// </summary>
    public event Action<TerminalTabViewModel>? HistoryChanged;

    /// <summary>
    /// Event triggered when the active history search box should be focused and its content selected.
    /// </summary>
    public event Action? FocusAndSelectHistorySearchRequested;

    /// <summary>
    /// Requests focusing and selecting all text in the active history search box.
    /// </summary>
    public void RequestFocusAndSelectHistorySearch()
    {
        FocusAndSelectHistorySearchRequested?.Invoke();
    }

    /// <summary>
    /// Live history of commands executed in this tab.
    /// </summary>
    public ObservableCollection<string> CommandHistory { get; } = new();

    /// <summary>
    /// Chronological history of visited directories in this tab.
    /// </summary>
    public ObservableCollection<string> DirectoryHistory { get; } = new();

    /// <summary>
    /// Score-ranked fuzzy-filtered command history.
    /// </summary>
    public ObservableCollection<string> FilteredCommandHistory { get; } = new();

    /// <summary>
    /// Score-ranked fuzzy-filtered directory history.
    /// </summary>
    public ObservableCollection<string> FilteredDirectoryHistory { get; } = new();

    /// <summary>
    /// Unified history of commands and directories across all tabs.
    /// </summary>
    public ObservableCollection<GlobalHistoryItem> GlobalHistory { get; } = new();

    /// <summary>
    /// Score-ranked fuzzy-filtered global history of commands and directories.
    /// </summary>
    public ObservableCollection<GlobalHistoryItem> FilteredGlobalHistory { get; } = new();

    partial void OnCommandFilterQueryChanged(string value)
    {
        RefreshFilteredCommands();
    }

    partial void OnDirectoryFilterQueryChanged(string value)
    {
        RefreshFilteredDirectories();
    }

    partial void OnGlobalFilterQueryChanged(string value)
    {
        RefreshFilteredGlobalHistory();
    }

    public void RefreshFilteredCommands()
    {
        RefreshFilteredStringHistory(
            _commandHistoryLock,
            CommandHistory,
            FilteredCommandHistory,
            CommandFilterQuery,
            () => _pathCommandHistoryService.GetHistoryWithTimestamps(WorkingDirectory),
            StringComparer.Ordinal);
    }

    public void RefreshFilteredDirectories()
    {
        RefreshFilteredStringHistory(
            _directoryHistoryLock,
            DirectoryHistory,
            FilteredDirectoryHistory,
            DirectoryFilterQuery,
            () => _directoryHistoryService.GetHistoryWithTimestamps(),
            StringComparer.OrdinalIgnoreCase);
    }

    private void RefreshFilteredStringHistory(
        System.Threading.Lock syncLock,
        ObservableCollection<string> source,
        ObservableCollection<string> target,
        string filterQuery,
        Func<IEnumerable<(string Item, DateTime LastUsedAt)>> getTimestampedEntries,
        StringComparer keyComparer)
    {
        lock (syncLock)
        {
            var timestampMap = new Dictionary<string, DateTime>(keyComparer);
            foreach (var (item, time) in getTimestampedEntries())
            {
                if (!timestampMap.ContainsKey(item))
                {
                    timestampMap[item] = time;
                }
            }

            var snapshot = source.ToArray();
            var results = string.IsNullOrWhiteSpace(filterQuery)
                ? snapshot
                : _fuzzySearchService.FilterAndRank(
                    snapshot,
                    filterQuery,
                    x => x,
                    x => timestampMap.TryGetValue(x, out var t) ? t : DateTime.MinValue,
                    secondaryDescending: true).ToArray();

            SyncCollection(target, results);
        }
    }

    public void RefreshFilteredGlobalHistory()
    {
        lock (_globalHistoryLock)
        {
            var snapshot = GlobalHistory.ToArray();
            var results = string.IsNullOrWhiteSpace(GlobalFilterQuery)
                ? snapshot.OrderByDescending(x => x.LastUsedAt).ToArray()
                : _fuzzySearchService.FilterAndRank(
                    snapshot,
                    GlobalFilterQuery,
                    x => x.Text,
                    x => x.LastUsedAt,
                    secondaryDescending: true).ToArray();

            SyncCollection(FilteredGlobalHistory, results);
        }
    }

    public void SyncGlobalHistory()
    {
        lock (_globalHistoryLock)
        {
            var commands = _pathCommandHistoryService.GetAllCommandsWithTimestamps();
            var directories = _directoryHistoryService.GetHistoryWithTimestamps();

            var combined = new List<GlobalHistoryItem>(commands.Count + directories.Count);
            foreach (var (cmd, lastUsed) in commands)
            {
                combined.Add(new GlobalHistoryItem(cmd, GlobalHistoryItemType.Command, lastUsed));
            }
            foreach (var (dir, lastUsed) in directories)
            {
                combined.Add(new GlobalHistoryItem(dir, GlobalHistoryItemType.Directory, lastUsed));
            }

            var sorted = combined.OrderByDescending(x => x.LastUsedAt).ToList();

            SyncCollection(GlobalHistory, sorted);
            RefreshFilteredGlobalHistory();
        }
    }

    public void RestoreHistory(IEnumerable<string>? commands, IEnumerable<string>? directories)
    {
        if (commands != null)
        {
            var existingHistory = _pathCommandHistoryService.GetHistory(WorkingDirectory);
            if (existingHistory.Count == 0)
            {
                var validCommands = commands
                    .Where(c => !string.IsNullOrWhiteSpace(c) && !IsInternalConfigurationCommand(c))
                    .ToList();

                if (validCommands.Count > 0)
                {
                    _pathCommandHistoryService.ImportAll(new Dictionary<string, List<string>>
                    {
                        [WorkingDirectory ?? string.Empty] = validCommands
                    });
                }
            }
            SyncCommandHistoryFromPath();
        }

        if (directories != null)
        {
            var validDirs = directories
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .ToList();

            if (validDirs.Count > 0)
            {
                _directoryHistoryService.HistoryChanged -= OnSharedDirectoryHistoryChanged;
                try
                {
                    _directoryHistoryService.ImportAll(validDirs);
                }
                finally
                {
                    _directoryHistoryService.HistoryChanged += OnSharedDirectoryHistoryChanged;
                }
            }
            SyncDirectoryHistory();
        }

        RefreshFilteredCommands();
        RefreshFilteredDirectories();
        SyncGlobalHistory();
    }

    private void SyncDirectoryHistory()
    {
        lock (_directoryHistoryLock)
        {
            var history = _directoryHistoryService.GetHistoryWithTimestamps();
            DirectoryHistory.Clear();
            foreach (var (dir, _) in history.OrderByDescending(x => x.LastUsedAt))
            {
                DirectoryHistory.Add(dir);
            }
            RefreshFilteredDirectories();
        }
    }

    private void OnSharedDirectoryHistoryChanged()
    {
        void Update()
        {
            SyncDirectoryHistory();
            SyncGlobalHistory();
            HistoryChanged?.Invoke(this);
        }

        _dispatcherService.RunOnUi(Update);
    }

    private void SyncCommandHistoryFromPath()
    {
        lock (_commandHistoryLock)
        {
            var history = _pathCommandHistoryService.GetHistoryWithTimestamps(WorkingDirectory);
            CommandHistory.Clear();
            foreach (var (cmd, _) in history.OrderByDescending(x => x.LastUsedAt))
            {
                CommandHistory.Add(cmd);
            }
            RefreshFilteredCommands();
        }
    }

    private void OnPathHistoryChanged(string changedNormalizedPath)
    {
        void Update()
        {
            var currentNormalized = PathCommandHistoryService.NormalizePath(WorkingDirectory);
            if (string.IsNullOrEmpty(changedNormalizedPath) || string.Equals(currentNormalized, changedNormalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                SyncCommandHistoryFromPath();
            }
            SyncGlobalHistory();
            HistoryChanged?.Invoke(this);
        }

        _dispatcherService.RunOnUi(Update);
    }

    internal void HandlePaneCommandExecuted(TerminalPaneViewModel pane, string command)
    {
        if (_isDisposed || string.IsNullOrWhiteSpace(command) || IsInternalConfigurationCommand(command)) return;

        var targetDir = _pendingCommandDirectory
            ?? (!string.IsNullOrWhiteSpace(pane.WorkingDirectory) ? pane.WorkingDirectory : WorkingDirectory);
        _pendingCommandDirectory = null;

        var sanitizedCommand = ShellCommandFilter.SanitizeCommandForHistory(command);
        _pathCommandHistoryService.RecordCommand(targetDir, sanitizedCommand);
    }

    private void OnSessionCommandExecuted(string command)
    {
        if (_isDisposed || string.IsNullOrWhiteSpace(command) || IsInternalConfigurationCommand(command)) return;

        var initialPane = _panes.Count > 0 ? _panes[0] : null;
        var targetDir = initialPane?.PendingCommandDirectory
            ?? _pendingCommandDirectory
            ?? (!string.IsNullOrWhiteSpace(initialPane?.WorkingDirectory) ? initialPane.WorkingDirectory : WorkingDirectory);
        if (initialPane != null)
        {
            initialPane.PendingCommandDirectory = null;
        }
        _pendingCommandDirectory = null;

        var sanitizedCommand = ShellCommandFilter.SanitizeCommandForHistory(command);
        _pathCommandHistoryService.RecordCommand(targetDir, sanitizedCommand);
    }

    private void CheckForDirectoryChangeCommand(string command, TerminalPaneViewModel? sourcePane = null)
    {
        var baseDir = !string.IsNullOrWhiteSpace(sourcePane?.WorkingDirectory)
            ? sourcePane.WorkingDirectory
            : WorkingDirectory;

        if (ShellDirectoryChangeDetector.TryDetectDirectoryChange(command, baseDir, out var newDir) &&
            !string.IsNullOrWhiteSpace(newDir))
        {
            if (sourcePane != null)
            {
                sourcePane.WorkingDirectory = newDir;
                sourcePane.Title = newDir;
                HandlePaneWorkingDirectoryChanged(sourcePane, newDir);
            }
            else
            {
                OnSessionWorkingDirectoryChanged(newDir);
            }
        }
    }

    /// <summary>
    /// Checks if a command is an internal configuration, setup, or prompt-hook command that should be excluded from CommandHistory.
    /// </summary>
    public static bool IsInternalConfigurationCommand(string? command) =>
        ShellCommandFilter.IsInternalConfigurationCommand(command);

    /// <summary>
    /// Clears all history collections to release memory upon tab disposal.
    /// </summary>
    internal void ClearHistory()
    {
        lock (_commandHistoryLock)
        {
            CommandHistory.Clear();
            FilteredCommandHistory.Clear();
        }

        lock (_directoryHistoryLock)
        {
            DirectoryHistory.Clear();
            FilteredDirectoryHistory.Clear();
        }

        lock (_globalHistoryLock)
        {
            GlobalHistory.Clear();
            FilteredGlobalHistory.Clear();
        }
    }

    /// <summary>
    /// Synchronizes an ObservableCollection with a target list in-place to minimize UI CollectionChanged events.
    /// Emits zero events if both collections are already identical.
    /// </summary>
    internal static void SyncCollection<T>(ObservableCollection<T> target, IList<T> source, IEqualityComparer<T>? comparer = null)
    {
        comparer ??= EqualityComparer<T>.Default;

        // Fast-path: if count and elements are already identical, do nothing (0 events fired)
        if (target.Count == source.Count)
        {
            var allMatch = true;
            for (var i = 0; i < source.Count; i++)
            {
                if (!comparer.Equals(target[i], source[i]))
                {
                    allMatch = false;
                    break;
                }
            }
            if (allMatch) return;
        }

        // 1. Trim excess items from the end
        while (target.Count > source.Count)
        {
            target.RemoveAt(target.Count - 1);
        }

        // 2. Replace mismatched items in-place
        for (var i = 0; i < target.Count; i++)
        {
            if (!comparer.Equals(target[i], source[i]))
            {
                target[i] = source[i];
            }
        }

        // 3. Append newly added items
        for (var i = target.Count; i < source.Count; i++)
        {
            target.Add(source[i]);
        }
    }
}
