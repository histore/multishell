using System;

namespace MultiShell.ViewModels;

/// <summary>
/// Specifies the type of an entry in the global history search feed.
/// </summary>
public enum GlobalHistoryItemType
{
    Command,
    Directory
}

/// <summary>
/// Represents a unified history entry (either a shell command or a visited directory)
/// for global fuzzy search across all workspace tabs and projects.
/// </summary>
public record GlobalHistoryItem(string Text, GlobalHistoryItemType Type)
{
    public bool IsCommand => Type == GlobalHistoryItemType.Command;
    public bool IsDirectory => Type == GlobalHistoryItemType.Directory;
    public string BadgeText => IsCommand ? "CMD" : "DIR";
    public string IconText => IsCommand ? "> " : "📁 ";
}
