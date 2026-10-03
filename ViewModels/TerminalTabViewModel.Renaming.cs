using System;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MultiShell.ViewModels;

/// <summary>
/// Partial class for tab renaming, custom title persistence, and tab accent color tag management.
/// </summary>
public partial class TerminalTabViewModel
{
    private string? _customTitle;

    /// <summary>
    /// Gets or sets the custom user-assigned title for this tab (REQ-TAB-020).
    /// </summary>
    public string? CustomTitle
    {
        get => _customTitle;
        set
        {
            if (SetProperty(ref _customTitle, value))
            {
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged(nameof(HasCustomTitle));
                OnPropertyChanged(nameof(TabTooltip));
            }
        }
    }

    private string? _tabColor;

    /// <summary>
    /// Gets or sets the custom tab color hex string (REQ-TAB-020).
    /// </summary>
    public string? TabColor
    {
        get => _tabColor;
        set
        {
            if (SetProperty(ref _tabColor, value))
            {
                OnPropertyChanged(nameof(HasTabColor));
                OnPropertyChanged(nameof(TabColorBrush));
            }
        }
    }

    private bool _isRenaming;

    /// <summary>
    /// Gets or sets whether inline tab renaming is currently active (REQ-TAB-020).
    /// </summary>
    public bool IsRenaming
    {
        get => _isRenaming;
        set => SetProperty(ref _isRenaming, value);
    }

    private string _renameBuffer = string.Empty;

    /// <summary>
    /// Gets or sets the temporary buffer during inline tab renaming (REQ-TAB-020).
    /// </summary>
    public string RenameBuffer
    {
        get => _renameBuffer;
        set => SetProperty(ref _renameBuffer, value);
    }

    /// <summary>
    /// Event fired when inline renaming starts to focus and select the rename text box.
    /// </summary>
    public event Action? FocusRenameBoxRequested;

    /// <summary>
    /// Gets whether a user-assigned custom tab title is active.
    /// </summary>
    public bool HasCustomTitle => !string.IsNullOrWhiteSpace(CustomTitle);

    /// <summary>
    /// Gets whether a user-assigned tab color is active and valid.
    /// </summary>
    public bool HasTabColor => !string.IsNullOrWhiteSpace(TabColor) && Color.TryParse(TabColor, out _);

    /// <summary>
    /// Gets the brush corresponding to the assigned tab color tag.
    /// </summary>
    public IBrush? TabColorBrush => HasTabColor && Color.TryParse(TabColor, out var color)
        ? new ImmutableSolidColorBrush(color)
        : null;

    /// <summary>
    /// Initiates inline tab renaming (REQ-TAB-020).
    /// </summary>
    [RelayCommand]
    public void StartRenaming()
    {
        if (!IsRenaming)
        {
            RenameBuffer = CustomTitle ?? Title ?? string.Empty;
            IsRenaming = true;
        }
        FocusRenameBoxRequested?.Invoke();
    }

    /// <summary>
    /// Confirms and commits the new tab title from the inline edit buffer (REQ-TAB-020).
    /// </summary>
    [RelayCommand]
    public void CommitRenaming()
    {
        if (!IsRenaming) return;
        var trimmed = RenameBuffer?.Trim();
        CustomTitle = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        IsRenaming = false;
        RenameBuffer = string.Empty;
    }

    /// <summary>
    /// Cancels inline tab renaming without altering the existing custom title (REQ-TAB-020).
    /// </summary>
    [RelayCommand]
    public void CancelRenaming()
    {
        IsRenaming = false;
        RenameBuffer = string.Empty;
    }

    /// <summary>
    /// Clears any custom tab title and restores default dynamic directory/process naming (REQ-TAB-020).
    /// </summary>
    [RelayCommand]
    public void ResetCustomTitle()
    {
        CustomTitle = null;
        IsRenaming = false;
        RenameBuffer = string.Empty;
    }

    /// <summary>
    /// Assigns or clears the tab's accent color tag (REQ-TAB-020).
    /// </summary>
    [RelayCommand]
    public void SetTabColor(string? colorHex)
    {
        TabColor = string.IsNullOrWhiteSpace(colorHex) || !Color.TryParse(colorHex.Trim(), out _)
            ? null
            : colorHex.Trim();
    }
}
