using System;

namespace MultiShell.Services;

/// <summary>
/// Service managing multi-level font sizes for App UI and Terminal.
/// Level 3 corresponds to the system default standard size.
/// </summary>
public interface IFontSizeService
{
    /// <summary>
    /// Minimum allowed font size level.
    /// </summary>
    const int MinLevel = 1;

    /// <summary>
    /// Maximum allowed font size level.
    /// </summary>
    const int MaxLevel = 5;

    /// <summary>
    /// Default standard font size level.
    /// </summary>
    const int DefaultLevel = 3;

    /// <summary>
    /// Gets or sets the current App UI font size level (1 to 5).
    /// </summary>
    int AppFontSizeLevel { get; set; }

    /// <summary>
    /// Gets or sets the current Terminal font size level (1 to 5).
    /// </summary>
    int TerminalFontSizeLevel { get; set; }

    /// <summary>
    /// Gets the current App UI scaling factor based on the active level (e.g. 1.0 for Level 3).
    /// </summary>
    double AppFontScale { get; }

    /// <summary>
    /// Gets the current Terminal font size in points based on the active level (e.g. 12.0 for Level 3).
    /// </summary>
    double TerminalFontSize { get; }

    /// <summary>
    /// Event raised when the App UI font size level changes.
    /// </summary>
    event Action<int>? AppFontSizeLevelChanged;

    /// <summary>
    /// Event raised when the Terminal font size level changes.
    /// </summary>
    event Action<int>? TerminalFontSizeLevelChanged;

    /// <summary>
    /// Calculates the App UI scaling factor for the given level.
    /// </summary>
    /// <param name="level">Level between 1 and 5.</param>
    /// <returns>Scaling factor (e.g. 0.85, 0.92, 1.00, 1.12, 1.25).</returns>
    double GetAppFontScale(int level);

    /// <summary>
    /// Calculates the Terminal font size in points for the given level.
    /// </summary>
    /// <param name="level">Level between 1 and 5.</param>
    /// <returns>Font size in points (e.g. 9.5, 10.5, 12.0, 14.0, 16.5).</returns>
    double GetTerminalFontSize(int level);

    /// <summary>
    /// Sets the App font size level with clamping to [1, 5].
    /// </summary>
    void SetAppFontSizeLevel(int level);

    /// <summary>
    /// Sets the Terminal font size level with clamping to [1, 5].
    /// </summary>
    void SetTerminalFontSizeLevel(int level);
}
