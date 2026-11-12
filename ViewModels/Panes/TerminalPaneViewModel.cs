using System;
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
/// ViewModel representing an individual terminal viewport (pane) backed by its own shell session and TerminalControl (REQ-UI-006).
/// Multiple panes can be hosted inside a single TerminalTabViewModel via horizontal and vertical splits.
/// </summary>
public partial class TerminalPaneViewModel : ViewModelBase, IDisposable
{
    private static readonly IBrush DarkTerminalBackground = new SolidColorBrush(Color.Parse("#0E0F15"));
    private static readonly IBrush LightTerminalBackground = new SolidColorBrush(Color.Parse("#F8F9FC"));
    private static readonly IBrush DarkTerminalCaret = new SolidColorBrush(Color.Parse("#7AA2F7"));
    private static readonly IBrush LightTerminalCaret = new SolidColorBrush(Color.Parse("#2563EB"));
    private static readonly IBrush ActiveBorderAccentBrush = new SolidColorBrush(Color.Parse("#3B82F6"));
    private static readonly IBrush InactiveBorderSubtleBrush = new SolidColorBrush(Color.Parse("#252836"));

    private readonly AnsiStreamProcessor _ansiStreamProcessor = new();
    private readonly StringBuilder _pendingUiFeedBuffer = new();
    private readonly Lock _pendingUiFeedLock = new();
    private readonly Lock _silenceTimerLock = new();
    private readonly EventHandler<Avalonia.AvaloniaPropertyChangedEventArgs>? _terminalModelPropertyChangedHandler;
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
    /// Gets the native terminal control model for ANSI/VT100 rendering and text selection.
    /// </summary>
    public TerminalControlModel TerminalModel { get; }

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
    private IBrush _terminalBackgroundBrush = DarkTerminalBackground;

    [ObservableProperty]
    private IBrush _terminalCaretBrush = DarkTerminalCaret;

    [ObservableProperty]
    private double _terminalFontSize = 14.0;

    [ObservableProperty]
    private FontFamily _terminalFontFamily = new("avares://MultiShell/Assets/Fonts#FiraCode Nerd Font Mono, Cascadia Code NF, CascadiaMono NF, CaskaydiaMono Nerd Font, Cascadia Mono, Cascadia Code, Consolas, Segoe UI Symbol, Segoe UI Emoji, DejaVu Sans Mono, monospace");

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

    public IBrush BorderBrush => IsActive ? ((Tab.HasTabColor && Tab.TabColorBrush != null) ? Tab.TabColorBrush : ActiveBorderAccentBrush) : InactiveBorderSubtleBrush;
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
        FontFamily? fontFamily = null,
        TerminalControlModel? terminalModel = null)
    {
        Tab = tab ?? throw new ArgumentNullException(nameof(tab));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _workingDirectory = session.WorkingDirectory;
        _title = !string.IsNullOrWhiteSpace(_workingDirectory) ? _workingDirectory : session.Title;
        _isDarkTerminalTheme = isDarkTheme;
        _terminalFontSize = fontSize;
        if (fontFamily != null) _terminalFontFamily = fontFamily;

        UpdateTheme(isDarkTheme);

        TerminalModel = terminalModel ?? new TerminalControlModel(new TerminalOptions
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
                if (TerminalModel.Terminal.Cols > 0 && TerminalModel.Terminal.Rows > 0)
                {
                    Session.Resize(TerminalModel.Terminal.Cols, TerminalModel.Terminal.Rows);
                }
            }
            catch
            {
                // ConPTY startup handled gracefully
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
            catch
            {
                // Ignore send errors during shutdown
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
            catch
            {
                // Ignore resize failures on closed session
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
        if (TerminalModel.Terminal.Engine.BracketedPasteMode)
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
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Safe reflection on terminal engine internal mouse tracker with null-safety.")]
    public void SuppressMouseTrackingMode()
    {
        try
        {
            // Reset DEC private modes for mouse tracking via standard VT sequences (1000, 1002, 1003, 1006)
            TerminalModel.Terminal.Feed("\x1b[?1000l\x1b[?1002l\x1b[?1003l\x1b[?1006l");
        }
        catch { }

        try
        {
            var engine = TerminalModel.Terminal.Engine;
            var trackerField = engine.GetType().GetField("_mouseTracker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (trackerField?.GetValue(engine) is object tracker)
            {
                var prop = tracker.GetType().GetProperty("TrackingMode");
                prop?.SetValue(tracker, 0); // MouseTrackingMode.None
            }
        }
        catch { }
    }

    private void OnTerminalUserInput(object? sender, TerminalUserInputEventArgs e)
    {
        if (!Session.IsRunning || e.Data.IsEmpty) return;

        var bytes = e.Data.ToArray();

        // When AltGr (Ctrl+Alt) is active on international keyboards (e.g. AltGr+Q for '@', AltGr+E for '€'),
        // SvcSystems.UI.Terminal misidentifies the key as Ctrl+Q (0x11) / Ctrl+E (0x05) in OnKeyDown.
        // We filter out these single control bytes so only the valid TextInput character (@, €, etc.) is sent.
        if (Tab.IsAltGrActive && bytes.Length == 1 && bytes[0] < 32 && bytes[0] != 9 && bytes[0] != 10 && bytes[0] != 13)
        {
            return;
        }

        SendInput(bytes);
    }

    private void OnTerminalSizeChanged(object? sender, TerminalSizeChangedEventArgs e)
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

        if (_terminalModelPropertyChangedHandler != null)
        {
            TerminalModel.PropertyChanged -= _terminalModelPropertyChangedHandler;
        }

        if (!ReferenceEquals(Tab.TerminalModel, TerminalModel))
        {
            TerminalModel.SizeChanged -= OnTerminalSizeChanged;
            TerminalModel.UserInput -= OnTerminalUserInput;
            Session.DataReceived -= OnSessionDataReceived;
        }

        Session.Exited -= OnSessionExited;
        Session.WorkingDirectoryChanged -= OnSessionWorkingDirectoryChanged;
        Session.CommandExecuted -= OnSessionCommandExecuted;
        Session.CommandCompleted -= OnSessionCommandCompleted;

        Session.Dispose();
    }
}
