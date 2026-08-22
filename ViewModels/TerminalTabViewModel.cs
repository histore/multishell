using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Media;
using SvcSystems.UI.Terminal;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Services;

namespace MultiShell.ViewModels;

/// <summary>
/// ViewModel representing a terminal tab backed by a real ConPTY PowerShell session and TerminalControlModel.
/// Tracks live command history and visited directory history for the tab overlay with Fuzzy Search filtering.
/// </summary>
public partial class TerminalTabViewModel : ViewModelBase, IDisposable
{
    private readonly IPowerShellSession _session;
    private readonly IFuzzySearchService _fuzzySearchService;
    private bool _isDisposed;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string? _workingDirectory;

    [ObservableProperty]
    private bool _isSelected;

    public bool IsRunning => _session.IsRunning;

    private static readonly IBrush DarkTerminalBackground = new SolidColorBrush(Color.Parse("#0E0F15"));
    private static readonly IBrush LightTerminalBackground = new SolidColorBrush(Color.Parse("#F8F9FC"));
    private static readonly IBrush DarkTerminalCaret = new SolidColorBrush(Color.Parse("#7AA2F7"));
    private static readonly IBrush LightTerminalCaret = new SolidColorBrush(Color.Parse("#2563EB"));

    [ObservableProperty]
    private bool _isDarkTerminalTheme = true;

    [ObservableProperty]
    private IBrush _terminalBackgroundBrush = DarkTerminalBackground;

    [ObservableProperty]
    private IBrush _terminalCaretBrush = DarkTerminalCaret;

    [ObservableProperty]
    private double _terminalFontSize = 12.0;

    [ObservableProperty]
    private string _commandFilterQuery = string.Empty;

    [ObservableProperty]
    private string _directoryFilterQuery = string.Empty;

    /// <summary>
    /// Updates the terminal surface colors based on dark/light preference.
    /// </summary>
    public void UpdateTheme(bool isDark)
    {
        IsDarkTerminalTheme = isDark;
        TerminalBackgroundBrush = isDark ? DarkTerminalBackground : LightTerminalBackground;
        TerminalCaretBrush = isDark ? DarkTerminalCaret : LightTerminalCaret;
    }

    /// <summary>
    /// Updates the terminal font size in points.
    /// </summary>
    /// <param name="fontSize">Font size in points.</param>
    public void UpdateFontSize(double fontSize)
    {
        TerminalFontSize = fontSize;
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
    /// The terminal model that drives the TerminalControl UI.
    /// </summary>
    public TerminalControlModel TerminalModel { get; }

    /// <summary>
    /// Event triggered when tab requests closure.
    /// </summary>
    public event Action<TerminalTabViewModel>? CloseRequested;

    /// <summary>
    /// Event triggered when the active working directory changes.
    /// </summary>
    public event Action<TerminalTabViewModel, string>? DirectoryChanged;

    /// <summary>
    /// Event triggered when command or directory history changes.
    /// </summary>
    public event Action<TerminalTabViewModel>? HistoryChanged;

    public TerminalTabViewModel(IPowerShellSession session, IFuzzySearchService? fuzzySearchService = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _fuzzySearchService = fuzzySearchService ?? new FuzzySearchService();
        _workingDirectory = session.WorkingDirectory;
        _title = !string.IsNullOrWhiteSpace(_workingDirectory) ? _workingDirectory : session.Title;

        if (!string.IsNullOrWhiteSpace(_workingDirectory))
        {
            DirectoryHistory.Add(_workingDirectory);
        }

        RefreshFilteredCommands();
        RefreshFilteredDirectories();

        TerminalModel = new TerminalControlModel(new TerminalOptions
        {
            ReflowOnResize = false,
        });

        // Wire PTY output -> terminal rendering, directory tracking & command tracking
        _session.DataReceived += OnSessionDataReceived;
        _session.Exited += OnSessionExited;
        _session.WorkingDirectoryChanged += OnSessionWorkingDirectoryChanged;
        _session.CommandExecuted += OnSessionCommandExecuted;

        // Wire terminal user input -> PTY stdin
        TerminalModel.UserInput += OnTerminalUserInput;

        // Wire terminal resize -> PTY resize
        TerminalModel.SizeChanged += OnTerminalSizeChanged;
    }

    partial void OnCommandFilterQueryChanged(string value)
    {
        RefreshFilteredCommands();
    }

    partial void OnDirectoryFilterQueryChanged(string value)
    {
        RefreshFilteredDirectories();
    }

    public void RefreshFilteredCommands()
    {
        var results = _fuzzySearchService.FilterAndRank(CommandHistory, CommandFilterQuery, x => x).ToList();
        FilteredCommandHistory.Clear();
        foreach (var item in results)
        {
            FilteredCommandHistory.Add(item);
        }
    }

    public void RefreshFilteredDirectories()
    {
        var results = _fuzzySearchService.FilterAndRank(DirectoryHistory, DirectoryFilterQuery, x => x).ToList();
        FilteredDirectoryHistory.Clear();
        foreach (var item in results)
        {
            FilteredDirectoryHistory.Add(item);
        }
    }

    /// <summary>
    /// Starts the underlying ConPTY PowerShell session.
    /// </summary>
    public void StartSession()
    {
        if (IsRunning) return;

        try
        {
            _session.Start();
            OnPropertyChanged(nameof(IsRunning));

            // Only apply resize if the control has already been measured
            if (TerminalModel.Terminal.Cols > 1 && TerminalModel.Terminal.Rows > 1)
            {
                _session.Resize(TerminalModel.Terminal.Cols, TerminalModel.Terminal.Rows);
            }
        }
        catch (Exception ex)
        {
            TerminalModel.Feed($"Failed to start PowerShell session.\r\n{ex.Message}\r\n");
            OnPropertyChanged(nameof(IsRunning));
        }
    }

    /// <summary>
    /// Restores previously persisted command and directory history into the tab collections.
    /// </summary>
    public void RestoreHistory(IEnumerable<string>? commands, IEnumerable<string>? directories)
    {
        if (commands != null)
        {
            CommandHistory.Clear();
            foreach (var cmd in commands)
            {
                if (!string.IsNullOrWhiteSpace(cmd) && !CommandHistory.Contains(cmd))
                {
                    CommandHistory.Add(cmd);
                }
            }
        }

        if (directories != null)
        {
            DirectoryHistory.Clear();
            foreach (var dir in directories)
            {
                if (!string.IsNullOrWhiteSpace(dir) && !DirectoryHistory.Contains(dir))
                {
                    DirectoryHistory.Add(dir);
                }
            }
            if (!string.IsNullOrWhiteSpace(WorkingDirectory) && !DirectoryHistory.Contains(WorkingDirectory))
            {
                DirectoryHistory.Add(WorkingDirectory);
            }
        }

        RefreshFilteredCommands();
        RefreshFilteredDirectories();
    }

    private void OnSessionCommandExecuted(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        void AddCommand()
        {
            if (CommandHistory.Contains(command))
            {
                CommandHistory.Remove(command);
            }
            CommandHistory.Add(command);
            RefreshFilteredCommands();
            HistoryChanged?.Invoke(this);
        }

        if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            AddCommand();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(AddCommand);
        }
    }

