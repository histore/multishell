using System;

namespace MultiShell.Services;

/// <summary>
/// Service managing multi-level font sizes for App UI and Terminal.
/// Level 4 corresponds to the system default standard size.
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
    const int MaxLevel = 7;

    /// <summary>
    /// Default standard font size level (middle of 1 to 7).
    /// </summary>
    const int DefaultLevel = 4;

    /// <summary>
    /// Gets or sets the current App UI font size level (1 to 7).
    /// </summary>
    int AppFontSizeLevel { get; set; }

    /// <summary>
    /// Gets or sets the current Terminal font size level (1 to 7).
    /// </summary>
    int TerminalFontSizeLevel { get; set; }

    /// <summary>
    /// Gets the current App UI scaling factor based on the active level (e.g. 1.0 for Level 4).
    /// </summary>
    double AppFontScale { get; }

    /// <summary>
    /// Gets the current Terminal font size in points based on the active level (e.g. 14.0 for Level 4).
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
    /// <param name="level">Level between 1 and 7.</param>
    /// <returns>Scaling factor (e.g. 0.80, 0.88, 0.94, 1.00, 1.08, 1.18, 1.30).</returns>
    double GetAppFontScale(int level);

    /// <summary>
    /// Calculates the Terminal font size in points for the given level.
    /// </summary>
    /// <param name="level">Level between 1 and 7.</param>
    /// <returns>Font size in points (e.g. 10.0, 11.0, 12.5, 14.0, 16.0, 18.5, 21.0).</returns>
    double GetTerminalFontSize(int level);

    /// <summary>
    /// Sets the App font size level with clamping to [1, 7].
    /// </summary>
    void SetAppFontSizeLevel(int level);

    /// <summary>
    /// Sets the Terminal font size level with clamping to [1, 7].
    /// </summary>
    void SetTerminalFontSizeLevel(int level);

    /// <summary>
    /// Resets both App UI and Terminal font size levels to DefaultLevel (4).
    /// </summary>
    void ResetLevels();

    /// <summary>
    /// Resets the App UI font size level to DefaultLevel (4).
    /// </summary>
    void ResetAppFontSizeLevel();

    /// <summary>
    /// Resets the Terminal font size level to DefaultLevel (4).
    /// </summary>
    void ResetTerminalFontSizeLevel();
}
