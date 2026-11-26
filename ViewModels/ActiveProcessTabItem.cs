namespace MultiShell.ViewModels;

/// <summary>
/// Represents a tab (and its hosting window) that currently has an active running process
/// for display in the main window close confirmation dialog (REQ-UI-011).
/// </summary>
public sealed class ActiveProcessTabItem
{
    public MainViewModel OwnerWindow { get; }
    public TerminalTabViewModel Tab { get; }
    public string ShellIconTag => Tab.ShellIconTag;
    public string DisplayTitle => Tab.DisplayTitle;
    public string CommandSummary { get; }
    public bool HasCommandSummary => !string.IsNullOrWhiteSpace(CommandSummary);
    public string WindowBadge { get; }
    public bool HasWindowBadge => !string.IsNullOrWhiteSpace(WindowBadge);

    public ActiveProcessTabItem(
        MainViewModel ownerWindow,
        TerminalTabViewModel tab,
        string commandSummary,
        string windowBadge = "")
    {
        OwnerWindow = ownerWindow;
        Tab = tab;
        CommandSummary = commandSummary;
        WindowBadge = windowBadge;
    }
}
