using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using MultiShell.Models;
using MultiShell.Services;

namespace MultiShell.ViewModels;

/// <summary>
/// Observable ViewModel representing a previously closed terminal tab.
/// Encapsulates tab state and history for 1-click restoration or removal.
/// </summary>
public partial class ClosedTabItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _workingDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShellIconTag))]
    private ShellType _shellType = ShellType.PowerShell;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedClosedTime))]
    private DateTime _closedAt = DateTime.Now;

    public List<string> CommandHistory { get; }
    public List<string> DirectoryHistory { get; }

    public string ShellIconTag => ShellType switch
    {
        ShellType.PowerShell => "PS",
        ShellType.NuShell => "NU",
        ShellType.WSL => "WSL",
        ShellType.CMD => "CMD",
        _ => ">_"
    };

    public string FormattedClosedTime => ClosedAt.ToString("HH:mm");

    public ClosedTabItemViewModel()
    {
        CommandHistory = [];
        DirectoryHistory = [];
    }

    public ClosedTabItemViewModel(
        string title,
        string? workingDirectory,
        ShellType shellType,
        IEnumerable<string>? commandHistory = null,
        IEnumerable<string>? directoryHistory = null,
        DateTime? closedAt = null)
    {
        _title = title;
        _workingDirectory = workingDirectory;
        _shellType = shellType;
        _closedAt = closedAt ?? DateTime.Now;
        CommandHistory = commandHistory != null ? new List<string>(commandHistory) : [];
        DirectoryHistory = directoryHistory != null ? new List<string>(directoryHistory) : [];
    }

    public static ClosedTabItemViewModel FromTabState(TabState state)
    {
        return new ClosedTabItemViewModel(
            state.Title,
            state.WorkingDirectory,
            state.ShellType,
            state.CommandHistory,
            state.DirectoryHistory);
    }

    public TabState ToTabState()
    {
        return new TabState(
            Title,
            WorkingDirectory,
            new List<string>(CommandHistory),
            new List<string>(DirectoryHistory),
            ShellType);
    }
}
