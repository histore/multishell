using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Models;
using MultiShell.Services;

namespace MultiShell.ViewModels;

/// <summary>
/// ViewModel representing a terminal tab backed by a real shell session and native Avalonia TerminalControl.
/// Core partial class managing session lifecycle, PTY I/O streaming, terminal buffer formatting, and theme coordination.
/// </summary>
public partial class TerminalTabViewModel : ViewModelBase, IDisposable, ICoordinatedTab
{
    private readonly IShellSession _session;
    private readonly IFuzzySearchService _fuzzySearchService;
    private readonly IPathCommandHistoryService _pathCommandHistoryService;
    private readonly IDirectoryHistoryService _directoryHistoryService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ITerminalModelFactory _terminalModelFactory;
    private readonly Lock _commandHistoryLock = new();
    private readonly Lock _directoryHistoryLock = new();
    private readonly Lock _globalHistoryLock = new();
    private readonly EventHandler? _terminalModelSearchStateChangedHandler;
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

    partial void OnWorkingDirectoryChanged(string? oldValue, string? newValue)
    {
        UpdateBreadcrumbs();
    }

    [ObservableProperty]
    private bool _hasDraftCommand;

    partial void OnHasDraftCommandChanged(bool oldValue, bool newValue)
    {
        UpdateBreadcrumbs();
    }

    [ObservableProperty]
    private IReadOnlyList<PathBreadcrumbSegment> _pathBreadcrumbs = Array.Empty<PathBreadcrumbSegment>();

    /// <summary>
    /// Delegate invoked by the UI layer to show a folder picker dialog and return the selected directory path.
    /// </summary>
    public Func<string?, Task<string?>>? RequestFolderPickerAsync { get; set; }

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
    public string DisplayTitle
    {
        get
        {
            var baseTitle = HasCustomTitle ? CustomTitle! : FormatMiddleEllipsis(Title);
            if (!HasMultiplePanes)
            {
                return baseTitle;
            }

            if (IsZoomed)
            {
                var activeIndex = ActivePane != null ? Panes.IndexOf(ActivePane) + 1 : 1;
                return $"⛶ {baseTitle} [{activeIndex}/{PaneCount}]";
            }

            return $"◫ {baseTitle} [{PaneCount}]";
        }
    }

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

    public bool IsRunning => _panes.Count > 0 ? _panes.Any(p => p.IsRunning) : _session.IsRunning;

    /// <summary>
    /// Gets the currently active shell session (active split pane if available, otherwise the initial session).
    /// </summary>
    internal IShellSession ActiveSession => ActivePane?.Session ?? _session;

    /// <summary>
    /// Gets the currently active terminal model adapter (active split pane if available, otherwise the initial model).
    /// </summary>
    internal ITerminalModelAdapter ActiveTerminalModel => ActivePane?.TerminalModel ?? TerminalModel;

    /// <summary>
    /// Gets the underlying shell session running in this tab.
    /// </summary>
    public IShellSession Session => ActiveSession;

    /// <summary>
    /// Gets whether this tab has been disposed.
    /// </summary>
    public bool IsDisposed => _isDisposed;

    /// <summary>
    /// The type of shell (PowerShell, NuShell, WSL, CMD) running in this tab.
    /// </summary>
    public ShellType ShellType => ActiveSession.ShellType;

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
    /// The terminal model adapter providing ConPTY VT100/ANSI rendering for the UI.
    /// </summary>
    public ITerminalModelAdapter TerminalModel { get; }

