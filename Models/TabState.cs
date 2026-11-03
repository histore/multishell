using System.Collections.Generic;

namespace MultiShell.Models;

/// <summary>
/// State of a single terminal tab for persistence.
/// </summary>
public record TabState(
    string Title,
    string? WorkingDirectory,
    List<string>? CommandHistory = null,
    List<string>? DirectoryHistory = null,
    ShellType ShellType = ShellType.PowerShell,
    string? CustomTitle = null,
    string? TabColor = null);

/// <summary>
/// Persisted window bounds and layout coordinates for multi-window restoration.
/// </summary>
public record WindowBoundsState(
    int? X = null,
    int? Y = null,
    double? Width = null,
    double? Height = null,
    bool IsMaximized = false);

/// <summary>
/// Persisted state of a single window instance (primary or satellite) per REQ-TAB-026.
/// </summary>
public record WindowWorkspaceState(
    int WindowNumber,
    List<TabState> Tabs,
    int SelectedIndex,
    WindowBoundsState? Bounds = null);

/// <summary>
/// Overall persisted workspace state containing all tabs and configuration preferences.
/// </summary>
public record WorkspaceState(
    List<TabState> Tabs,
    int SelectedIndex,
    string? SavedLanguage = null,
    int AppFontSizeLevel = 4,
    int TerminalFontSizeLevel = 4,
    ShellType DefaultShellType = ShellType.PowerShell,
    List<TabState>? ClosedTabs = null,
    Dictionary<string, List<string>>? PathCommandHistory = null,
    List<string>? SharedDirectoryHistory = null,
    bool? IsDarkAppTheme = null,
    bool? IsDarkTerminalTheme = null,
    int Version = 2,
    WindowWorkspaceState? PrimaryWindow = null,
    List<WindowWorkspaceState>? SecondaryWindows = null);