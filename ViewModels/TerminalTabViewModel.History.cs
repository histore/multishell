using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
                timestampMap[cmd] = time;
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

            FilteredCommandHistory.Clear();
            foreach (var item in results)
            {
                FilteredCommandHistory.Add(item);
            }
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

            FilteredDirectoryHistory.Clear();
            foreach (var item in results)
            {
                FilteredDirectoryHistory.Add(item);
            }
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

            FilteredGlobalHistory.Clear();
            foreach (var item in results)
            {
                FilteredGlobalHistory.Add(item);
            }
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

            GlobalHistory.Clear();
            foreach (var item in sorted)
            {
                GlobalHistory.Add(item);
            }
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
            _directoryHistoryService.HistoryChanged -= OnSharedDirectoryHistoryChanged;
            try
            {
                _directoryHistoryService.ImportAll(directories);
            }
            finally
            {
                _directoryHistoryService.HistoryChanged += OnSharedDirectoryHistoryChanged;
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
            if (string.Equals(currentNormalized, changedNormalizedPath, StringComparison.OrdinalIgnoreCase))
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
        _session.Send(bytes);
    }

    [RelayCommand]
    public void NavigateToHistoryDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return;

        var escapedPath = $"\"{directory}\"";
        string command = ShellType switch
        {
            ShellType.CMD => $"cd /d {escapedPath}\r",
            ShellType.WSL => $"cd {escapedPath}\n",
            _ => $"Set-Location -LiteralPath {escapedPath}\r"
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

        var escapedPath = $"\"{directory}\"";
        string command = ShellType switch
        {
            ShellType.CMD => $"cd /d {escapedPath}",
            ShellType.WSL => $"cd {escapedPath}",
            _ => $"Set-Location -LiteralPath {escapedPath}"
        };
        var bytes = Encoding.UTF8.GetBytes(command);
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
        _session.Send(bytes);
        FocusTerminalRequested?.Invoke();
    }
}