    private const string DarkTerminalBackground = "#0E0F15";
    private const string LightTerminalBackground = "#F8F9FC";
    private const string DarkTerminalCaret = "#7AA2F7";
    private const string LightTerminalCaret = "#2563EB";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExecutionStatusForeground))]
    private bool _isDarkTerminalTheme = true;

    [ObservableProperty]
    private string _terminalBackgroundBrush = DarkTerminalBackground;

    [ObservableProperty]
    private string _terminalCaretBrush = DarkTerminalCaret;

    [ObservableProperty]
    private double _terminalFontSize = 14.0;

    [ObservableProperty]
    private string _terminalFontFamily = UiServiceDefaults.DefaultTerminalFontFamily;

    /// <summary>
    /// Dynamic localization service for the tab and in-terminal search.
    /// </summary>
    public ILocalizationService Loc { get; }

    public void UpdateTheme(bool isDark)
    {
        IsDarkTerminalTheme = isDark;
        TerminalBackgroundBrush = isDark ? DarkTerminalBackground : LightTerminalBackground;
        TerminalCaretBrush = isDark ? DarkTerminalCaret : LightTerminalCaret;
        if (_panes != null)
        {
            foreach (var pane in _panes)
            {
                pane.UpdateTheme(isDark);
            }
        }
    }

    public void UpdateFontSize(double fontSize)
    {
        TerminalFontSize = fontSize;
        if (_panes != null)
        {
            foreach (var pane in _panes)
            {
                pane.UpdateFontSize(fontSize);
            }
        }
    }

    /// <summary>
    /// Event triggered when tab requests closure.
    /// </summary>
    public event Action<TerminalTabViewModel>? CloseRequested;
    public event Action<TerminalTabViewModel>? UndockRequested;

    /// <summary>
    /// Event triggered when the active working directory changes.
    /// </summary>
    public event Action<TerminalTabViewModel, string>? DirectoryChanged;

    public TerminalTabViewModel(
        IShellSession session,
        IFuzzySearchService? fuzzySearchService = null,
        IPathCommandHistoryService? pathCommandHistoryService = null,
        IDirectoryHistoryService? directoryHistoryService = null,
        ILocalizationService? localizationService = null,
        IDispatcherService? dispatcherService = null,
        ITerminalModelFactory? terminalModelFactory = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _fuzzySearchService = fuzzySearchService ?? new FuzzySearchService();
        _pathCommandHistoryService = pathCommandHistoryService ?? new PathCommandHistoryService();
        _directoryHistoryService = directoryHistoryService ?? new DirectoryHistoryService();
        _dispatcherService = dispatcherService ?? UiServiceDefaults.DispatcherService;
        _terminalModelFactory = terminalModelFactory ?? UiServiceDefaults.TerminalModelFactory;
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

        TerminalModel = _terminalModelFactory.Create();

        _terminalModelSearchStateChangedHandler = (_, _) =>
        {
            SearchResultCount = TerminalModel.SearchResultCount;
            CurrentSearchResultIndex = TerminalModel.CurrentSearchResultIndex;
            UpdateSearchMatchSummary();
        };
        TerminalModel.SearchStateChanged += _terminalModelSearchStateChangedHandler;

        Panes = new ReadOnlyObservableCollection<TerminalPaneViewModel>(_panes);
        _uiFeedCoordinator = new TerminalUiFeedCoordinator(_pendingUiFeedBuffer);
        var initialPane = new TerminalPaneViewModel(this, session, IsDarkTerminalTheme, TerminalFontSize, TerminalFontFamily, TerminalModel, _dispatcherService, _terminalModelFactory);
        _initialPane = initialPane;
        InitializeSplitPanes(initialPane);

        // Wire PTY output -> terminal rendering, directory tracking & command tracking
        // Session exit is coordinated via TerminalPaneViewModel -> HandlePaneSessionExited so split panes collapse cleanly.
        _session.DataReceived += OnSessionDataReceived;
        _session.WorkingDirectoryChanged += OnSessionWorkingDirectoryChanged;
        _session.CommandExecuted += OnSessionCommandExecuted;
        _session.CommandCompleted += OnSessionCommandCompleted;

        // Wire terminal user input -> PTY stdin
        TerminalModel.UserInput += OnTerminalUserInput;

        // Wire terminal resize -> PTY resize
        TerminalModel.SizeChanged += OnTerminalSizeChanged;

        UpdateBreadcrumbs();
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
            if (TerminalModel.Cols > 1 && TerminalModel.Rows > 1)
            {
                _session.Resize(TerminalModel.Cols, TerminalModel.Rows);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or System.IO.IOException or ObjectDisposedException or NotSupportedException)
        {
            TerminalModel.Feed($"{Loc["Terminal_Start_Failed"]}\r\n{ex.Message}\r\n");
            OnPropertyChanged(nameof(IsRunning));
        }
    }

    /// <summary>
    /// Sends raw input bytes directly to the active PTY shell session.
    /// </summary>
    public void SendInput(byte[] input)
    {
        var session = ActiveSession;
        if (session.IsRunning && input.Length > 0)
        {
            TrackInputBuffer(input);
            session.Send(input);
        }
    }

    internal void HandlePaneWorkingDirectoryChanged(TerminalPaneViewModel pane, string newDir)
    {
        if (_isDisposed || string.IsNullOrWhiteSpace(newDir)) return;

        if (ReferenceEquals(pane, ActivePane) || _panes.Count <= 1)
        {
            ApplyActiveWorkingDirectoryChanged(newDir);
        }
        else
        {
            _dispatcherService.RunOnUi(() =>
            {
                if (_isDisposed) return;
                _directoryHistoryService.RecordDirectory(newDir);
            });
        }
    }

    private void OnSessionWorkingDirectoryChanged(string newDir)
    {
        if (_isDisposed || string.IsNullOrWhiteSpace(newDir)) return;

        var initialPane = _initialPane;
        if (initialPane != null && !ReferenceEquals(initialPane, ActivePane))
        {
            _dispatcherService.RunOnUi(() =>
            {
                if (_isDisposed) return;
                _directoryHistoryService.RecordDirectory(newDir);
            });
            return;
        }

        ApplyActiveWorkingDirectoryChanged(newDir);
    }

    private void ApplyActiveWorkingDirectoryChanged(string newDir)
    {
        void UpdateDir()
        {
            WorkingDirectory = newDir;
            Title = newDir;

            _directoryHistoryService.RecordDirectory(newDir);

            SyncCommandHistoryFromPath();
            HistoryChanged?.Invoke(this);

            DirectoryChanged?.Invoke(this, newDir);
        }

        _dispatcherService.RunOnUi(UpdateDir);
    }

    private void DetachInitialSessionHandlers()
    {
        _session.DataReceived -= OnSessionDataReceived;
        _session.WorkingDirectoryChanged -= OnSessionWorkingDirectoryChanged;
        _session.CommandExecuted -= OnSessionCommandExecuted;
        _session.CommandCompleted -= OnSessionCommandCompleted;
        TerminalModel.UserInput -= OnTerminalUserInput;
        TerminalModel.SizeChanged -= OnTerminalSizeChanged;
        if (_terminalModelSearchStateChangedHandler != null)
        {
            TerminalModel.SearchStateChanged -= _terminalModelSearchStateChangedHandler;
        }
    }

    [RelayCommand]
    public void RequestClose() => CloseRequested?.Invoke(this);

    [RelayCommand]
    public void RequestUndock() => UndockRequested?.Invoke(this);

    /// <summary>
    /// Attempts to terminate all underlying shell sessions in this tab and reports whether all sessions stopped.
    /// </summary>
    internal bool TryStopSessions()
    {
        if (_panes != null)
        {
            foreach (var pane in _panes.ToList())
            {
                if (!ReferenceEquals(pane.Session, _session))
                {
                    pane.Session.Dispose();
                }
            }
        }
        _session.Dispose();
        return !IsRunning;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Loc.PropertyChanged -= OnLocalizationPropertyChanged;
        DetachInitialSessionHandlers();
        _pathCommandHistoryService.HistoryChangedForPath -= OnPathHistoryChanged;
        _directoryHistoryService.HistoryChanged -= OnSharedDirectoryHistoryChanged;

        StopActivityCooldownTimer();
        UiFeedCoordinator.Dispose();

        if (_panes != null)
        {
            foreach (var pane in _panes.ToList())
            {
                pane.Dispose();
            }
            _panes.Clear();
        }

        TerminalModel.Dispose();
        _session.Dispose();

        RequestFolderPickerAsync = null;
        CloseRequested = null;
        UndockRequested = null;
        DirectoryChanged = null;
        HistoryChanged = null;
        LayoutChanged = null;
        CommandExecutionCompleted = null;
        FocusRenameBoxRequested = null;
        FocusSearchBoxRequested = null;
        FocusTerminalRequested = null;

        ClearHistory();
    }

    /// <summary>
    /// Exports the current tab state as an immutable <see cref="TabState"/> snapshot for workspace persistence.
    /// </summary>
    public TabState ToTabState() => new(
        Title,
        WorkingDirectory,
        CommandHistory?.AsEnumerable().Reverse().ToList() ?? new List<string>(),
        DirectoryHistory?.AsEnumerable().Reverse().ToList() ?? new List<string>(),
        ShellType,
        CustomTitle,
        TabColor);
}
