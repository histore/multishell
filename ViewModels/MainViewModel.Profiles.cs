using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Models;
using MultiShell.Services;

namespace MultiShell.ViewModels;

public partial class MainViewModel
{
    private readonly IShellDiscoveryService _shellDiscoveryService;
    private readonly ITerminalProfileService _terminalProfileService;

    [ObservableProperty]
    private ShellType _defaultShellType = ShellType.PowerShell;

    [ObservableProperty]
    private bool _isProfilesModalOpen;

    [ObservableProperty]
    private bool _isEditingProfile;

    [ObservableProperty]
    private bool _isCreatingNewProfile;

    [ObservableProperty]
    private Guid? _editingProfileId;

    [ObservableProperty]
    private string _editingProfileName = string.Empty;

    [ObservableProperty]
    private string _editingExecutablePath = string.Empty;

    private string? _editingWorkingDirectory;

    public string? EditingWorkingDirectory
    {
        get => _editingWorkingDirectory;
        set => SetProperty(ref _editingWorkingDirectory, value);
    }

    [ObservableProperty]
    private string? _editingArguments;

    [ObservableProperty]
    private string _editingIconTag = "PS";

    [ObservableProperty]
    private ShellType _editingShellType = ShellType.PowerShell;

    [ObservableProperty]
    private TerminalProfileItemViewModel? _selectedProfile;

    public ObservableCollection<TerminalProfileItemViewModel> Profiles { get; } = new();

    /// <summary>
    /// Gets all detected shells and their availability on this machine.
    /// </summary>
    public IReadOnlyList<ShellOptionInfo> AvailableShells => _shellDiscoveryService.GetAvailableShells();

    public string NewTabTooltip
    {
        get
        {
            var shellKey = DefaultShellType switch
            {
                ShellType.PowerShell => "Shell_PowerShell",
                ShellType.NuShell => "Shell_NuShell",
                ShellType.WSL => "Shell_WSL",
                ShellType.CMD => "Shell_CMD",
                _ => "Shell_PowerShell"
            };
            var shellName = _localizationService[shellKey];
            return string.Format(_localizationService["Btn_New_Tab_With_Shell_Tooltip"], shellName);
        }
    }

    partial void OnDefaultShellTypeChanged(ShellType value)
    {
        OnPropertyChanged(nameof(NewTabTooltip));
        TriggerSaveState();
    }

    private void ReloadProfiles()
    {
        void Update()
        {
            var loaded = _terminalProfileService.GetProfiles();
            Profiles.Clear();
            foreach (var p in loaded)
            {
                Profiles.Add(new TerminalProfileItemViewModel(p));
            }
        }

        if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Update);
        }
    }

    [RelayCommand]
    public void OpenProfilesModal()
    {
        IsProfilesModalOpen = true;
        CancelEditProfile();
    }

    [RelayCommand]
    public void CloseProfilesModal()
    {
        IsProfilesModalOpen = false;
        CancelEditProfile();
    }

    [RelayCommand]
    public void StartNewProfile()
    {
        IsCreatingNewProfile = true;
        IsEditingProfile = true;
        EditingProfileId = null;
        EditingProfileName = "Custom Terminal";
        EditingExecutablePath = "pwsh.exe";
        EditingWorkingDirectory = TerminalProfileService.GetDefaultWorkingDirectory();
        EditingArguments = string.Empty;
        EditingIconTag = "SH";
        EditingShellType = ShellType.PowerShell;
    }

    [RelayCommand]
    public void StartEditProfile(TerminalProfileItemViewModel? item)
    {
        if (item == null) return;
        IsCreatingNewProfile = false;
        IsEditingProfile = true;
        EditingProfileId = item.Id;
        EditingProfileName = item.Name;
        EditingExecutablePath = item.ExecutablePath;
        EditingWorkingDirectory = !string.IsNullOrWhiteSpace(item.WorkingDirectory)
            ? item.WorkingDirectory
            : TerminalProfileService.GetDefaultWorkingDirectory();
        EditingArguments = item.Arguments;
        EditingIconTag = item.IconTag;
        EditingShellType = item.ShellType;
    }

    [RelayCommand]
    public void CancelEditProfile()
    {
        IsEditingProfile = false;
        IsCreatingNewProfile = false;
        EditingProfileId = null;
        EditingWorkingDirectory = null;
    }

    [RelayCommand]
    public async Task SaveProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(EditingProfileName) || string.IsNullOrWhiteSpace(EditingExecutablePath))
        {
            return;
        }

        var workingDir = !string.IsNullOrWhiteSpace(EditingWorkingDirectory)
            ? EditingWorkingDirectory.Trim()
            : TerminalProfileService.GetDefaultWorkingDirectory();

        var profile = new TerminalProfile(
            EditingProfileId ?? Guid.NewGuid(),
            EditingProfileName.Trim(),
            EditingExecutablePath.Trim(),
            string.IsNullOrWhiteSpace(EditingArguments) ? null : EditingArguments.Trim(),
            workingDir,
            string.IsNullOrWhiteSpace(EditingIconTag) ? "PS" : EditingIconTag.Trim().ToUpperInvariant(),
            EditingShellType,
            IsBuiltIn: false);

        if (IsCreatingNewProfile)
        {
            await _terminalProfileService.AddProfileAsync(profile);
        }
        else
        {
            await _terminalProfileService.UpdateProfileAsync(profile);
        }

        CancelEditProfile();
    }

    [RelayCommand]
    public async Task DeleteProfileAsync(TerminalProfileItemViewModel? item)
    {
        if (item == null) return;
        await _terminalProfileService.DeleteProfileAsync(item.Id);
        if (EditingProfileId == item.Id)
        {
            CancelEditProfile();
        }
    }

    [RelayCommand]
    public async Task ResetProfilesToDefaultAsync()
    {
        await _terminalProfileService.ResetToDefaultsAsync();
        CancelEditProfile();
    }
}
