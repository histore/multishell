using System.Collections.Generic;
using MultiShell.Services;

namespace MultiShell.Models;

/// <summary>
/// State of a single terminal tab for persistence.
/// </summary>
public record TabState(
    string Title,
    string? WorkingDirectory,
    List<string>? CommandHistory = null,
    List<string>? DirectoryHistory = null,
    ShellType ShellType = ShellType.PowerShell);

/// <summary>
/// Overall persisted workspace state containing all tabs and configuration preferences.
/// </summary>
public record WorkspaceState(
    List<TabState> Tabs,
    int SelectedIndex,
    string? SavedLanguage = null,
    int AppFontSizeLevel = 3,
    int TerminalFontSizeLevel = 3,
    ShellType DefaultShellType = ShellType.PowerShell);