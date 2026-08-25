using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Services;
using SvcSystems.UI.Terminal;

namespace MultiShell.ViewModels;

/// <summary>
/// ViewModel representing a terminal tab backed by a real shell session and native Avalonia TerminalControl.
/// Tracks live command history and visited directory history for the tab overlay with Fuzzy Search filtering.
/// </summary>
public partial class TerminalTabViewModel : ViewModelBase, IDisposable
{
    private readonly IShellSession _session;
    private readonly IFuzzySearchService _fuzzySearchService;
    private bool _isDisposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    [NotifyPropertyChangedFor(nameof(TabTooltip))]
    private string _title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    [NotifyPropertyChangedFor(nameof(TabTooltip))]
    private string? _workingDirectory;

    /// <summary>
    /// Formats the tab title with a middle-ellipsis (e.g. C:\...\multishell) when space is limited.
    /// </summary>
    public string DisplayTitle => FormatMiddleEllipsis(Title);

    /// <summary>
    /// Gets the full, untruncated working directory path for display in the hover tooltip.
    /// </summary>
    public string TabTooltip => !string.IsNullOrWhiteSpace(WorkingDirectory)
        ? WorkingDirectory
        : (!string.IsNullOrWhiteSpace(Title) ? Title : _session.Title);

    public static string FormatMiddleEllipsis(string? text, int maxLength = 22)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        if (text.Length <= maxLength) return text;

