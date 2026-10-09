using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Models;
using MultiShell.Services;
using SvcSystems.UI.Terminal;

namespace MultiShell.ViewModels;

/// <summary>
/// ViewModel representing a terminal tab backed by a real shell session and native Avalonia TerminalControl.
/// Core partial class managing session lifecycle, PTY I/O streaming, terminal buffer formatting, and theme coordination.
/// </summary>
public partial class TerminalTabViewModel : ViewModelBase, IDisposable
{
    private readonly IShellSession _session;
    private readonly IFuzzySearchService _fuzzySearchService;
    private readonly IPathCommandHistoryService _pathCommandHistoryService;
    private readonly IDirectoryHistoryService _directoryHistoryService;
    private readonly Lock _commandHistoryLock = new();
    private readonly Lock _directoryHistoryLock = new();
    private readonly Lock _globalHistoryLock = new();
    private readonly EventHandler<Avalonia.AvaloniaPropertyChangedEventArgs>? _terminalModelPropertyChangedHandler;
    private string? _pendingCommandDirectory;
    private bool _isDisposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    [NotifyPropertyChangedFor(nameof(TabTooltip))]
    private string _title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    [NotifyPropertyChangedFor(nameof(TabTooltip))]
    private string? _workingDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPathColorStripes))]
    private IReadOnlyList<PathColorStripe> _pathColorStripes = Array.Empty<PathColorStripe>();

    /// <summary>
    /// Gets whether this tab currently has one or more dynamic path color stripes (REQ-TAB-025).
    /// </summary>
    public bool HasPathColorStripes => PathColorStripes != null && PathColorStripes.Count > 0;

    /// <summary>
    /// Formats the tab title with a middle-ellipsis (e.g. C:\...\multishell) when space is limited,
    /// or returns the custom title if one has been assigned.
    /// </summary>
    public string DisplayTitle => HasCustomTitle ? CustomTitle! : FormatMiddleEllipsis(Title);

    /// <summary>
    /// Gets the full, untruncated working directory path for display in the hover tooltip.
    /// </summary>
    public string TabTooltip => HasCustomTitle
        ? (!string.IsNullOrWhiteSpace(WorkingDirectory) && !string.Equals(CustomTitle?.TrimEnd('\\', '/'), WorkingDirectory?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
            ? $"{CustomTitle} ({WorkingDirectory})"
            : CustomTitle!)
        : (!string.IsNullOrWhiteSpace(WorkingDirectory)
            ? WorkingDirectory
            : (!string.IsNullOrWhiteSpace(Title) ? Title : _session.Title));

    /// <summary>
    /// Formats the tab title with a middle-ellipsis (e.g. C:\...\multishell) when space is limited.
    /// </summary>
    public static string FormatMiddleEllipsis(string? text, int maxLength = 22) =>
        TerminalTextFormatter.FormatMiddleEllipsis(text, maxLength);

    /// <summary>
    /// Cleans raw text copied from the terminal buffer by trimming trailing spaces from each line
    /// and removing trailing blank lines caused by fixed rectangular buffer selection.
    /// </summary>
    public static string CleanSelectedTerminalText(string? rawSelectedText) =>
        TerminalTextFormatter.CleanSelectedTerminalText(rawSelectedText);

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
    private FontFamily _terminalFontFamily = new("avares://MultiShell/Assets/Fonts#FiraCode Nerd Font Mono, Cascadia Code NF, CascadiaMono NF, CaskaydiaMono Nerd Font, Cascadia Mono, Cascadia Code, Consolas, Segoe UI Symbol, Segoe UI Emoji, DejaVu Sans Mono, monospace");

    /// <summary>
    /// Dynamic localization service for the tab and in-terminal search.
    /// </summary>
    public ILocalizationService Loc { get; }

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

    public TerminalTabViewModel(
        IShellSession session,
        IFuzzySearchService? fuzzySearchService = null,
        IPathCommandHistoryService? pathCommandHistoryService = null,
        IDirectoryHistoryService? directoryHistoryService = null,
        ILocalizationService? localizationService = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _fuzzySearchService = fuzzySearchService ?? new FuzzySearchService();
        _pathCommandHistoryService = pathCommandHistoryService ?? new PathCommandHistoryService();
        _directoryHistoryService = directoryHistoryService ?? new DirectoryHistoryService();
        Loc = localizationService ?? new LocalizationService();
        Loc.PropertyChanged += OnLocalizationPropertyChanged;
        _workingDirectory = session.WorkingDirectory;
        _title = !string.IsNullOrWhiteSpace(_workingDirectory) ? _workingDirectory : session.Title;

        SyncCommandHistoryFromPath();

        if (!string.IsNullOrWhiteSpace(_workingDirectory))
        {
            _directoryHistoryService.RecordDirectory(_workingDirectory);
        }
        SyncDirectoryHistory();

        RefreshFilteredCommands();
        RefreshFilteredDirectories();

        _pathCommandHistoryService.HistoryChangedForPath += OnPathHistoryChanged;
        _directoryHistoryService.HistoryChanged += OnSharedDirectoryHistoryChanged;
        SyncGlobalHistory();

        TerminalModel = new TerminalControlModel(new TerminalOptions
        {
            ReflowOnResize = false,
        });

        _terminalModelPropertyChangedHandler = (s, e) =>
        {
            if (e.Property.Name == "SearchResultCount")
            {
                SearchResultCount = TerminalModel.SearchResultCount;
                UpdateSearchMatchSummary();
            }
            else if (e.Property.Name == "CurrentSearchResultIndex")
            {
                CurrentSearchResultIndex = TerminalModel.CurrentSearchResultIndex;
                UpdateSearchMatchSummary();
            }
        };
        TerminalModel.PropertyChanged += _terminalModelPropertyChangedHandler;

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

    private void OnSessionWorkingDirectoryChanged(string newDir)
    {
        if (string.IsNullOrWhiteSpace(newDir)) return;

        void UpdateDir()
        {
            WorkingDirectory = newDir;
            Title = newDir;

            _directoryHistoryService.RecordDirectory(newDir);

            SyncCommandHistoryFromPath();
            HistoryChanged?.Invoke(this);

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

    /// <summary>
    /// Opens the platform-specific native file browser in this tab's current working directory.
    /// </summary>
    [RelayCommand]
    public void OpenInFileBrowser()
    {
        FileBrowserHelper.OpenInFileBrowser(WorkingDirectory);
    }

    /// <summary>
    /// Scrolls the terminal scrollback buffer up by one page (REQ-TERM-003).
    /// </summary>
    public void PageUp()
    {
        TerminalModel.PageUp();
    }

    /// <summary>
    /// Scrolls the terminal scrollback buffer down by one page (REQ-TERM-003).
    /// </summary>
    public void PageDown()
    {
        TerminalModel.PageDown();
    }

    /// <summary>
    /// Sends the VT escape sequence for PageUp to the active PTY shell session (REQ-TERM-003).
    /// Plain PageUp: \x1b[5~, Alt+PageUp: \x1b[5;3~
    /// </summary>
    public void SendPageUp(bool alt = false)
    {
        SendInput(alt ? "\u001b[5;3~"u8.ToArray() : "\u001b[5~"u8.ToArray());
    }

    /// <summary>
    /// Sends the VT escape sequence for PageDown to the active PTY shell session (REQ-TERM-003).
    /// Plain PageDown: \x1b[6~, Alt+PageDown: \x1b[6;3~
    /// </summary>
    public void SendPageDown(bool alt = false)
    {
        SendInput(alt ? "\u001b[6;3~"u8.ToArray() : "\u001b[6~"u8.ToArray());
    }

    /// <summary>
    /// Clears the terminal screen and scrollback buffer (REQ-TERM-003).
    /// </summary>
    public void ClearBuffer()
    {
        // ANSI sequence: \x1b[2J (erase screen), \x1b[3J (erase scrollback), \x1b[H (cursor home)
        const string clearSequence = "\x1b[2J\x1b[3J\x1b[H";
        if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            TerminalModel.Feed(clearSequence);
            TerminalModel.ClearSelection();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                TerminalModel.Feed(clearSequence);
                TerminalModel.ClearSelection();
            });
        }
    }

    private void OnLocalizationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdateSearchMatchSummary();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Loc.PropertyChanged -= OnLocalizationPropertyChanged;
        _session.DataReceived -= OnSessionDataReceived;
        _session.Exited -= OnSessionExited;
        _session.WorkingDirectoryChanged -= OnSessionWorkingDirectoryChanged;
        _session.CommandExecuted -= OnSessionCommandExecuted;
        _pathCommandHistoryService.HistoryChangedForPath -= OnPathHistoryChanged;
        _directoryHistoryService.HistoryChanged -= OnSharedDirectoryHistoryChanged;
        TerminalModel.UserInput -= OnTerminalUserInput;
        TerminalModel.SizeChanged -= OnTerminalSizeChanged;
        TerminalModel.UpdateUI = null;
        if (_terminalModelPropertyChangedHandler != null)
        {
            TerminalModel.PropertyChanged -= _terminalModelPropertyChangedHandler;
        }

        CloseRequested = null;
        DirectoryChanged = null;
        HistoryChanged = null;
        FocusRenameBoxRequested = null;
        FocusSearchBoxRequested = null;
        FocusTerminalRequested = null;

        _session.Dispose();

        ClearHistory();

        lock (_pendingUiFeedLock)
        {
            _pendingUiFeedBuffer.Clear();
            _pendingUiFeedBuffer.Capacity = 0;
            _isUiFeedScheduled = false;
        }
    }
}
