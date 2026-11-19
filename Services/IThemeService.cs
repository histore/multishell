using System;

namespace MultiShell.Services;

/// <summary>
/// Service contract for managing independent application UI and terminal shell themes (Dark / Light).
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Raised when the application UI theme changes (parameter is <c>true</c> for dark, <c>false</c> for light).
    /// </summary>
    event Action<bool>? AppThemeChanged;

    /// <summary>
    /// Raised when the terminal shell theme changes (parameter is <c>true</c> for dark, <c>false</c> for light).
    /// </summary>
    event Action<bool>? TerminalThemeChanged;
    /// <summary>
    /// Gets a value indicating whether the application UI theme is dark.
    /// </summary>
    bool IsDarkAppTheme { get; }

    /// <summary>
    /// Gets a value indicating whether the terminal shell theme is dark.
    /// </summary>
    bool IsDarkTerminalTheme { get; }

    /// <summary>
    /// Sets the application UI theme variant.
    /// </summary>
    /// <param name="isDark">True for dark UI, false for light UI.</param>
    void SetAppTheme(bool isDark);

    /// <summary>
    /// Sets the terminal shell theme variant.
    /// </summary>
    /// <param name="isDark">True for dark terminal, false for light terminal.</param>
    void SetTerminalTheme(bool isDark);

    /// <summary>
    /// Toggles the application UI theme between dark and light.
    /// </summary>
    void ToggleAppTheme();

    /// <summary>
    /// Toggles the terminal shell theme between dark and light.
    /// </summary>
    void ToggleTerminalTheme();
}