    private void OnSessionWorkingDirectoryChanged(string newDir)
    {
        if (string.IsNullOrWhiteSpace(newDir)) return;

        void UpdateDir()
        {
            WorkingDirectory = newDir;
            Title = newDir;

            if (!DirectoryHistory.Contains(newDir))
            {
                DirectoryHistory.Add(newDir);
                RefreshFilteredDirectories();
            }

            DirectoryChanged?.Invoke(this, newDir);
        }

        if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            UpdateDir();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateDir);
        }
    }

    private void OnSessionDataReceived(byte[] data)
    {
        if (data.Length == 0) return;

        var text = Encoding.UTF8.GetString(data);
        if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            TerminalModel.Feed(text);
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => TerminalModel.Feed(text));
        }
    }

    private void OnTerminalUserInput(object? sender, TerminalUserInputEventArgs e)
    {
        if (!IsRunning || e.Data.IsEmpty) return;

        var bytes = e.Data.ToArray();
        _session.Send(bytes);
    }

    private void OnTerminalSizeChanged(object? sender, TerminalSizeChangedEventArgs e)
    {
        if (!IsRunning) return;

        if (e.Cols > 0 && e.Rows > 0)
        {
            _session.Resize(e.Cols, e.Rows);
        }
    }

    private void OnSessionExited(int exitCode)
    {
        void HandleExit()
        {
            OnPropertyChanged(nameof(IsRunning));
            TerminalModel.Feed($"\r\n[Process exited with code {exitCode}]\r\n");
            CloseRequested?.Invoke(this);
        }

        if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            HandleExit();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(HandleExit);
        }
    }

        [RelayCommand]
    public void RequestClose() => CloseRequested?.Invoke(this);

    [RelayCommand]
    public void ExecuteHistoryCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        var bytes = Encoding.UTF8.GetBytes(command);
        _session.Send(bytes);
    }

    [RelayCommand]
    public void NavigateToHistoryDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return;

        var escapedPath = $"\"{directory}\"";
        var command = $"Set-Location -LiteralPath {escapedPath}";
        var bytes = Encoding.UTF8.GetBytes(command);
        _session.Send(bytes);
    }

    [RelayCommand]
    public void NavigateToDirectory(string? directory) => NavigateToHistoryDirectory(directory);

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _session.DataReceived -= OnSessionDataReceived;
        _session.Exited -= OnSessionExited;
        _session.WorkingDirectoryChanged -= OnSessionWorkingDirectoryChanged;
        _session.CommandExecuted -= OnSessionCommandExecuted;
        TerminalModel.UserInput -= OnTerminalUserInput;
        TerminalModel.SizeChanged -= OnTerminalSizeChanged;

        _session.Dispose();
    }
}
