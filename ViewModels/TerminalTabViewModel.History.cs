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
        lock (_commandHistoryLock)
        {
            var historyWithTimestamps = _pathCommandHistoryService.GetHistoryWithTimestamps(WorkingDirectory);
            var timestampMap = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            foreach (var (cmd, time) in historyWithTimestamps)
            {
                if (!timestampMap.ContainsKey(cmd))
                {
                    timestampMap[cmd] = time;
                }
            }

            var snapshot = CommandHistory.ToArray();
            var results = string.IsNullOrWhiteSpace(CommandFilterQuery)
                ? snapshot
                : _fuzzySearchService.FilterAndRank(
                    snapshot,
                    CommandFilterQuery,
                    x => x,
                    x => timestampMap.TryGetValue(x, out var t) ? t : DateTime.MinValue,
                    secondaryDescending: true).ToArray();

            SyncCollection(FilteredCommandHistory, results);
        }
    }

    public void RefreshFilteredDirectories()
    {
        lock (_directoryHistoryLock)
        {
            var historyWithTimestamps = _directoryHistoryService.GetHistoryWithTimestamps();
            var timestampMap = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (var (dir, time) in historyWithTimestamps)
            {
                timestampMap[dir] = time;
            }

            var snapshot = DirectoryHistory.ToArray();
            var results = string.IsNullOrWhiteSpace(DirectoryFilterQuery)
                ? snapshot
                : _fuzzySearchService.FilterAndRank(
                    snapshot,
                    DirectoryFilterQuery,
                    x => x,
                    x => timestampMap.TryGetValue(x, out var t) ? t : DateTime.MinValue,
                    secondaryDescending: true).ToArray();

            SyncCollection(FilteredDirectoryHistory, results);
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

        if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Update);
        }
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

        if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Update);
        }
    }

    private void OnSessionCommandExecuted(string command)
    {
        if (string.IsNullOrWhiteSpace(command) || IsInternalConfigurationCommand(command)) return;

        var targetDir = _pendingCommandDirectory ?? WorkingDirectory;
        _pendingCommandDirectory = null;

        _pathCommandHistoryService.RecordCommand(targetDir, command);
    }

    private void CheckForDirectoryChangeCommand(string command)
    {
        if (ShellDirectoryChangeDetector.TryDetectDirectoryChange(command, WorkingDirectory, out var newDir) &&
            !string.IsNullOrWhiteSpace(newDir))
        {
            OnSessionWorkingDirectoryChanged(newDir);
        }
    }

    /// <summary>
    /// Checks if a command is an internal configuration, setup, or prompt-hook command that should be excluded from CommandHistory.
    /// </summary>
    public static bool IsInternalConfigurationCommand(string? command) =>
        ShellCommandFilter.IsInternalConfigurationCommand(command);

    [RelayCommand]
    public void ExecuteHistoryCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        var clean = command.Trim();
        MarkCommandStarted(clean);
        _pendingCommandDirectory = WorkingDirectory;
        if (ShellType != ShellType.PowerShell)
        {
            OnSessionCommandExecuted(clean);
            CheckForDirectoryChangeCommand(clean);
        }

        var commandWithEnter = clean.EndsWith('\r') || clean.EndsWith('\n') ? clean : clean + "\r";
        var bytes = Encoding.UTF8.GetBytes(commandWithEnter);
        _session.Send(bytes);
    }

    /// <summary>
    /// Inserts the history command into the active terminal prompt without executing it.
    /// </summary>
    [RelayCommand]
    public void PasteHistoryCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        var clean = command.Trim();
        var bytes = Encoding.UTF8.GetBytes(clean);
        TrackInputBuffer(bytes);
        _session.Send(bytes);
    }

    [RelayCommand]
    public void NavigateToHistoryDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return;

        var cleanDir = Path.TrimEndingDirectorySeparator(directory.Trim());
        string command = ShellType switch
        {
            ShellType.CMD => $"cd /d \"{cleanDir}\"\r",
            ShellType.WSL => $"cd \"{cleanDir.Replace("\"", "\\\"")}\"\n",
            ShellType.NuShell => $"cd \"{cleanDir.Replace("\"", "\\\"")}\"\r",
            _ => $"Set-Location -LiteralPath \"{cleanDir}\"\r"
        };

        if (ShellType != ShellType.PowerShell)
        {
            CheckForDirectoryChangeCommand(command.Trim());
        }

        var bytes = Encoding.UTF8.GetBytes(command);
        _session.Send(bytes);
    }

    /// <summary>
    /// Inserts the directory navigation command into the active terminal prompt without executing it.
    /// </summary>
    [RelayCommand]
    public void PasteHistoryDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return;

        var cleanDir = Path.TrimEndingDirectorySeparator(directory.Trim());
        string command = ShellType switch
        {
            ShellType.CMD => $"cd /d \"{cleanDir}\"",
            ShellType.WSL => $"cd \"{cleanDir.Replace("\"", "\\\"")}\"",
            ShellType.NuShell => $"cd \"{cleanDir.Replace("\"", "\\\"")}\"",
            _ => $"Set-Location -LiteralPath \"{cleanDir}\""
        };
        var bytes = Encoding.UTF8.GetBytes(command);
        TrackInputBuffer(bytes);
        _session.Send(bytes);
    }

    [RelayCommand]
    public void NavigateToDirectory(string? directory) => NavigateToHistoryDirectory(directory);

    [RelayCommand]
    public void ExecuteGlobalItem(GlobalHistoryItem? item)
    {
        if (item == null) return;
        if (item.IsCommand)
        {
            ExecuteHistoryCommand(item.Text);
        }
        else
        {
            NavigateToHistoryDirectory(item.Text);
        }
    }

    [RelayCommand]
    public void PasteGlobalItem(GlobalHistoryItem? item)
    {
        if (item == null) return;
        if (item.IsCommand)
        {
            PasteHistoryCommand(item.Text);
        }
        else
        {
            PasteHistoryDirectory(item.Text);
        }
    }

    /// <summary>
    /// Inserts a file or folder path into the active terminal prompt without a newline,
    /// automatically quoting the path if it contains spaces.
    /// </summary>
    [RelayCommand]
    public void InsertPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var clean = path.Trim();
        var formatted = clean.Contains(' ') && !clean.StartsWith('"') && !clean.EndsWith('"')
            ? $"\"{clean}\""
            : clean;

        var bytes = Encoding.UTF8.GetBytes(formatted);
        TrackInputBuffer(bytes);
        _session.Send(bytes);
        FocusTerminalRequested?.Invoke();
    }

    /// <summary>
    /// Deletes a recorded command from the current working directory's command history.
    /// </summary>
    [RelayCommand]
    public void DeleteCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        _pathCommandHistoryService.RemoveCommand(WorkingDirectory, command);
    }

    /// <summary>
    /// Deletes a recorded directory from the shared visited directory history.
    /// </summary>
    [RelayCommand]
    public void DeleteDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return;
        _directoryHistoryService.RemoveDirectory(directory);
    }

    /// <summary>
    /// Prunes directories that no longer exist on the local filesystem from the shared directory history.
    /// </summary>
    [RelayCommand]
    public void PruneDirectoryHistory()
    {
        _directoryHistoryService.PruneNonExistentDirectories();
    }

    /// <summary>
    /// Deletes a global history item (command across all paths, or directory from visited directories).
    /// </summary>
    [RelayCommand]
    public void DeleteGlobalItem(GlobalHistoryItem? item)
    {
        if (item == null) return;
        if (item.IsCommand)
        {
            _pathCommandHistoryService.RemoveFromAllPaths(item.Text);
        }
        else
        {
            _directoryHistoryService.RemoveDirectory(item.Text);
        }
    }

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
