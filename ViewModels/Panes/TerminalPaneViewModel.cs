using System;
using System.Text;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Models;
using MultiShell.Services;

namespace MultiShell.ViewModels;

/// <summary>
/// ViewModel representing an individual terminal viewport (pane) backed by its own shell session and terminal model adapter (REQ-UI-006).
/// Multiple panes can be hosted inside a single TerminalTabViewModel via horizontal and vertical splits.
/// </summary>
public partial class TerminalPaneViewModel : ViewModelBase, IDisposable
{
    private const string DarkTerminalBackground = "#0E0F15";
    private const string LightTerminalBackground = "#F8F9FC";
    private const string DarkTerminalCaret = "#7AA2F7";
    private const string LightTerminalCaret = "#2563EB";
    private const string ActiveBorderAccentBrush = "#3B82F6";
    private const string InactiveBorderSubtleBrush = "#252836";

    private readonly AnsiStreamProcessor _ansiStreamProcessor = new();
    private readonly StringBuilder _pendingUiFeedBuffer = new();
    private readonly Lock _pendingUiFeedLock = new();
    private readonly Lock _silenceTimerLock = new();
    private readonly IDispatcherService _dispatcherService;
    private readonly EventHandler? _terminalModelSearchStateChangedHandler;
    private Timer? _silenceTimer;
    private DateTime? _commandStartTime;
    private bool _isUiFeedScheduled;
    private bool _isDisposed;

    /// <summary>
    /// Gets the unique identifier for this terminal pane.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the parent tab hosting this pane.
    /// </summary>
    public TerminalTabViewModel Tab { get; }

    /// <summary>
    /// Gets the underlying ConPTY shell session.
    /// </summary>
    public IShellSession Session { get; }

