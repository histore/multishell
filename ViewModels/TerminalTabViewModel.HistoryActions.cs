using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Models;

namespace MultiShell.ViewModels;

/// <summary>
/// Partial class exposing history execution, insertion, directory navigation, and deletion commands.
/// </summary>
public partial class TerminalTabViewModel
{
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

        string command = BuildDirectoryNavigationCommand(directory, appendSubmitNewline: true);
        if (ShellType != ShellType.PowerShell)
        {
            CheckForDirectoryChangeCommand(command.Trim());
        }

        _session.Send(Encoding.UTF8.GetBytes(command));
    }

    /// <summary>
    /// Inserts the directory navigation command into the active terminal prompt without executing it.
    /// </summary>
    [RelayCommand]
    public void PasteHistoryDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return;

        string command = BuildDirectoryNavigationCommand(directory, appendSubmitNewline: false);
        var bytes = Encoding.UTF8.GetBytes(command);
        TrackInputBuffer(bytes);
        _session.Send(bytes);
    }

    private string BuildDirectoryNavigationCommand(string directory, bool appendSubmitNewline)
    {
        var cleanDir = Path.TrimEndingDirectorySeparator(directory.Trim());
        string baseCommand = ShellType switch
        {
            ShellType.CMD => $"cd /d \"{cleanDir}\"",
            ShellType.WSL => $"cd \"{cleanDir.Replace("\"", "\\\"")}\"",
            ShellType.NuShell => $"cd \"{cleanDir.Replace("\"", "\\\"")}\"",
            _ => $"Set-Location -LiteralPath \"{cleanDir}\""
        };

        if (!appendSubmitNewline)
        {
            return baseCommand;
        }

        return ShellType == ShellType.WSL ? baseCommand + "\n" : baseCommand + "\r";
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
}