        char sep = text.Contains('/') && !text.Contains('\\') ? '/' : '\\';
        var parts = text.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length <= 2)
        {
            // Truncate simple long text or 2-segment path in the middle
            int keep = (maxLength - 3) / 2;
            if (keep < 1) keep = 1;
            return text[..keep] + "..." + text[^keep..];
        }

        string root = text.StartsWith('/') ? "" : parts[0];
        string leaf = parts[^1];

        // Format: C:\...\multishell or /.../multishell
        string compact = $"{root}{sep}...{sep}{leaf}";
        if (compact.Length <= maxLength)
        {
            return compact;
        }

        // If leaf itself is too long, truncate leaf with an ellipsis at the end
        int rootLength = root.Length + 1; // root + sep
        int availableForLeaf = Math.Max(4, maxLength - rootLength - 4); // minus ...\
        if (leaf.Length > availableForLeaf)
        {
            return $"{root}{sep}...{sep}{leaf[..availableForLeaf]}…";
        }

        return compact;
    }

    [ObservableProperty]
    private bool _isSelected;

    public bool IsRunning => _session.IsRunning;

    /// <summary>
    /// The type of shell (PowerShell, NuShell, WSL, CMD) running in this tab.
    /// </summary>
    public ShellType ShellType => _session.ShellType;

    /// <summary>
    /// Short badge/tag representing the shell (e.g. "PS", "NU", "WSL", "CMD").
    /// </summary>
    public string ShellIconTag => ShellType switch
    {
        ShellType.PowerShell => "PS",
        ShellType.NuShell => "NU",
        ShellType.WSL => "WSL",
        ShellType.CMD => "CMD",
        _ => ">_"
    };

    /// <summary>
    /// The terminal control model providing ConPTY VT100/ANSI rendering for the UI.
    /// </summary>
    public TerminalControlModel TerminalModel { get; }

    private static readonly IBrush DarkTerminalBackground = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#0E0F15"));
    private static readonly IBrush LightTerminalBackground = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#F8F9FC"));
    private static readonly IBrush DarkTerminalCaret = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#7AA2F7"));
    private static readonly IBrush LightTerminalCaret = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#2563EB"));

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

    public void UpdateTheme(bool isDark)
    {
        IsDarkTerminalTheme = isDark;
        TerminalBackgroundBrush = isDark ? DarkTerminalBackground : LightTerminalBackground;
        TerminalCaretBrush = isDark ? DarkTerminalCaret : LightTerminalCaret;
    }

    public void UpdateFontSize(double fontSize)
    {
        TerminalFontSize = fontSize;
    }

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

    public TerminalTabViewModel(IShellSession session, IFuzzySearchService? fuzzySearchService = null)
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
    /// Starts the underlying ConPTY shell session.
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
            TerminalModel.Feed($"Failed to start shell session.\r\n{ex.Message}\r\n");
            OnPropertyChanged(nameof(IsRunning));
        }
    }

    /// <summary>
    /// Sends raw input bytes directly to the underlying PTY shell session.
    /// </summary>
    public void SendInput(byte[] input)
    {
        if (IsRunning && input.Length > 0)
        {
            TrackInputBuffer(input);
            _session.Send(input);
        }
    }

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

    private readonly StringBuilder _inputLineBuffer = new();

    /// <summary>
    /// Indicates whether AltGr (Ctrl+Alt modifier) is currently pressed.
    /// Used to suppress false-positive Control characters generated by the terminal control during AltGr key combos.
    /// </summary>
    public bool IsAltGrActive { get; set; }

    private void OnTerminalUserInput(object? sender, TerminalUserInputEventArgs e)
    {
        if (!IsRunning || e.Data.IsEmpty) return;

        var bytes = e.Data.ToArray();

        // When AltGr (Ctrl+Alt) is active on international keyboards (e.g. AltGr+Q for '@', AltGr+E for '€'),
        // SvcSystems.UI.Terminal misidentifies the key as Ctrl+Q (0x11) / Ctrl+E (0x05) in OnKeyDown.
        // We filter out these single control bytes so only the valid TextInput character (@, €, etc.) is sent.
        if (IsAltGrActive && bytes.Length == 1 && bytes[0] < 32 && bytes[0] != 9 && bytes[0] != 10 && bytes[0] != 13)
        {
            return;
        }

        // Track user input line for live CommandHistory tracking
        TrackInputBuffer(bytes);

        _session.Send(bytes);
    }

    private void TrackInputBuffer(byte[] bytes)
    {
        if (bytes.Length == 0) return;

        // Ignore ANSI escape sequences starting with ESC (0x1B) such as arrow keys
        if (bytes[0] == 0x1B) return;

        char[] chars;
        try
        {
            chars = Encoding.UTF8.GetChars(bytes);
        }
        catch
        {
            return;
        }

        lock (_inputLineBuffer)
        {
            foreach (var ch in chars)
            {
                if (ch == '\x03') // Ctrl+C -> cancel line
                {
                    _inputLineBuffer.Clear();
                    continue;
                }

                if (ch == '\r' || ch == '\n') // Enter / Return
                {
                    string executedCommand = _inputLineBuffer.ToString().Trim();
                    _inputLineBuffer.Clear();

                    if (!string.IsNullOrWhiteSpace(executedCommand))
                    {
                        CheckForDirectoryChangeCommand(executedCommand);
                        if (ShellType != ShellType.PowerShell)
                        {
                            OnSessionCommandExecuted(executedCommand);
                        }
                    }
                    continue;
                }

                if (ch == '\b' || ch == '\x7F') // Backspace
                {
                    if (_inputLineBuffer.Length > 0)
                    {
                        _inputLineBuffer.Length--;
                    }
                    continue;
                }

                if (!char.IsControl(ch))
                {
                    _inputLineBuffer.Append(ch);
                }
            }
        }
    }

    private void CheckForDirectoryChangeCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(WorkingDirectory)) return;

        var trimmed = command.Trim();
        string? targetPath = null;

        if (trimmed.StartsWith("cd ", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = trimmed[3..].Trim().Trim('"', '\'');
        }
        else if (trimmed.StartsWith("Set-Location ", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = trimmed[13..].Trim().Trim('"', '\'');
            if (targetPath.StartsWith("-LiteralPath ", StringComparison.OrdinalIgnoreCase))
            {
                targetPath = targetPath[13..].Trim().Trim('"', '\'');
            }
            else if (targetPath.StartsWith("-Path ", StringComparison.OrdinalIgnoreCase))
            {
                targetPath = targetPath[6..].Trim().Trim('"', '\'');
            }
        }

        if (string.IsNullOrWhiteSpace(targetPath)) return;

        try
        {
            if (targetPath == "..")
            {
                var parent = System.IO.Directory.GetParent(WorkingDirectory)?.FullName;
                if (!string.IsNullOrEmpty(parent) && System.IO.Directory.Exists(parent))
                {
                    OnSessionWorkingDirectoryChanged(parent);
                }
            }
            else if (targetPath == "\\" || targetPath == "/")
            {
                var root = System.IO.Path.GetPathRoot(WorkingDirectory);
                if (!string.IsNullOrEmpty(root) && System.IO.Directory.Exists(root))
                {
                    OnSessionWorkingDirectoryChanged(root);
                }
            }
            else if (System.IO.Path.IsPathRooted(targetPath))
            {
                if (System.IO.Directory.Exists(targetPath))
                {
                    OnSessionWorkingDirectoryChanged(System.IO.Path.GetFullPath(targetPath));
                }
            }
            else
            {
                var combined = System.IO.Path.Combine(WorkingDirectory, targetPath);
                if (System.IO.Directory.Exists(combined))
                {
                    OnSessionWorkingDirectoryChanged(System.IO.Path.GetFullPath(combined));
                }
            }
        }
        catch { }
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

        var clean = command.Trim();
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