    /// <summary>
    /// Gets the abstracted terminal model adapter for ANSI/VT100 rendering and text selection.
    /// </summary>
    public ITerminalModelAdapter TerminalModel { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    private string _title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    private string? _workingDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BorderBrush))]
    [NotifyPropertyChangedFor(nameof(BorderThickness))]
    private bool _isActive;

    [ObservableProperty]
    private bool _isDarkTerminalTheme = true;

    [ObservableProperty]
    private string _terminalBackgroundBrush = DarkTerminalBackground;

    [ObservableProperty]
    private string _terminalCaretBrush = DarkTerminalCaret;

    [ObservableProperty]
    private double _terminalFontSize = 14.0;

    [ObservableProperty]
    private string _terminalFontFamily = "avares://MultiShell/Assets/Fonts#FiraCode Nerd Font Mono, Cascadia Code NF, CascadiaMono NF, CaskaydiaMono Nerd Font, Cascadia Mono, Cascadia Code, Consolas, Segoe UI Symbol, Segoe UI Emoji, DejaVu Sans Mono, monospace";

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string? _executionStatusBadge;

    [ObservableProperty]
    private string? _floatingHudBadge;

    [ObservableProperty]
    private bool _isSearchOpen;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private int _searchResultCount;

    [ObservableProperty]
    private int _currentSearchResultIndex;

    [ObservableProperty]
    private string _searchMatchSummary = string.Empty;

    public bool IsRunning => Session.IsRunning;
    public ShellType ShellType => Session.ShellType;

    public string DisplayTitle => !string.IsNullOrWhiteSpace(WorkingDirectory)
        ? TerminalTabViewModel.FormatMiddleEllipsis(WorkingDirectory, 24)
        : (!string.IsNullOrWhiteSpace(Title) ? Title : Session.Title);

    public string BorderBrush => IsActive ? ((Tab.HasTabColor && Tab.TabColorBrush != null) ? Tab.TabColorBrush : ActiveBorderAccentBrush) : InactiveBorderSubtleBrush;
    public double BorderThickness => IsActive ? 1.5 : 1.0;

    public event Action? FocusSearchBoxRequested;
    public event Action? FocusTerminalRequested;
    public event Action<TerminalPaneViewModel>? CloseRequested;
    public event Action<TerminalPaneViewModel>? Activated;
    public event Action<TerminalPaneViewModel, string>? DirectoryChanged;

    public TerminalPaneViewModel(
        TerminalTabViewModel tab,
        IShellSession session,
        bool isDarkTheme = true,
        double fontSize = 12.0,
        string? fontFamily = null,
        ITerminalModelAdapter? terminalModel = null,
        IDispatcherService? dispatcherService = null,
        ITerminalModelFactory? terminalModelFactory = null)
    {
        Tab = tab ?? throw new ArgumentNullException(nameof(tab));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _dispatcherService = dispatcherService ?? AvaloniaDispatcherService.Instance;
        var modelFactory = terminalModelFactory ?? AvaloniaTerminalModelFactory.Instance;
        _workingDirectory = session.WorkingDirectory;
        _title = !string.IsNullOrWhiteSpace(_workingDirectory) ? _workingDirectory : session.Title;
        _isDarkTerminalTheme = isDarkTheme;
        _terminalFontSize = fontSize;
        if (!string.IsNullOrWhiteSpace(fontFamily)) _terminalFontFamily = fontFamily;

        UpdateTheme(isDarkTheme);

        TerminalModel = terminalModel ?? modelFactory.Create();

        _terminalModelSearchStateChangedHandler = (_, _) =>
        {
            SearchResultCount = TerminalModel.SearchResultCount;
            CurrentSearchResultIndex = TerminalModel.CurrentSearchResultIndex;
            UpdateSearchMatchSummary();
        };
        TerminalModel.SearchStateChanged += _terminalModelSearchStateChangedHandler;
        if (!ReferenceEquals(tab.TerminalModel, TerminalModel))
        {
            TerminalModel.SizeChanged += OnTerminalSizeChanged;
            TerminalModel.UserInput += OnTerminalUserInput;
            Session.DataReceived += OnSessionDataReceived;
        }

        Session.Exited += OnSessionExited;
        Session.WorkingDirectoryChanged += OnSessionWorkingDirectoryChanged;
        Session.CommandExecuted += OnSessionCommandExecuted;
        Session.CommandCompleted += OnSessionCommandCompleted;
    }

    public void StartSession()
    {
        if (!Session.IsRunning)
        {
            try
            {
                Session.Start();
                if (TerminalModel.Cols > 0 && TerminalModel.Rows > 0)
                {
                    Session.Resize(TerminalModel.Cols, TerminalModel.Rows);
                }
            }
            catch (Exception ex) when (ex is System.IO.IOException or System.ComponentModel.Win32Exception or InvalidOperationException or ObjectDisposedException)
            {
                // ConPTY startup handled gracefully
                System.Diagnostics.Debug.WriteLine($"[TerminalPaneViewModel.StartSession] ConPTY startup error: {ex.Message}");
            }
        }
    }

    public void SendInput(byte[] input)
    {
        if (Session.IsRunning)
        {
            try
            {
                Session.Send(input);
            }
            catch (Exception ex) when (ex is System.IO.IOException or ObjectDisposedException or InvalidOperationException)
            {
                // Ignore send errors during shutdown
                System.Diagnostics.Debug.WriteLine($"[TerminalPaneViewModel.SendInput] Send error ignored: {ex.Message}");
            }
        }
    }

    public void Resize(int cols, int rows)
    {
        if (Session.IsRunning && cols > 0 && rows > 0)
        {
            try
            {
                Session.Resize(cols, rows);
            }
            catch (Exception ex) when (ex is System.IO.IOException or System.ComponentModel.Win32Exception or ObjectDisposedException or InvalidOperationException)
            {
                // Ignore resize failures on closed session
                System.Diagnostics.Debug.WriteLine($"[TerminalPaneViewModel.Resize] Resize error ignored: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Pastes text into the terminal session, wrapping in Bracketed Paste Mode (VT 2004h)
    /// if requested by the active shell application (e.g. Copilot CLI, Readline, Vim, Node.js).
    /// </summary>
    public void PasteText(string text)
    {
        if (!Session.IsRunning || string.IsNullOrEmpty(text)) return;

        // In Bracketed Paste Mode (VT 2004h), wrap pasted text with ESC [ 200 ~ and ESC [ 201 ~.
        // This informs the interactive CLI / TUI that the input is a single pasted payload
        // and prevents characters from being dropped or prematurely submitted.
        if (TerminalModel.BracketedPasteMode)
        {
            var bracketed = $"\x1b[200~{text}\x1b[201~";
            SendInput(Encoding.UTF8.GetBytes(bracketed));
        }
        else
        {
            SendInput(Encoding.UTF8.GetBytes(text));
        }
    }

    /// <summary>
    /// Suppresses VT mouse tracking mode so that terminal pointer interactions (click, double-click, drag)
    /// always perform native text selection instead of sending raw mouse reporting escape sequences.
    /// </summary>
    public void SuppressMouseTrackingMode() => TerminalModel.SuppressMouseTracking();

    private void OnTerminalUserInput(object? sender, ReadOnlyMemory<byte> data)
    {
        if (!Session.IsRunning || data.IsEmpty) return;

        var bytes = data.ToArray();

        // When AltGr (Ctrl+Alt) is active on international keyboards (e.g. AltGr+Q for '@', AltGr+E for '€'),
        // SvcSystems.UI.Terminal misidentifies the key as Ctrl+Q (0x11) / Ctrl+E (0x05) in OnKeyDown.
        // We filter out these single control bytes so only the valid TextInput character (@, €, etc.) is sent.
        if (Tab.IsAltGrActive && bytes.Length == 1 && bytes[0] < 32 && bytes[0] != 9 && bytes[0] != 10 && bytes[0] != 13)
        {
            return;
        }

        SendInput(bytes);
    }

    private void OnTerminalSizeChanged(object? sender, (int Cols, int Rows) e)
    {
        if (e.Cols > 0 && e.Rows > 0)
        {
            Resize(e.Cols, e.Rows);
        }
    }

    public void Focus()
    {
        Tab.SetActivePane(this);
        Activated?.Invoke(this);
        FocusTerminalRequested?.Invoke();
    }

    public void PageUp()
    {
        TerminalModel.PageUp();
    }

    public void PageDown()
    {
        TerminalModel.PageDown();
    }

    public void SendPageUp(bool alt = false)
    {
        SendInput(alt ? "\u001b[5;3~"u8.ToArray() : "\u001b[5~"u8.ToArray());
    }

    public void SendPageDown(bool alt = false)
    {
        SendInput(alt ? "\u001b[6;3~"u8.ToArray() : "\u001b[6~"u8.ToArray());
    }

    public void ClearBuffer()
    {
        const string clearSequence = "\x1b[2J\x1b[3J\x1b[H";
        RunOnUi(() =>
        {
            TerminalModel.Feed(clearSequence);
            TerminalModel.ClearSelection();
        });
    }

    public void UpdateTheme(bool isDark)
    {
        IsDarkTerminalTheme = isDark;
        TerminalBackgroundBrush = isDark ? DarkTerminalBackground : LightTerminalBackground;
        TerminalCaretBrush = isDark ? DarkTerminalCaret : LightTerminalCaret;
        OnPropertyChanged(nameof(BorderBrush));
    }

    public void UpdateFontSize(double fontSize)
    {
        TerminalFontSize = fontSize;
    }

    // --- Split and Pane Control Commands ---

    [RelayCommand]
    public void SplitVertical() => Tab.SplitPane(this, PaneSplitDirection.Vertical);

    [RelayCommand]
    public void SplitHorizontal() => Tab.SplitPane(this, PaneSplitDirection.Horizontal);

    [RelayCommand]
    public void Close() => Tab.ClosePane(this);

    [RelayCommand]
    public void ToggleZoom() => Tab.ToggleZoom();

    [RelayCommand]
    public void RequestFocus() => Focus();

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        lock (_silenceTimerLock)
        {
            _silenceTimer?.Dispose();
            _silenceTimer = null;
        }

        lock (_pendingUiFeedLock)
        {
            _pendingUiFeedBuffer.Clear();
            _pendingUiFeedBuffer.Capacity = 0;
            _isUiFeedScheduled = false;
        }

        if (_terminalModelSearchStateChangedHandler != null)
        {
            TerminalModel.SearchStateChanged -= _terminalModelSearchStateChangedHandler;
        }

        if (!ReferenceEquals(Tab.TerminalModel, TerminalModel))
        {
            TerminalModel.SizeChanged -= OnTerminalSizeChanged;
            TerminalModel.UserInput -= OnTerminalUserInput;
            Session.DataReceived -= OnSessionDataReceived;
            TerminalModel.Dispose();
        }

        Session.Exited -= OnSessionExited;
        Session.WorkingDirectoryChanged -= OnSessionWorkingDirectoryChanged;
        Session.CommandExecuted -= OnSessionCommandExecuted;
        Session.CommandCompleted -= OnSessionCommandCompleted;

        Session.Dispose();
    }
}
