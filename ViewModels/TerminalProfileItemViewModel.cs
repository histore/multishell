using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using MultiShell.Models;
using MultiShell.Services;

namespace MultiShell.ViewModels;

/// <summary>
/// Observable ViewModel representing a single terminal profile for display and editing.
/// </summary>
public partial class TerminalProfileItemViewModel : ObservableObject
{
    public Guid Id { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _executablePath;

    [ObservableProperty]
    private string? _arguments;

    [ObservableProperty]
    private string? _workingDirectory;

    [ObservableProperty]
    private string _iconTag;

    [ObservableProperty]
    private ShellType _shellType;

    [ObservableProperty]
    private bool _isBuiltIn;

    public bool IsAvailable => File.Exists(ExecutablePath) || ShellDiscoveryService.ResolveExecutable(ExecutablePath) != null;

    public TerminalProfileItemViewModel(TerminalProfile profile)
    {
        Id = profile.Id;
        _name = profile.Name;
        _executablePath = profile.ExecutablePath;
        _arguments = profile.Arguments;
        _workingDirectory = profile.WorkingDirectory;
        _iconTag = string.IsNullOrWhiteSpace(profile.IconTag) ? "PS" : profile.IconTag;
        _shellType = profile.ShellType;
        _isBuiltIn = profile.IsBuiltIn;
    }

    public TerminalProfile ToModel()
    {
        return new TerminalProfile(
            Id,
            Name,
            ExecutablePath,
            Arguments,
            WorkingDirectory,
            IconTag,
            ShellType,
            IsBuiltIn);
    }
}
