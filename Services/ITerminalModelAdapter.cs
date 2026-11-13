using System;

namespace MultiShell.Services;

/// <summary>
/// Abstraction over the terminal control model and VT buffer,
/// keeping ViewModels decoupled from <c>SvcSystems.UI.Terminal</c> and Avalonia types.
/// </summary>
public interface ITerminalModelAdapter : IDisposable
{
    /// <summary>
    /// Gets the underlying native terminal model instance for View-layer control binding.
    /// </summary>
    object NativeModel { get; }

    /// <summary>
    /// Gets the current terminal column count.
    /// </summary>
    int Cols { get; }

    /// <summary>
    /// Gets the current terminal row count.
    /// </summary>
    int Rows { get; }

    /// <summary>
    /// Gets whether the terminal is currently in the alternate screen buffer (e.g. vim, less, htop).
    /// </summary>
    bool IsAlternateBufferActive { get; }

    /// <summary>
    /// Gets whether VT bracketed paste mode (2004h) is currently enabled.
    /// </summary>
    bool BracketedPasteMode { get; }

    /// <summary>
    /// Gets whether the terminal currently has an active text selection.
    /// </summary>
    bool HasSelection { get; }

    /// <summary>
    /// Gets the currently selected text in the terminal buffer.
    /// </summary>
    string SelectedText { get; }

    /// <summary>
    /// Gets the number of matches found for the active search query.
    /// </summary>
    int SearchResultCount { get; }

    /// <summary>
    /// Gets the 0-based index of the currently highlighted search match.
    /// </summary>
    int CurrentSearchResultIndex { get; }

    /// <summary>
    /// Gets or sets the UI invalidation callback invoked by the terminal control.
    /// </summary>
    Action? UpdateUI { get; set; }

    /// <summary>
    /// Raised when the user inputs raw bytes into the terminal control.
    /// </summary>
    event EventHandler<ReadOnlyMemory<byte>>? UserInput;

    /// <summary>
    /// Raised when the terminal viewport dimensions (columns or rows) change.
    /// </summary>
    event EventHandler<(int Cols, int Rows)>? SizeChanged;

    /// <summary>
    /// Raised when search result count or active match index changes.
    /// </summary>
    event EventHandler? SearchStateChanged;

    /// <summary>
    /// Feeds processed VT/ANSI text into the terminal control model.
    /// </summary>
    void Feed(string text);

    /// <summary>
    /// Suppresses VT mouse tracking mode so pointer interactions perform native text selection.
    /// </summary>
    void SuppressMouseTracking();

    /// <summary>
    /// Clears any active text selection in the terminal buffer.
    /// </summary>
    void ClearSelection();

    /// <summary>
    /// Scrolls the terminal viewport up by one page.
    /// </summary>
    void PageUp();

    /// <summary>
    /// Scrolls the terminal viewport down by one page.
    /// </summary>
    void PageDown();

    /// <summary>
    /// Executes an in-terminal scrollback search for the specified query.
    /// </summary>
    void Search(string query);

    /// <summary>
    /// Advances to the next search match in the scrollback buffer.
    /// </summary>
    void SelectNextSearchResult();

    /// <summary>
    /// Moves to the previous search match in the scrollback buffer.
    /// </summary>
    void SelectPreviousSearchResult();
}

/// <summary>
/// Factory for creating <see cref="ITerminalModelAdapter"/> instances without exposing concrete UI types to ViewModels.
/// </summary>
public interface ITerminalModelFactory
{
    /// <summary>
    /// Creates a new <see cref="ITerminalModelAdapter"/> instance.
    /// </summary>
    ITerminalModelAdapter Create();
}
