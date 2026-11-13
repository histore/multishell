using System;
using Avalonia;
using SvcSystems.UI.Terminal;

namespace MultiShell.Services;

/// <summary>
/// Avalonia/SvcSystems.UI.Terminal implementation of <see cref="ITerminalModelAdapter"/>.
/// </summary>
public sealed class AvaloniaTerminalModelAdapter : ITerminalModelAdapter
{
    private readonly EventHandler<AvaloniaPropertyChangedEventArgs> _propertyChangedHandler;
    private readonly EventHandler<TerminalUserInputEventArgs> _userInputHandler;
    private readonly EventHandler<TerminalSizeChangedEventArgs> _sizeChangedHandler;
    private bool _isDisposed;

    /// <summary>
    /// Gets the wrapped <see cref="TerminalControlModel"/>.
    /// </summary>
    public TerminalControlModel Model { get; }

    /// <inheritdoc />
    public object NativeModel => Model;

    /// <summary>
    /// Initializes a new adapter wrapping a fresh <see cref="TerminalControlModel"/>.
    /// </summary>
    public AvaloniaTerminalModelAdapter()
        : this(new TerminalControlModel(new TerminalOptions { ReflowOnResize = false }))
    {
    }

    /// <summary>
    /// Initializes a new adapter wrapping an existing <see cref="TerminalControlModel"/>.
    /// </summary>
    public AvaloniaTerminalModelAdapter(TerminalControlModel model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));

        _propertyChangedHandler = (_, e) =>
        {
            if (e.Property.Name == nameof(TerminalControlModel.SearchResultCount) ||
                e.Property.Name == nameof(TerminalControlModel.CurrentSearchResultIndex))
            {
                SearchStateChanged?.Invoke(this, EventArgs.Empty);
            }
        };

        _userInputHandler = (_, e) => UserInput?.Invoke(this, e.Data);
        _sizeChangedHandler = (_, e) => SizeChanged?.Invoke(this, (e.Cols, e.Rows));

        Model.PropertyChanged += _propertyChangedHandler;
        Model.UserInput += _userInputHandler;
        Model.SizeChanged += _sizeChangedHandler;
    }

    /// <inheritdoc />
    public int Cols => Model.Terminal.Cols;

    /// <inheritdoc />
    public int Rows => Model.Terminal.Rows;

    /// <inheritdoc />
    public bool IsAlternateBufferActive => Model.Terminal.IsAlternateBufferActive;

    /// <inheritdoc />
    public bool BracketedPasteMode => Model.Terminal.Engine.BracketedPasteMode;

    /// <inheritdoc />
    public bool HasSelection => Model.HasSelection;

    /// <inheritdoc />
    public string SelectedText => Model.SelectedText;

    /// <inheritdoc />
    public int SearchResultCount => Model.SearchResultCount;

    /// <inheritdoc />
    public int CurrentSearchResultIndex => Model.CurrentSearchResultIndex;

    /// <inheritdoc />
    public Action? UpdateUI
    {
        get => Model.UpdateUI;
        set => Model.UpdateUI = value;
    }

    /// <inheritdoc />
    public event EventHandler<ReadOnlyMemory<byte>>? UserInput;

    /// <inheritdoc />
    public event EventHandler<(int Cols, int Rows)>? SizeChanged;

    /// <inheritdoc />
    public event EventHandler? SearchStateChanged;

    /// <inheritdoc />
    public void Feed(string text) => Model.Feed(text);

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Safe reflection on terminal engine internal mouse tracker with null-safety.")]
    public void SuppressMouseTracking()
    {
        try
        {
            // Reset DEC private modes for mouse tracking via standard VT sequences (1000, 1002, 1003, 1006)
            Model.Terminal.Feed("\x1b[?1000l\x1b[?1002l\x1b[?1003l\x1b[?1006l");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SuppressMouseTracking feed failed: {ex.Message}");
        }

        try
        {
            var engine = Model.Terminal.Engine;
            var trackerField = engine.GetType().GetField("_mouseTracker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (trackerField?.GetValue(engine) is object tracker)
            {
                var prop = tracker.GetType().GetProperty("TrackingMode");
                prop?.SetValue(tracker, 0); // MouseTrackingMode.None
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SuppressMouseTracking reflection failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void ClearSelection() => Model.ClearSelection();

    /// <inheritdoc />
    public void PageUp() => Model.PageUp();

    /// <inheritdoc />
    public void PageDown() => Model.PageDown();

    /// <inheritdoc />
    public void Search(string query) => Model.Search(query);

    /// <inheritdoc />
    public void SelectNextSearchResult() => Model.SelectNextSearchResult();

    /// <inheritdoc />
    public void SelectPreviousSearchResult() => Model.SelectPreviousSearchResult();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Model.PropertyChanged -= _propertyChangedHandler;
        Model.UserInput -= _userInputHandler;
        Model.SizeChanged -= _sizeChangedHandler;
        Model.UpdateUI = null;
    }
}

/// <summary>
/// Default <see cref="ITerminalModelFactory"/> creating <see cref="AvaloniaTerminalModelAdapter"/> instances.
/// </summary>
public sealed class AvaloniaTerminalModelFactory : ITerminalModelFactory
{
    /// <summary>
    /// Shared singleton instance for default ViewModel construction.
    /// </summary>
    public static AvaloniaTerminalModelFactory Instance { get; } = new();

    /// <inheritdoc />
    public ITerminalModelAdapter Create() => new AvaloniaTerminalModelAdapter();
}
